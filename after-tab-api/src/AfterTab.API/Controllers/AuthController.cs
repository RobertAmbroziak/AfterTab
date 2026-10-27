using AfterTab.API.Security;
using AfterTab.BL.Interfaces.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AfterTab.API.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(ISessionService sessions) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("guest")]
    [EnableRateLimiting("guest")]
    public async Task<IActionResult> Guest(CancellationToken cancellationToken)
        => Ok(await sessions.CreateGuestAsync(SessionAuthenticationHandler.ReadToken(Request), cancellationToken));

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var profile = await sessions.ValidateAsync(SessionAuthenticationHandler.ReadToken(Request), cancellationToken);
        return profile is null ? Unauthorized() : Ok(profile);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessions.RevokeAsync(SessionAuthenticationHandler.ReadToken(Request)!, cancellationToken);
        return NoContent();
    }
}
