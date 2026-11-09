namespace AfterTab.DAL.Entities.Users;

public sealed class McpOAuthToken
{
    public long Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string TokenType { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public Guid FamilyId { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
