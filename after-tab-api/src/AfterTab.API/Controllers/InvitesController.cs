using AfterTab.BL.DTO.Events;
using AfterTab.BL.Interfaces.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AfterTab.API.Controllers;

[ApiController, Route("api/invites"), EnableRateLimiting("invites")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class InvitesController(IEventService events) : ControllerBase
{
    [AllowAnonymous, HttpPost("preview")]
    public async Task<IActionResult> Preview(InviteTokenDto input, CancellationToken ct) => Ok(await events.PreviewAsync(input.Token, ct));
    [Authorize, HttpPost("join")]
    public async Task<IActionResult> Join(InviteTokenDto input, CancellationToken ct) => Ok(await events.JoinAsync(input.Token, ct));
}
