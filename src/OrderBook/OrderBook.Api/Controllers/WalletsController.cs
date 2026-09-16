using Microsoft.AspNetCore.Mvc;
using OrderBook.Application.Ports;

namespace OrderBook.Api.Controllers;

[ApiController]
[Route("api/v1/wallets")]
public sealed class WalletsController(IReadiness readiness, IOrderQueries queries) : ControllerBase
{
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken) =>
        !readiness.IsReady ? StatusCode(503) : (await queries.GetWalletAsync(userId, cancellationToken)) is { } result ? Ok(result) : NotFound();
}
