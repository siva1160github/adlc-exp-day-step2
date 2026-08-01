using System.Net;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.CosmosDB;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using OuterloopLabApi.Infrastructure.Cosmos;
using OuterloopLabApi.Models;
using OuterloopLabApi.Providers;
using OuterloopLabApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Per spec: configuration must be runtime environment variables only.
builder.Configuration.Sources.Clear();
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddControllers();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Keep defaults (camelCase etc.)
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var managedIdentityClientId = GetRequiredEnv("AZURE_MANAGED_IDENTITY_CLIENT_ID");

var cosmosOptions = new CosmosOptions(
    CosmosUri: GetRequiredEnv("COSMOS_DB_URI"),
    DatabaseId: GetRequiredEnv("COSMOS_DB_DATABASE"),
    ContainerId: GetRequiredEnv("COSMOS_DB_CONTAINER"),
    AccountName: GetRequiredEnv("COSMOS_DB_ACCOUNT_NAME"),
    ResourceGroupName: GetRequiredEnv("COSMOS_DB_RESOURCE_GROUP"),
    Region: GetRequiredEnv("COSMOS_DB_REGION"));

var currencyApiBaseUrl = GetOptionalEnv("CURRENCY_API_BASE_URL") ?? "https://frankfurter.dev";

builder.Services
    .AddHttpClient<FrankfurterCurrencyRateProvider>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
        client.BaseAddress = new Uri(currencyApiBaseUrl.TrimEnd('/'));
    });

builder.Services.AddSingleton<ICurrencyRateProvider>(sp => sp.GetRequiredService<FrankfurterCurrencyRateProvider>());
builder.Services.AddScoped<CurrencyConversionService>();

// Provision Cosmos at startup (control-plane best-effort, then mandatory data-plane create-if-not-exists).
var cosmosLogger = LoggerFactory.Create(logging => logging.AddConsole()).CreateLogger("cosmos-startup");

await TryProvisionCosmosViaArmAsync(
    cosmosOptions: cosmosOptions,
    managedIdentityClientId: managedIdentityClientId,
    logger: cosmosLogger);

var cosmosContainer = await ProvisionCosmosDataPlaneAsync(cosmosOptions, managedIdentityClientId, cosmosLogger);

builder.Services.AddSingleton<IAuditRepository>(new CosmosAuditRepository(cosmosContainer));

var app = builder.Build();
app.MapControllers();
app.Run();

static string GetRequiredEnv(string key)
{
    var value = Environment.GetEnvironmentVariable(key);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"Missing required environment variable '{key}'.");
    }

    return value;
}

static string? GetOptionalEnv(string key) => Environment.GetEnvironmentVariable(key);

