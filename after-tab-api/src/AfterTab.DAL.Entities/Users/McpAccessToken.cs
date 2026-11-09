namespace AfterTab.DAL.Entities.Users;

public sealed class McpAccessToken : PublicEntity
{
    public long UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public bool CanWrite { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
