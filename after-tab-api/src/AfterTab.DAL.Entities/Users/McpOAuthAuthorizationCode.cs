namespace AfterTab.DAL.Entities.Users;

public sealed class McpOAuthAuthorizationCode
{
    public long Id { get; set; }
    public string CodeHash { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string RedirectUri { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}
