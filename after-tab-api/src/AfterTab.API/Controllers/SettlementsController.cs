using AfterTab.BL.Interfaces.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AfterTab.API.Controllers;
[ApiController, Authorize, Route("api/events/{eventId:guid}")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SettlementsController(ISettlementService settlements) : ControllerBase
{
    [HttpGet("settlement")]
    public async Task<IActionResult> Get(Guid eventId, CancellationToken ct) => Ok(await settlements.GetAsync(eventId, ct));
    [HttpPost("close")]
    public async Task<IActionResult> Close(Guid eventId, CancellationToken ct) => Ok(await settlements.SetClosedAsync(eventId, true, ct));
    [HttpPost("reopen")]
    public async Task<IActionResult> Reopen(Guid eventId, CancellationToken ct) => Ok(await settlements.SetClosedAsync(eventId, false, ct));
    [HttpPut("settlement/confirmation")]
    public async Task<IActionResult> Mark(Guid eventId, AfterTab.BL.DTO.Events.MarkTransferDto input, CancellationToken ct)
        => Ok(await settlements.MarkAsync(eventId, input, ct));
}
