using OuterloopLabApi.Infrastructure.Cosmos;
using OuterloopLabApi.Models;
using OuterloopLabApi.Providers;

namespace OuterloopLabApi.Services;

public sealed class CurrencyConversionService
{
    private readonly ICurrencyRateProvider _provider;
    private readonly IAuditRepository _auditRepository;

    public CurrencyConversionService(ICurrencyRateProvider provider, IAuditRepository auditRepository)
    {
        _provider = provider;
        _auditRepository = auditRepository;
    }

    public async Task<CurrencyConversionResponseDto> ConvertAsync(
        string from,
        string to,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var normalizedFrom = NormalizeCurrencyCode(from);
        var normalizedTo = NormalizeCurrencyCode(to);

        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        var rateResult = await _provider.GetRateAsync(normalizedFrom, normalizedTo, cancellationToken);

        // Per spec, the persisted rate must be the exact rate used for the conversion.
        var convertedAmount = amount * rateResult.Rate;
        var serverTimestampUtc = DateTimeOffset.UtcNow;

        var record = new AuditRecord
        {
            Id = Guid.NewGuid().ToString(),
            From = normalizedFrom,
            To = normalizedTo,
            Amount = amount,
            ConvertedAmount = convertedAmount,
            Rate = rateResult.Rate,
            ProviderDate = rateResult.ProviderDate,
            ServerTimestampUtc = serverTimestampUtc
        };

        await _auditRepository.AddAsync(record, cancellationToken);

        return new CurrencyConversionResponseDto
        {
            Id = record.Id,
            From = record.From,
            To = record.To,
            Amount = record.Amount,
            ConvertedAmount = record.ConvertedAmount,
            Rate = record.Rate,
            ProviderDate = record.ProviderDate,
            ServerTimestamp = record.ServerTimestampUtc
        };
    }

    public async Task<AuditRecordResponseDto?> GetAuditAsync(string id, CancellationToken cancellationToken)
    {
        var record = await _auditRepository.GetByIdAsync(id, cancellationToken);
        if (record is null)
        {
            return null;
        }

        return new AuditRecordResponseDto
        {
            Id = record.Id,
            From = record.From,
            To = record.To,
            Amount = record.Amount,
            ConvertedAmount = record.ConvertedAmount,
            Rate = record.Rate,
            ProviderDate = record.ProviderDate,
            ServerTimestamp = record.ServerTimestampUtc
        };
    }

    private static string NormalizeCurrencyCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("Currency code is required.", nameof(code));
        }

        var upper = code.Trim().ToUpperInvariant();
        return upper;
    }
}
