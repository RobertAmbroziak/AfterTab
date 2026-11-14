using AfterTab.BL.DTO.Events;

namespace AfterTab.BL.Interfaces.Events;

public interface IEventService
{
    Task<EventPageDto> ListAsync(int page, CancellationToken ct);
    Task<EventDetailsDto> GetAsync(long id, CancellationToken ct);
    Task<EventDetailsDto> RenameAsync(long id, UpdateEventNameDto input, CancellationToken ct);
    Task<EventDetailsDto> AddVirtualAsync(long id, VirtualParticipantDto input, CancellationToken ct);
    Task<EventDetailsDto> RenameVirtualAsync(long id, long participantId, VirtualParticipantDto input, CancellationToken ct);
    Task<EventDetailsDto> RemoveVirtualAsync(long id, long participantId, CancellationToken ct);
    Task<EventDetailsDto> LinkVirtualAsync(long id, long participantId, LinkParticipantDto input, CancellationToken ct);
    Task<CreatedEventDto> CreateAsync(CreateEventDto input, CancellationToken ct);
    Task<InviteTokenDto> RotateInviteAsync(long id, CancellationToken ct);
    Task RevokeInviteAsync(long id, CancellationToken ct);
    Task<InvitePreviewDto> PreviewAsync(string token, CancellationToken ct);
    Task<EventDetailsDto> JoinAsync(string token, CancellationToken ct);
}
