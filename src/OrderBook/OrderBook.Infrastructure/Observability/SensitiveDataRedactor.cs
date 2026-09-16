using System.Text.RegularExpressions;

namespace OrderBook.Infrastructure.Observability;

public static partial class SensitiveDataRedactor
{
    [GeneratedRegex("(?i)(idempotency[-_ ]?key|hash|balance|saldo|secret|password|passwd|token|api[-_ ]?key|authorization|cookie|set-cookie|payload|body|request|connectionstring|connection string|connection[-_ ]?strings?)(\\s*[:=]\\s*)[^,;\\r\\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveValuePattern();

    public static string Redact(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : SensitiveValuePattern().Replace(value, "$1$2[REDACTED]");
}
