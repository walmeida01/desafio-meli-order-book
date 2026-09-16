using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OrderBook.Infrastructure.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddOrderBookObservability(this IServiceCollection services)
    {
        var options = ObservabilityOptions.FromEnvironment();
        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: ObservabilityOptions.ServiceName,
                serviceVersion: options.ServiceVersion,
                serviceInstanceId: options.ServiceInstanceId)
            .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", options.DeploymentEnvironmentName)]);
        services.AddSingleton(options);
        services.AddOpenTelemetry()
            .WithMetrics(metrics => metrics
                .SetResourceBuilder(resourceBuilder)
                .AddMeter(OrderBookMetrics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = options.OtlpEndpoint)
                .AddPrometheusExporter());
        services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .SetResourceBuilder(resourceBuilder)
                .AddSource(ObservabilityOptions.ActivitySourceName)
                .AddSource("OrderBook.Api")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(exporter => exporter.Endpoint = options.OtlpEndpoint));
        services.AddOpenTelemetry().WithLogging(logging => logging
            .SetResourceBuilder(resourceBuilder)
            .AddProcessor(new RedactingLogRecordProcessor())
            .AddOtlpExporter(exporter => exporter.Endpoint = options.OtlpEndpoint), loggingOptions =>
        {
            loggingOptions.IncludeScopes = true;
            loggingOptions.IncludeFormattedMessage = true;
            loggingOptions.ParseStateValues = true;
        });
        return services;
    }
}
