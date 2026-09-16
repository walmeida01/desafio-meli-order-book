using Microsoft.AspNetCore.Mvc;
using OrderBook.Application.Ports;

namespace OrderBook.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Produces("application/json")]
public sealed class HealthController(IReadiness readiness) : ControllerBase
{
    [HttpGet("health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Health() => Ok();

    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Ready() => readiness.IsReady ? Ok() : StatusCode(StatusCodes.Status503ServiceUnavailable);
}
