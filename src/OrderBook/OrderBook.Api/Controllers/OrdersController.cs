using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OrderBook.Application.Modules.Orders.SubmitOrder;
using OrderBook.Application.Modules.Orders.SubmitOrder.Idempotency;
using OrderBook.Application.Ports;
using OrderBook.Contracts.Orders;
using OrderBook.Domain.Shared;
using OrderBook.Infrastructure;
using OrderBook.Infrastructure.Observability;
using OrderBook.Infrastructure.Postgres;

namespace OrderBook.Api.Controllers;

[ApiController]
[Route("api/v1/orders")]
public sealed class OrdersController(ISubmitOrder submission, IReadiness readiness, AdmissionState admission, OrderBookMetrics metrics, ILogger<OrdersController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, [FromBody] JsonElement body, CancellationToken cancellationToken)
    {
        if (!readiness.IsReady || !admission.Accepting) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (idempotencyKey is null || !Request.Headers.TryGetValue("Idempotency-Key", out var values) || values.Count != 1)
            return BadRequest(new { code = "INVALID_IDEMPOTENCY_KEY" });

        SubmitOrderRequest payload;
        SubmitOrderCommand command;
        try
        {
            var names = body.ValueKind == JsonValueKind.Object ? body.EnumerateObject().Select(property => property.Name).ToArray() : [];
            var expected = new[] { "userId", "side", "priceBrlCents", "quantity" };
            if (body.ValueKind != JsonValueKind.Object || names.Length != expected.Length || names.Except(expected).Any() || expected.Except(names).Any()) throw new JsonException();
            payload = body.Deserialize<SubmitOrderRequest>(new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new JsonException();
            var side = Enum.Parse<Side>(payload.Side, true);
            var user = UserId.Create(payload.UserId);
            var price = BrlCents.Create(payload.PriceBrlCents);
            var quantity = Quantity.Create(payload.Quantity);
            var canonical = new CanonicalOrderPayload(payload.UserId, side.ToString(), payload.PriceBrlCents, payload.Quantity);
            command = new(Guid.NewGuid(), user, side, price, quantity, IdempotencyKey.Create(idempotencyKey), CanonicalPayload.Hash(canonical));
        }
        catch (Exception exception) when (exception is JsonException or FormatException or ArgumentException or DomainException)
        {
            logger.LogWarning("Order payload failed domain validation: {Error}", SensitiveDataRedactor.Redact(exception.Message));
            return BadRequest(new { code = "INVALID_PAYLOAD" });
        }

        metrics.OrdersReceived.Add(1);
        if (!submission.TrySubmit(command, out var result))
        {
            metrics.QueueRejected.Add(1);
            Response.Headers["Retry-After"] = "1";
            return StatusCode(StatusCodes.Status429TooManyRequests);
        }

        try
        {
            var output = await result.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);
            return new ObjectResult(output) { StatusCode = output.HttpStatus };
        }
        catch (SubmissionConflictException) { return Conflict(new { code = "IDEMPOTENCY_CONFLICT" }); }
        catch (SubmissionRejectedException exception) { return Conflict(new { code = "ORDER_REJECTED", message = exception.Message }); }
        catch (SubmissionUnavailableException) { Response.Headers["Retry-After"] = "1"; return StatusCode(StatusCodes.Status429TooManyRequests); }
        catch (TimeoutException) { return StatusCode(StatusCodes.Status504GatewayTimeout); }
        catch (NpgsqlException) { return StatusCode(StatusCodes.Status503ServiceUnavailable); }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromServices] IOrderQueries queries, CancellationToken cancellationToken) =>
        !readiness.IsReady ? StatusCode(503) : (await queries.GetOrderAsync(id, cancellationToken)) is { } result ? Ok(result) : NotFound();
}
