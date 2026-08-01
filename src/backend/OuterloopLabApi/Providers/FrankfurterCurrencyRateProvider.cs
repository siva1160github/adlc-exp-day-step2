using System.Net.Http.Json;
using System.Text.Json;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Providers;

public sealed class FrankfurterCurrencyRateProvider : ICurrencyRateProvider
{
    private readonly HttpClient _httpClient;

    public FrankfurterCurrencyRateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProviderRateResult> GetRateAsync(string from, string to, CancellationToken cancellationToken)
    {
        // Frankfurter provides latest rates at /latest?base=USD&symbols=EUR
        var requestUri = $"/latest?base={Uri.EscapeDataString(from)}&symbols={Uri.EscapeDataString(to)}";

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Upstream responded with HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return FrankfurterResponseNormalizer.Normalize(doc, to);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            throw new CurrencyProviderUnavailableException("Currency provider failed to return a rate.", ex);
        }
    }
}
