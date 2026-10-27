using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Events;
public sealed class Event : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Currency { get; set; } = "PLN";
    public EventStatus Status { get; set; } = EventStatus.Active;
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public int SettlementVersion { get; set; }
}
