using System.Diagnostics;
using OrderBook.Infrastructure;
using OrderBook.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOrderBookObservability();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton(new ActivitySource(ObservabilityOptions.ActivitySourceName));
builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(30));

var app = builder.Build();

app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("X-Correlation-Id", out var supplied) && !string.IsNullOrWhiteSpace(supplied)
        ? supplied.ToString()
        : Guid.NewGuid().ToString("N");
    context.Response.Headers["X-Correlation-Id"] = correlationId;
    Activity.Current?.SetTag("meli.correlation_id", correlationId);
    using (app.Logger.BeginScope(new Dictionary<string, object?> { ["correlation_id"] = correlationId }))
        await next(context);
});

app.MapControllers();
app.MapPrometheusScrapingEndpoint();

app.Run();
