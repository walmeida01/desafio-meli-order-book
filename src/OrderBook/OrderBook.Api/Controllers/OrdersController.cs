using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Metadata;
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
[Produces("application/json")]
public sealed class OrdersController(ISubmitOrder submission, IReadiness readiness, AdmissionState admission, OrderBookMetrics metrics, ILogger<OrdersController> logger) : ControllerBase
{
    [HttpPost]
    [EndpointSummary("Cria uma ordem de compra ou venda")]
    [EndpointDescription("Reserva saldo, executa o matching contra ordens compativeis e devolve o resultado da ordem.")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Create([FromBody] SubmitOrderRequest payload, CancellationToken cancellationToken)
    {
        if (!readiness.IsReady || !admission.Accepting) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var idempotencyKey = Guid.NewGuid().ToString("N");

        SubmitOrderCommand command;
        try
        {
            var side = Enum.Parse<Side>(payload.Side, true);
            var user = UserId.Create(payload.UserId);
            var price = BrlCents.Create(payload.PriceBrlCents);
            var quantity = Quantity.Create(payload.Quantity);
            var canonical = new CanonicalOrderPayload(payload.UserId, side.ToString(), payload.PriceBrlCents, payload.Quantity);
            command = new(Guid.NewGuid(), user, side, price, quantity, IdempotencyKey.Create(idempotencyKey), CanonicalPayload.Hash(canonical));
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or DomainException)
        {
            logger.LogWarning("Order payload failed domain validation: {Error}", SensitiveDataRedactor.Redact(exception.Message));
            return BadRequest(new { code = "INVALID_PAYLOAD" });
        }

        metrics.OrdersReceived.Add(1, new KeyValuePair<string, object?>("side", command.Side.ToString()));
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
    [EndpointSummary("Consulta uma ordem")]
    [EndpointDescription("Retorna o status, quantidades executadas e negocios associados a uma ordem.")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(Guid id, [FromServices] IOrderQueries queries, CancellationToken cancellationToken) =>
        !readiness.IsReady ? StatusCode(503) : (await queries.GetOrderAsync(id, cancellationToken)) is { } result ? Ok(result) : NotFound();
}
