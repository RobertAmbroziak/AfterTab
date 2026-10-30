namespace AfterTab.DAL.Entities.Users;

// Only hashes of one-use secrets are stored. Version prevents concurrent replay.
public sealed class AccountAction : BaseEntity
{
    public Guid? UserId { get; set; }
    public string TokenHash { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string? Challenge { get; set; }
    public string? State { get; set; }
    public string? ReturnOrigin { get; set; }
    public string? BrowserHash { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
    public Guid Version { get; set; } = Guid.NewGuid();
}
