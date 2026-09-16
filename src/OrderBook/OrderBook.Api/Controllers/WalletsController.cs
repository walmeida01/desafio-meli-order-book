using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Metadata;
using OrderBook.Application.Ports;
using OrderBook.Contracts.Queries;

namespace OrderBook.Api.Controllers;

[ApiController]
[Route("api/v1/wallets")]
[Produces("application/json")]
public sealed class WalletsController(IReadiness readiness, IOrderQueries queries) : ControllerBase
{
    [HttpGet("{userId:guid}")]
    [EndpointSummary("Consulta uma carteira")]
    [EndpointDescription("Retorna saldos BRL e Vibranium, separados entre valores disponiveis e bloqueados.")]
    [ProducesResponseType(typeof(WalletResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(Guid userId, CancellationToken cancellationToken) =>
        !readiness.IsReady ? StatusCode(503) : (await queries.GetWalletAsync(userId, cancellationToken)) is { } result ? Ok(result) : NotFound();
}
