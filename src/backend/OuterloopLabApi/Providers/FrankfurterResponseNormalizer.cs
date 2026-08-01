using System.Text.Json;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Providers;

public static class FrankfurterResponseNormalizer
{
    public static ProviderRateResult Normalize(JsonDocument document, string toCurrency)
    {
        var root = document.RootElement;

        var providerDate = root.TryGetProperty("date", out var dateEl) && dateEl.ValueKind == JsonValueKind.String
            ? dateEl.GetString() ?? string.Empty
            : string.Empty;

        if (!TryGetRatesElement(root, out var ratesEl))
        {
            throw new JsonException("Currency provider response does not contain a supported rates object.");
        }

        if (!ratesEl.TryGetProperty(toCurrency, out var rateEl))
        {
            throw new JsonException($"Currency provider response does not contain a rate for '{toCurrency}'.");
        }

        if (!TryReadDecimal(rateEl, out var rate))
        {
            throw new JsonException($"Currency provider rate for '{toCurrency}' is not a number.");
        }

        return new ProviderRateResult
        {
            Rate = rate,
            ProviderDate = providerDate
        };
    }

    private static bool TryGetRatesElement(JsonElement root, out JsonElement ratesEl)
    {
        if (root.TryGetProperty("rates", out var rates))
        {
            ratesEl = rates;
            return true;
        }

        if (root.TryGetProperty("conversion_rates", out var conversionRates))
        {
            ratesEl = conversionRates;
            return true;
        }

        ratesEl = default;
        return false;
    }

    private static bool TryReadDecimal(JsonElement el, out decimal value)
    {
        value = default;
        return el.ValueKind switch
        {
            JsonValueKind.Number when el.TryGetDecimal(out value) => true,
            JsonValueKind.Number =>
                TryFromDouble(el.GetDouble(), out value),
            JsonValueKind.String =>
                decimal.TryParse(el.GetString(), out value),
            _ => false
        };
    }

    private static bool TryFromDouble(double d, out decimal value)
    {
        try
        {
            value = (decimal)d;
            return true;
        }
        catch
        {
            value = default;
            return false;
        }
    }
}
