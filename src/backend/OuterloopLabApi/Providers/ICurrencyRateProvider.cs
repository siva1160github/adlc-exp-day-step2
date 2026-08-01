using System.Threading;
using OuterloopLabApi.Models;

namespace OuterloopLabApi.Providers;

public interface ICurrencyRateProvider
{
    Task<ProviderRateResult> GetRateAsync(string from, string to, CancellationToken cancellationToken);
}
