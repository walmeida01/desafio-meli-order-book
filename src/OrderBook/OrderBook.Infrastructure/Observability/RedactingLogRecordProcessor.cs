using OpenTelemetry;
using OpenTelemetry.Logs;

namespace OrderBook.Infrastructure.Observability;

/// <summary>Sanitizes records at the SDK boundary, including records not written manually by the application.</summary>
public sealed class RedactingLogRecordProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord data)
    {
        data.Body = data.Body is string body ? SensitiveDataRedactor.Redact(body) : data.Body;
        data.FormattedMessage = SensitiveDataRedactor.Redact(data.FormattedMessage);
        data.Exception = data.Exception is null ? null : new RedactedException(data.Exception);

#pragma warning disable CS0618 // StateValues is still required to sanitize ILogger scopes in OpenTelemetry 1.15.
        if (data.StateValues is not null)
        {
            data.StateValues = data.StateValues
                .Select(static item => new KeyValuePair<string, object?>(item.Key, RedactObject(item.Value)))
                .ToList();
        }
#pragma warning restore CS0618

        if (data.Attributes is not null)
        {
            data.Attributes = data.Attributes
                .Select(static item => new KeyValuePair<string, object?>(item.Key, RedactObject(item.Value)))
                .ToList();
        }
    }

    private static object? RedactObject(object? value) =>
        value is string text ? SensitiveDataRedactor.Redact(text) : value;

}
