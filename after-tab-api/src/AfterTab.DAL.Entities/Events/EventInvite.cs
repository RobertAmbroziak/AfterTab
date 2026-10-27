using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Events;
public sealed class EventInvite : BaseEntity
{
    public Guid EventId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
