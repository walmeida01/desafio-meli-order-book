using Microsoft.AspNetCore.Mvc;
using OrderBook.Application.Ports;

namespace OrderBook.Api.Controllers;

[ApiController]
[Route("api/v1/trades")]
public sealed class TradesController(IReadiness readiness, IOrderQueries queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string? cursor, [FromQuery] string? limit, CancellationToken cancellationToken)
    {
        if (!readiness.IsReady) return StatusCode(503);
        var parsedLimit = string.IsNullOrEmpty(limit) ? 50 : int.TryParse(limit, out var value) ? value : 0;
        if (parsedLimit is < 1 or > 100) return BadRequest(new { code = "INVALID_LIMIT" });
        if (cursor is not null)
            try { _ = Convert.FromBase64String(cursor); } catch (FormatException) { return BadRequest(new { code = "INVALID_CURSOR" }); }
        try { return Ok(await queries.GetTradesAsync(cursor, parsedLimit, cancellationToken)); }
        catch (ArgumentException) { return BadRequest(new { code = "INVALID_CURSOR" }); }
    }
}
