using Microsoft.AspNetCore.Mvc;
using OrderBook.Application.Ports;

namespace OrderBook.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class HealthController(IReadiness readiness) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok();

    [HttpGet("ready")]
    public IActionResult Ready() => readiness.IsReady ? Ok() : StatusCode(StatusCodes.Status503ServiceUnavailable);
}
