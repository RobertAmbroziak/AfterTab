using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Users;
public sealed class Session : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? GuestDeviceId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset LastUsedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
    public string? DeviceLabel { get; set; }
}
