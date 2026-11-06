namespace AfterTab.DAL.Entities.Users;

// Persist before performing a write. An interrupted operation must never be replayed blindly.
public sealed class McpOperation : BaseEntity
{
    public long UserId { get; set; }
    public Guid OperationId { get; set; }
    public string Tool { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = string.Empty;
    public bool Completed { get; set; }
    public string? ResultJson { get; set; }
}
