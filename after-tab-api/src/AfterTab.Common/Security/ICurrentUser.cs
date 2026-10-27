namespace AfterTab.Common.Security;
public interface ICurrentUser
{
    Guid? UserId { get; }
}
