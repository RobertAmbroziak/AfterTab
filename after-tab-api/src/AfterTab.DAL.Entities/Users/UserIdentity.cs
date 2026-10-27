using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Users;
public sealed class UserIdentity : BaseEntity
{
    public Guid UserId { get; set; }
    public IdentityProvider Provider { get; set; }
    public string? ProviderSubject { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public DateTimeOffset? EmailVerifiedAtUtc { get; set; }
    public string? PasswordHash { get; set; }
}