static async Task TryProvisionCosmosViaArmAsync(
    CosmosOptions cosmosOptions,
    string managedIdentityClientId,
    ILogger logger)
{
    // Best-effort by spec: this may fail if ARM RBAC differs from data-plane RBAC.
    try
    {
        var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            logger.LogWarning("Skipping ARM provisioning because AZURE_SUBSCRIPTION_ID is not set.");
            return;
        }

        var credential = new DefaultAzureCredential(
            new DefaultAzureCredentialOptions
            {
                ManagedIdentityClientId = managedIdentityClientId
            });

        var armClient = new ArmClient(credential, subscriptionId);

        // Reflection-based best-effort provisioning. This avoids tightly coupling to exact SDK type names.
        // If ARM RBAC or SDK surface differs, we log and continue with data-plane provisioning.
        var accountId = $"/subscriptions/{subscriptionId}/resourceGroups/{cosmosOptions.ResourceGroupName}/providers/Microsoft.DocumentDB/databaseAccounts/{cosmosOptions.AccountName}";
        var cosmosArmAssembly = System.Reflection.Assembly.Load("Azure.ResourceManager.CosmosDB");

        var accountGetter = cosmosArmAssembly
            .GetTypes()
            .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            .FirstOrDefault(m =>
                m.Name.Contains("GetCosmosDBDatabaseAccount") &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType == typeof(ArmClient));

        if (accountGetter is null)
        {
            logger.LogWarning("ARM provisioning reflection: couldn't locate CosmosDB database account getter.");
            return;
        }

        var accountResource = accountGetter.Invoke(null, new object[] { armClient, new ResourceIdentifier(accountId) });
        if (accountResource is null)
        {
            logger.LogWarning("ARM provisioning reflection: database account getter returned null.");
            return;
        }

        var waitUntilCompleted = Azure.WaitUntil.Completed;

        // Best-effort SQL database creation.
        try
        {
            var getDbCollection = accountResource.GetType().GetMethods().FirstOrDefault(m =>
                m.Name.Contains("GetCosmosDBSqlDatabases") && m.GetParameters().Length == 0);

            var dbCollection = getDbCollection?.Invoke(accountResource, null);
            var createOrUpdateDb = dbCollection?.GetType().GetMethods().FirstOrDefault(m =>
                m.Name.Contains("CreateOrUpdateAsync") && m.GetParameters().Length >= 3);
            var dbDataType = cosmosArmAssembly.GetTypes().FirstOrDefault(t => t.Name.Contains("SqlDatabaseResourceData"));
            var dbData = dbDataType is null ? null : Activator.CreateInstance(dbDataType);

            if (createOrUpdateDb is not null && dbCollection is not null)
            {
                var taskObj = createOrUpdateDb.Invoke(dbCollection, new object[] { waitUntilCompleted, cosmosOptions.DatabaseId, dbData });
                if (taskObj is Task task) await task;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ARM provisioning reflection: SQL database create attempt failed.");
        }

        // Best-effort SQL container creation.
        try
        {
            var getContainerCollection = accountResource.GetType().GetMethods().FirstOrDefault(m =>
                m.Name.Contains("GetCosmosDBSqlContainers") && m.GetParameters().Length == 1);

            var containerCollection = getContainerCollection?.Invoke(accountResource, new object[] { cosmosOptions.DatabaseId });
            var createOrUpdateContainer = containerCollection?.GetType().GetMethods().FirstOrDefault(m =>
                m.Name.Contains("CreateOrUpdateAsync") && m.GetParameters().Length >= 3);

            var containerDataType = cosmosArmAssembly.GetTypes().FirstOrDefault(t => t.Name.Contains("SqlContainerResourceData"));
            object? containerData = containerDataType is null ? null : Activator.CreateInstance(containerDataType);

            // Try set partition key path if we can find a writable property.
            if (containerData is not null)
            {
                var pkProp = containerDataType!.GetProperties().FirstOrDefault(p =>
                    p.Name.Contains("PartitionKey") && p.CanWrite);
                pkProp?.SetValue(containerData, "/id");
            }

            if (createOrUpdateContainer is not null && containerCollection is not null)
            {
                var taskObj = createOrUpdateContainer.Invoke(containerCollection, new object[] { waitUntilCompleted, cosmosOptions.ContainerId, containerData });
                if (taskObj is Task task) await task;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "ARM provisioning reflection: SQL container create attempt failed.");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "ARM provisioning attempt failed; continuing with data-plane provisioning.");
    }
}

static async Task<Container> ProvisionCosmosDataPlaneAsync(
    CosmosOptions cosmosOptions,
    string managedIdentityClientId,
    ILogger logger)
{
    var credential = new DefaultAzureCredential(
        new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = managedIdentityClientId
        });

    // No connection strings or keys: token-based auth only.
    var cosmosClient = new CosmosClient(
        cosmosOptions.CosmosUri,
        credential,
        new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway });

    // Per spec: mandatory token-auth data-plane create-if-not-exists must fail startup if it fails.
    logger.LogInformation("Creating Cosmos database/container (data-plane) if not exists.");

    var dbResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(cosmosOptions.DatabaseId);

    var containerProperties = new ContainerProperties(cosmosOptions.ContainerId, "/id");
    var containerResponse = await dbResponse.Database.CreateContainerIfNotExistsAsync(
        containerProperties,
        throughput: null);

    return containerResponse.Container;
}

public readonly record struct CosmosOptions(
    string CosmosUri,
    string DatabaseId,
    string ContainerId,
    string AccountName,
    string ResourceGroupName,
    string Region);
