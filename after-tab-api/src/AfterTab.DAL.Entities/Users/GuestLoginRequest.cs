namespace AfterTab.DAL.Entities.Users;

public sealed class GuestLoginRequest : BaseEntity
{
    public string RequestHash { get; set; } = string.Empty;
    public string Challenge { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ReturnOrigin { get; set; } = string.Empty;
    public string Action { get; set; } = "login";
    public string? ProposedDisplayName { get; set; }
    public long? LegacyUserId { get; set; }
    public long? LegacySessionId { get; set; }
    public long? UserId { get; set; }
    public long? GuestDeviceId { get; set; }
    public string? CodeHash { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
