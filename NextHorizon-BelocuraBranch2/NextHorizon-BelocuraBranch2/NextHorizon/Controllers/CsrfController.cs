using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace NextHorizon.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/security")]
public sealed class CsrfController : ControllerBase
{
    [HttpGet("csrf-token")]
    [EnableRateLimiting("csrf-token")]
    public IActionResult GetToken([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }
}