using System.Globalization;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AfterTab.API.Security;
using AfterTab.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AfterTab.API.Mcp;

public sealed class McpAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger, UrlEncoder encoder, McpTokenService tokens, McpOAuthService oauth)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var raw = SessionAuthenticationHandler.ReadToken(Request);
        if (raw is null) return AuthenticateResult.NoResult();
        if (raw.StartsWith("atm_oauth_", StringComparison.Ordinal))
        {
            var oauthToken = await oauth.ValidateAccessTokenAsync(raw, Context.RequestAborted);
            if (oauthToken is null) return AuthenticateResult.Fail("Invalid, expired or revoked AfterTab OAuth access token.");
            var claims = new List<Claim>
            {
                new("aftertab_user_id", oauthToken.UserId.ToString(CultureInfo.InvariantCulture)),
                new("aftertab_mcp_client_id", oauthToken.ClientId)
            };
            claims.AddRange(oauthToken.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(scope => new Claim(McpOptions.ScopeClaim, scope)));
            return AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, McpOptions.AuthenticationScheme)), McpOptions.AuthenticationScheme));
        }
        var token = await tokens.ValidateAsync(raw, Context.RequestAborted);
        if (token is null) return AuthenticateResult.Fail("Invalid, expired or revoked AfterTab integration token.");
        var identity = new ClaimsIdentity([
            new Claim("aftertab_user_id", token.UserId.ToString(CultureInfo.InvariantCulture)),
            new Claim("aftertab_mcp_token_id", token.PublicId.ToString()),
            new Claim(McpOptions.ScopeClaim, McpOptions.ReadScope)
        ], McpOptions.AuthenticationScheme);
        if (token.CanWrite) identity.AddClaim(new Claim(McpOptions.ScopeClaim, McpOptions.WriteScope));
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), McpOptions.AuthenticationScheme));
    }

    protected override Task HandleChallengeAsync(Microsoft.AspNetCore.Authentication.AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = $"Bearer resource_metadata=\"{oauth.Origin}/.well-known/oauth-protected-resource\"";
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
