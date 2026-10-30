namespace AfterTab.BL.DTO.Events;

public sealed record CreateEventDto(string Name, string? Description = null);
public sealed record EventPageDto(IReadOnlyList<EventSummaryDto> Items, int Page, int PageSize, int TotalCount);
public sealed record EventSummaryDto(Guid Id, string Name, string? Description, string Currency,
    string OwnerName, bool IsOwner, string Status, int ParticipantCount);
public sealed record EventMemberDto(Guid ParticipantId, string DisplayName, bool IsOwner, Guid? UserId = null)
{
    public bool IsVirtual => UserId is null;
}
public sealed record VirtualParticipantDto(string DisplayName);
public sealed record LinkParticipantDto(Guid TargetParticipantId);
public sealed record EventDetailsDto(EventSummaryDto Event, IReadOnlyList<EventMemberDto> Participants);
public sealed record CreatedEventDto(EventDetailsDto Details, string InviteToken);
public sealed record InviteTokenDto(string Token);
public sealed record InvitePreviewDto(Guid EventId, string EventName, string OwnerName, string Currency, bool AlreadyMember);
