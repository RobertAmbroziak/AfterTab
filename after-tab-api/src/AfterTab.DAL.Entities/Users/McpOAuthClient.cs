namespace AfterTab.DAL.Entities.Users;

public sealed class McpOAuthClient
{
    public long Id { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string RedirectUrisJson { get; set; } = "[]";
    public DateTimeOffset CreatedAtUtc { get; set; }
}
