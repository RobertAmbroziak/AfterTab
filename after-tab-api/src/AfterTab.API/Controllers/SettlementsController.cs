using AfterTab.BL.Interfaces.Events;
using AfterTab.API.Contracts;
using AfterTab.BL.DTO.Events;
using AfterTab.DAL.Entities.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AfterTab.API.Controllers;
[ApiController, Authorize, Route("api/events/{eventId:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SettlementsController(ISettlementService settlements, PublicIds ids) : ControllerBase
{
    [HttpGet("settlement")]
    public async Task<IActionResult> Get(Guid eventId, CancellationToken ct) => Ok(await settlements.GetAsync(await ids.ResolveAsync<Event>(eventId, ct), ct));
    [HttpPost("close")]
    public async Task<IActionResult> Close(Guid eventId, CancellationToken ct) => Ok(await settlements.SetClosedAsync(await ids.ResolveAsync<Event>(eventId, ct), true, ct));
    [HttpPost("reopen")]
    public async Task<IActionResult> Reopen(Guid eventId, CancellationToken ct) => Ok(await settlements.SetClosedAsync(await ids.ResolveAsync<Event>(eventId, ct), false, ct));
    [HttpPut("settlement/confirmation")]
    public async Task<IActionResult> Mark(Guid eventId, PublicMarkTransfer input, CancellationToken ct)
        => Ok(await settlements.MarkAsync(await ids.ResolveAsync<Event>(eventId, ct), new MarkTransferDto(input.Version,
            await ids.ResolveAsync<EventParticipant>(input.FromParticipantId, ct), await ids.ResolveAsync<EventParticipant>(input.ToParticipantId, ct),
            input.AmountMinor, input.Side, input.Value), ct));
}
