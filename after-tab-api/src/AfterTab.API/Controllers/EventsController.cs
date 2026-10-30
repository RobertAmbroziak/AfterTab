using AfterTab.BL.DTO.Events;
using AfterTab.BL.Interfaces.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AfterTab.API.Controllers;

[ApiController, Authorize, Route("api/events")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EventsController(IEventService events) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int? page = null)
    {
        var result = await events.ListAsync(page ?? 1, ct);
        // Installed PWAs can keep the previous bundle until the user accepts an update.
        // Their request has no page parameter and expects an array; still limit it to ten.
        return page.HasValue ? Ok(result) : Ok(result.Items);
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await events.GetAsync(id, ct));
    [HttpPost]
    public async Task<IActionResult> Create(CreateEventDto input, CancellationToken ct)
    {
        var created = await events.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Details.Event.Id }, created);
    }
    [HttpPost("{id:guid}/participants/virtual")]
    public async Task<IActionResult> AddVirtual(Guid id, VirtualParticipantDto input, CancellationToken ct)
        => Ok(await events.AddVirtualAsync(id, input, ct));
    [HttpPut("{id:guid}/participants/{participantId:guid}")]
    public async Task<IActionResult> RenameVirtual(Guid id, Guid participantId, VirtualParticipantDto input, CancellationToken ct)
        => Ok(await events.RenameVirtualAsync(id, participantId, input, ct));
    [HttpDelete("{id:guid}/participants/{participantId:guid}")]
    public async Task<IActionResult> RemoveVirtual(Guid id, Guid participantId, CancellationToken ct)
        => Ok(await events.RemoveVirtualAsync(id, participantId, ct));
    [HttpPost("{id:guid}/participants/{participantId:guid}/link")]
    public async Task<IActionResult> LinkVirtual(Guid id, Guid participantId, LinkParticipantDto input, CancellationToken ct)
        => Ok(await events.LinkVirtualAsync(id, participantId, input, ct));
    [HttpPost("{id:guid}/invite")]
    public async Task<IActionResult> Rotate(Guid id, CancellationToken ct) => Ok(await events.RotateInviteAsync(id, ct));
    [HttpDelete("{id:guid}/invite")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await events.RevokeInviteAsync(id, ct);
        return NoContent();
    }
}
