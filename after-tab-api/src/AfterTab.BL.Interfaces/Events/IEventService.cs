using AfterTab.BL.DTO.Events;

namespace AfterTab.BL.Interfaces.Events;

public interface IEventService
{
    Task<EventPageDto> ListAsync(int page, CancellationToken ct);
    Task<EventDetailsDto> GetAsync(Guid id, CancellationToken ct);
    Task<EventDetailsDto> AddVirtualAsync(Guid id, VirtualParticipantDto input, CancellationToken ct);
    Task<EventDetailsDto> RenameVirtualAsync(Guid id, Guid participantId, VirtualParticipantDto input, CancellationToken ct);
    Task<EventDetailsDto> RemoveVirtualAsync(Guid id, Guid participantId, CancellationToken ct);
    Task<EventDetailsDto> LinkVirtualAsync(Guid id, Guid participantId, LinkParticipantDto input, CancellationToken ct);
    Task<CreatedEventDto> CreateAsync(CreateEventDto input, CancellationToken ct);
    Task<InviteTokenDto> RotateInviteAsync(Guid id, CancellationToken ct);
    Task RevokeInviteAsync(Guid id, CancellationToken ct);
    Task<InvitePreviewDto> PreviewAsync(string token, CancellationToken ct);
    Task<EventDetailsDto> JoinAsync(string token, CancellationToken ct);
}
