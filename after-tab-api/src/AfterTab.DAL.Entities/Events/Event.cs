using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Events;
public sealed class Event : PublicEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long OwnerUserId { get; set; }
    public string Currency { get; set; } = "PLN";
    public EventStatus Status { get; set; } = EventStatus.Active;
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public int SettlementVersion { get; set; }
}
