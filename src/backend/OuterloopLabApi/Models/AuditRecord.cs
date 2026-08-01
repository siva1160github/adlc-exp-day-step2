namespace OuterloopLabApi.Models;

public sealed class AuditRecord
{
    public string Id { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal ConvertedAmount { get; set; }
    public decimal Rate { get; set; }
    public string ProviderDate { get; set; } = string.Empty;
    public DateTimeOffset ServerTimestampUtc { get; set; }
}
