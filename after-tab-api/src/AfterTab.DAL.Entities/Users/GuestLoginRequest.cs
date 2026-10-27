namespace AfterTab.DAL.Entities.Users;

public sealed class GuestLoginRequest : BaseEntity
{
    public string RequestHash { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ReturnOrigin { get; set; } = string.Empty;
    public string Action { get; set; } = "login";
    public string? ProposedDisplayName { get; set; }
    public Guid? LegacyUserId { get; set; }
    public Guid? LegacySessionId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? GuestDeviceId { get; set; }
    public string? CodeHash { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
