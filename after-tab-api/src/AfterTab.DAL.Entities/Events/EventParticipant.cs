using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Events;
public sealed class EventParticipant : PublicEntity
{
    public long EventId { get; set; }
    public long? UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public long? MergedIntoParticipantId { get; set; }
    public ParticipantStatus Status { get; set; } = ParticipantStatus.Active;
    public DateTimeOffset JoinedAtUtc { get; set; }
    public DateTimeOffset? LeftAtUtc { get; set; }
}
