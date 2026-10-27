using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Events;
public sealed class EventParticipant : BaseEntity
{
    public Guid EventId { get; set; }
    public Guid UserId { get; set; }
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Active;
    public DateTimeOffset JoinedAtUtc { get; set; }
    public DateTimeOffset? LeftAtUtc { get; set; }
}
