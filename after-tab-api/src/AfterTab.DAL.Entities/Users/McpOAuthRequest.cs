namespace AfterTab.DAL.Entities.Users;

public sealed class McpOAuthRequest
{
    public long Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string Resource { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }
}
