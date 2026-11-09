using AfterTab.Common.Security;
namespace AfterTab.API.Security;
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long? UserId
    {
        get
        {
            var principal = accessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true) return null;
            // This claim must be issued by the server after mapping to Users.Id.
            return long.TryParse(principal.FindFirst("aftertab_user_id")?.Value, out var id) ? id : null;
        }
    }
}
