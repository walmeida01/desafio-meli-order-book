using System.Reflection;

namespace OrderBook.Infrastructure.Observability;

public sealed class ObservabilityOptions
{
    public const string ServiceName = "meli-order-book-api";
    public const string ActivitySourceName = "MeliOrderBook";
    public const string OtlpEndpointEnvironmentVariable = "OTEL_EXPORTER_OTLP_ENDPOINT";

    public string ServiceVersion { get; init; } = ResolveAssemblyVersion();
    public string DeploymentEnvironmentName { get; init; } = "local";
    public string? ServiceInstanceId { get; init; }
    public Uri OtlpEndpoint { get; init; } = new("http://alloy:4317");

    public static ObservabilityOptions FromEnvironment()
    {
        var endpoint = Environment.GetEnvironmentVariable(OtlpEndpointEnvironmentVariable);
        return new ObservabilityOptions
        {
            ServiceVersion = Environment.GetEnvironmentVariable("SERVICE_VERSION") ?? ResolveAssemblyVersion(),
            DeploymentEnvironmentName = Environment.GetEnvironmentVariable("DEPLOYMENT_ENVIRONMENT_NAME") ?? "local",
            ServiceInstanceId = Environment.GetEnvironmentVariable("SERVICE_INSTANCE_ID"),
            OtlpEndpoint = Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) ? uri : new Uri("http://alloy:4317")
        };
    }

    private static string ResolveAssemblyVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "unknown";
}
