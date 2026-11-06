using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Users;
public sealed class User : PublicEntity
{
    public string DisplayName { get; set; } = string.Empty;
    public bool DisplayNameConfirmed { get; set; }
    public string McpInstructions { get; set; } = string.Empty;
    public UserStatus Status { get; set; } = UserStatus.Active;
    public DateTimeOffset? LastSeenAtUtc { get; set; }
}
