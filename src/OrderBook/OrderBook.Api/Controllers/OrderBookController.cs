using Microsoft.AspNetCore.Mvc;
using OrderBook.Application.Ports;

namespace OrderBook.Api.Controllers;

[ApiController]
[Route("api/v1/order-book")]
public sealed class OrderBookController(IReadiness readiness, IOrderQueries queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        !readiness.IsReady ? StatusCode(503) : Ok(await queries.GetBookAsync(cancellationToken));
}
