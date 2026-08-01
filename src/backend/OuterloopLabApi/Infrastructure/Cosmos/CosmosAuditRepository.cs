using System.Net;
using Microsoft.Azure.Cosmos;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Infrastructure.Cosmos;

public sealed class CosmosAuditRepository : IAuditRepository
{
    private readonly Container _container;

    public CosmosAuditRepository(Container container)
    {
        _container = container;
    }

    public async Task AddAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        var entity = CosmosAuditRecord.FromModel(record);
        await _container.CreateItemAsync(entity, new PartitionKey(entity.id), cancellationToken: cancellationToken);
    }

    public async Task<AuditRecord?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _container.ReadItemAsync<CosmosAuditRecord>(
                id,
                new PartitionKey(id),
                cancellationToken: cancellationToken);

            return response.Resource.ToModel();
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
}
