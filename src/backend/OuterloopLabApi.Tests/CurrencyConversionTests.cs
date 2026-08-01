using System.Text.Json;
using OuterloopLabApi.Infrastructure.Cosmos;
using OuterloopLabApi.Models;
using OuterloopLabApi.Providers;
using OuterloopLabApi.Services;
using Xunit;

namespace OuterloopLabApi.Tests;

public sealed class CurrencyConversionTests
{
    [Fact]
    public void Normalizes_rates_property()
    {
        var json = "{\"date\":\"2026-08-01\",\"base\":\"USD\",\"rates\":{\"EUR\":0.92}}";
        using var doc = JsonDocument.Parse(json);

        var result = FrankfurterResponseNormalizer.Normalize(doc, "EUR");

        Assert.Equal(0.92m, result.Rate);
        Assert.Equal("2026-08-01", result.ProviderDate);
    }

    [Fact]
    public void Normalizes_conversion_rates_property()
    {
        var json = "{\"date\":\"2026-08-01\",\"conversion_rates\":{\"EUR\":0.92}}";
        using var doc = JsonDocument.Parse(json);

        var result = FrankfurterResponseNormalizer.Normalize(doc, "EUR");

        Assert.Equal(0.92m, result.Rate);
        Assert.Equal("2026-08-01", result.ProviderDate);
    }

    [Fact]
    public async Task ConvertAsync_Uses_rate_for_conversion_and_persists_audit_record_shape()
    {
        var provider = new FakeRateProvider(rate: 0.92m, providerDate: "2026-08-01");
        var auditRepo = new InMemoryAuditRepository();
        var service = new CurrencyConversionService(provider, auditRepo);

        var amount = 100.00m;
        var before = DateTimeOffset.UtcNow;
        var result = await service.ConvertAsync("USD", "EUR", amount, CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        Assert.Equal("USD", result.From);
        Assert.Equal("EUR", result.To);
        Assert.Equal(amount, result.Amount);
        Assert.Equal(92.00m, result.ConvertedAmount);
        Assert.Equal(0.92m, result.Rate);
        Assert.Equal("2026-08-01", result.ProviderDate);

        Assert.False(string.IsNullOrWhiteSpace(result.Id));
        Assert.InRange(result.ServerTimestamp, before, after);

        var saved = await auditRepo.GetByIdAsync(result.Id, CancellationToken.None);
        Assert.NotNull(saved);
        Assert.Equal(result.Rate, saved!.Rate);
        Assert.Equal(result.ConvertedAmount, saved.ConvertedAmount);
        Assert.Equal(result.ProviderDate, saved.ProviderDate);
        Assert.Equal(result.ServerTimestamp, saved.ServerTimestampUtc);

        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.True(root.TryGetProperty("serverTimestamp", out _));
        Assert.True(root.TryGetProperty("providerDate", out _));
        Assert.True(root.TryGetProperty("convertedAmount", out _));
    }

    private sealed class FakeRateProvider : ICurrencyRateProvider
    {
        private readonly decimal _rate;
        private readonly string _providerDate;

        public FakeRateProvider(decimal rate, string providerDate)
        {
            _rate = rate;
            _providerDate = providerDate;
        }

        public Task<ProviderRateResult> GetRateAsync(string from, string to, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ProviderRateResult { Rate = _rate, ProviderDate = _providerDate });
        }
    }

    private sealed class InMemoryAuditRepository : IAuditRepository
    {
        private readonly Dictionary<string, AuditRecord> _records = new();

        public Task AddAsync(AuditRecord record, CancellationToken cancellationToken)
        {
            _records[record.Id] = record;
            return Task.CompletedTask;
        }

        public Task<AuditRecord?> GetByIdAsync(string id, CancellationToken cancellationToken)
        {
            _records.TryGetValue(id, out var record);
            return Task.FromResult(record);
        }
    }
}
