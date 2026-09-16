using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Metadata;
using OrderBook.Application.Ports;
using OrderBook.Contracts.Queries;

namespace OrderBook.Api.Controllers;

[ApiController]
[Route("api/v1/order-book")]
[Produces("application/json")]
public sealed class OrderBookController(IReadiness readiness, IOrderQueries queries) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Consulta o livro de ofertas")]
    [EndpointDescription("Retorna somente ordens BUY e SELL ainda abertas, ordenadas por preco e prioridade.")]
    [ProducesResponseType(typeof(IReadOnlyList<BookLevel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        !readiness.IsReady ? StatusCode(503) : Ok(await queries.GetBookAsync(cancellationToken));
}
