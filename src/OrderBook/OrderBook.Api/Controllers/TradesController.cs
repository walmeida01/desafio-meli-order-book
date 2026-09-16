using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Metadata;
using OrderBook.Application.Ports;
using OrderBook.Contracts.Queries;

namespace OrderBook.Api.Controllers;

[ApiController]
[Route("api/v1/trades")]
[Produces("application/json")]
public sealed class TradesController(IReadiness readiness, IOrderQueries queries) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Consulta o historico de negocios")]
    [EndpointDescription("Retorna trades liquidados em ordem deterministica, com paginacao por cursor.")]
    [ProducesResponseType(typeof(TradePage), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get([FromQuery] string? cursor, [FromQuery, Range(1, 100), DefaultValue(50)] int? limit, CancellationToken cancellationToken)
    {
        if (!readiness.IsReady) return StatusCode(503);
        var parsedLimit = limit ?? 50;
        if (parsedLimit is < 1 or > 100) return BadRequest(new { code = "INVALID_LIMIT" });
        if (cursor is not null)
            try { _ = Convert.FromBase64String(cursor); } catch (FormatException) { return BadRequest(new { code = "INVALID_CURSOR" }); }
        try { return Ok(await queries.GetTradesAsync(cursor, parsedLimit, cancellationToken)); }
        catch (ArgumentException) { return BadRequest(new { code = "INVALID_CURSOR" }); }
    }
}
