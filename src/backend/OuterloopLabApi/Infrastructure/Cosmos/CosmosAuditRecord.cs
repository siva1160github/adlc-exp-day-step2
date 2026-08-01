using System.Text.Json.Serialization;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Infrastructure.Cosmos;

public sealed class CosmosAuditRecord
{
    [JsonPropertyName("id")]
    public string id { get; set; } = string.Empty;

    public string from { get; set; } = string.Empty;
    public string to { get; set; } = string.Empty;
    public decimal amount { get; set; }
    public decimal convertedAmount { get; set; }
    public decimal rate { get; set; }
    public string providerDate { get; set; } = string.Empty;
    public DateTimeOffset serverTimestampUtc { get; set; }

    public static CosmosAuditRecord FromModel(AuditRecord model) => new()
    {
        id = model.Id,
        from = model.From,
        to = model.To,
        amount = model.Amount,
        convertedAmount = model.ConvertedAmount,
        rate = model.Rate,
        providerDate = model.ProviderDate,
        serverTimestampUtc = model.ServerTimestampUtc
    };

    public AuditRecord ToModel() => new()
    {
        Id = id,
        From = from,
        To = to,
        Amount = amount,
        ConvertedAmount = convertedAmount,
        Rate = rate,
        ProviderDate = providerDate,
        ServerTimestampUtc = serverTimestampUtc
    };
}
