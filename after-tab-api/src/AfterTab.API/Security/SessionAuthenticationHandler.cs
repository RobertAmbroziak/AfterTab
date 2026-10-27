using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AfterTab.BL.Interfaces.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AfterTab.API.Security;

public sealed class SessionAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    ISessionService sessions) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "AfterTabSession";

    public static string? ReadToken(HttpRequest request)
        => AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var header)
            && header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
                ? header.Parameter : null;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken(Request);
        if (token is null) return AuthenticateResult.NoResult();
        var profile = await sessions.ValidateAsync(token, Context.RequestAborted);
        if (profile is null) return AuthenticateResult.Fail("Invalid or expired session.");
        var identity = new ClaimsIdentity([
            new Claim("aftertab_user_id", profile.User.Id.ToString()),
            new Claim("aftertab_session_id", profile.SessionId.ToString()),
            new Claim(ClaimTypes.Name, profile.User.DisplayName)
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
