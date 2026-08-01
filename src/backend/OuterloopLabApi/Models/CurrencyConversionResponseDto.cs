namespace OuterloopLabApi.Models;

public sealed class CurrencyConversionResponseDto
{
    public string From { get; init; } = string.Empty;
    public string To { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal ConvertedAmount { get; init; }
    public decimal Rate { get; init; }
    public string ProviderDate { get; init; } = string.Empty;
    public DateTimeOffset ServerTimestamp { get; init; }
    public string Id { get; init; } = string.Empty;
}
