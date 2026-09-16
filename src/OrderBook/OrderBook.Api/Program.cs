using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using OrderBook.Contracts.Orders;
using OrderBook.Infrastructure;
using OrderBook.Infrastructure.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOrderBookObservability();
builder.Services.AddControllers();
builder.Services.AddOpenApi("v1", options => options.AddSchemaTransformer((schema, context, _) =>
{
    if (schema.Properties is { } properties && properties.TryGetValue("side", out var side) && side is not null)
    {
        var enumSchema = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = side.Pattern };
        enumSchema.Enum = [JsonValue.Create("BUY")!, JsonValue.Create("SELL")!];
        properties["side"] = enumSchema;
    }

    if (schema.Properties is { } responseProperties && responseProperties.TryGetValue("status", out var status) && status is not null)
    {
        var enumSchema = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = status.Pattern };
        enumSchema.Enum = [
            JsonValue.Create("OPEN")!,
            JsonValue.Create("PARTIALLY_FILLED")!,
            JsonValue.Create("FILLED")!,
            JsonValue.Create("REJECTED")!
        ];
        responseProperties["status"] = enumSchema;
    }

    return Task.CompletedTask;
}));
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
app.MapOpenApi("/openapi/{documentName}.json");
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Meli Order Book API";
    options.SwaggerEndpoint("/openapi/v1.json", "Meli Order Book V1");
});
app.MapPrometheusScrapingEndpoint();

app.Run();
