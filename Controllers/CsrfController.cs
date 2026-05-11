using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace MyAspNetApp.Controllers;

[ApiController]
[Route("api/security")]
public sealed class CsrfController : ControllerBase
{
    [HttpGet("csrf-token")]
    public IActionResult GetToken([FromServices] IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }
}
