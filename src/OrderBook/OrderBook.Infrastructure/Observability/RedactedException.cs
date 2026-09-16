namespace OrderBook.Infrastructure.Observability;

internal sealed class RedactedException(Exception source)
    : Exception(SensitiveDataRedactor.Redact(source.Message), source.InnerException is null ? null : new RedactedException(source.InnerException))
{
    public override string StackTrace => source.StackTrace ?? string.Empty;
}
