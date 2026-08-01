namespace OuterloopLabApi.Providers;

public sealed class CurrencyProviderUnavailableException : Exception
{
    public CurrencyProviderUnavailableException(string message, Exception? innerException) : base(message, innerException)
    {
    }
}
