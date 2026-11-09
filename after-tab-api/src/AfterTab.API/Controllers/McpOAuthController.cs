using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using AfterTab.API.Mcp;
using AfterTab.API.Security;
using AfterTab.Common.Security;
using AfterTab.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace AfterTab.API.Controllers;

[ApiController, AllowAnonymous, Route(""), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class McpOAuthController(McpOAuthService oauth, IOptions<McpOptions> options,
    ICurrentUser currentUser) : ControllerBase
{
    public sealed class TokenRequest
    {
        [FromForm(Name = "grant_type")] public string GrantType { get; init; } = string.Empty;
        [FromForm(Name = "client_id")] public string? ClientId { get; init; }
        [FromForm(Name = "code")] public string? Code { get; init; }
        [FromForm(Name = "redirect_uri")] public string? RedirectUri { get; init; }
        [FromForm(Name = "code_verifier")] public string? CodeVerifier { get; init; }
        [FromForm(Name = "refresh_token")] public string? RefreshToken { get; init; }
        [FromForm(Name = "resource")] public string? Resource { get; init; }
    }
    public sealed class RevocationRequest
    {
        [FromForm(Name = "token")] public string Token { get; init; } = string.Empty;
        [FromForm(Name = "token_type_hint")] public string? TokenTypeHint { get; init; }
    }

    private bool Enabled => options.Value.Enabled;

    [HttpGet(".well-known/oauth-authorization-server")]
    public IActionResult AuthorizationMetadata()
    {
        if (!Enabled) return NotFound();
        var origin = oauth.Origin;
        return Ok(new
        {
            issuer = origin,
            authorization_endpoint = origin + "/oauth/authorize",
            token_endpoint = origin + "/oauth/token",
            registration_endpoint = origin + "/oauth/register",
            revocation_endpoint = origin + "/oauth/revoke",
            response_types_supported = new[] { "code" },
            response_modes_supported = new[] { "query" },
            grant_types_supported = new[] { "authorization_code", "refresh_token" },
            token_endpoint_auth_methods_supported = new[] { "none" },
            code_challenge_methods_supported = new[] { "S256" },
            client_id_metadata_document_supported = false,
            scopes_supported = new[] { McpOptions.ReadScope, McpOptions.WriteScope, "offline_access" }
        });
    }

    [HttpGet(".well-known/oauth-protected-resource")]
    public IActionResult ProtectedResourceMetadata()
    {
        if (!Enabled) return NotFound();
        return Ok(new
        {
            resource = oauth.Resource,
            authorization_servers = new[] { oauth.Origin },
            scopes_supported = new[] { McpOptions.ReadScope, McpOptions.WriteScope },
            resource_documentation = oauth.FrontendOrigin
        });
    }

    [HttpPost("oauth/register")]
    [EnableRateLimiting("mcp-oauth-register")]
    public async Task<IActionResult> Register([FromBody] JsonElement input, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        try { return Created("/oauth/register", await oauth.RegisterAsync(input, ct)); }
        catch (McpOAuthException ex) { return OAuthError(ex.Error, ex.Message, StatusCodes.Status400BadRequest); }
    }

    [HttpGet("oauth/authorize")]
    public async Task<IActionResult> Authorize([FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "client_id")] string? clientId, [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery] string? state, [FromQuery(Name = "code_challenge")] string? challenge,
        [FromQuery(Name = "code_challenge_method")] string? challengeMethod,
        [FromQuery] string? scope, [FromQuery] string? resource, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var request = await oauth.BeginAuthorizationAsync(responseType, clientId, redirectUri, state,
            challenge, challengeMethod, scope, resource, ct);
        if (request is null) return OAuthError("invalid_request", "Invalid OAuth authorization request.", StatusCodes.Status400BadRequest);
        return Redirect(oauth.FrontendOrigin + "/#mcp-oauth=" + request.Value.RequestId.ToString("D"));
    }

    [HttpGet("api/mcp/oauth/requests/{id:guid}")]
    public async Task<IActionResult> GetRequest(Guid id, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var request = await oauth.GetAuthorizationRequestAsync(id, ct);
        return request is null ? NotFound() : Ok(request);
    }

    [HttpGet("api/mcp/oauth/clients")]
    public async Task<IActionResult> ConnectedClients(CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await oauth.ListConnectedClientsAsync(userId, ct));
    }

    [HttpDelete("api/mcp/oauth/clients/{clientId}")]
    public async Task<IActionResult> RevokeClient(string clientId, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        if (currentUser.UserId is not { } userId) return Unauthorized();
        await oauth.RevokeClientAsync(userId, clientId, ct);
        return NoContent();
    }

    [HttpPost("api/mcp/oauth/consent")]
    public async Task<IActionResult> Consent([FromBody] McpOAuthApproveRequest input, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        long? userId = null;
        if (input.Approve)
        {
            var authentication = await HttpContext.AuthenticateAsync(SessionAuthenticationHandler.SchemeName);
            if (!authentication.Succeeded) return Unauthorized();
            var raw = authentication.Principal?.FindFirstValue("aftertab_user_id");
            if (!long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)) return Unauthorized();
            userId = parsed;
        }
        var result = await oauth.CompleteAuthorizationAsync(input.RequestId, userId, input.Approve, ct);
        return result is null ? BadRequest(new { error = "invalid_request", error_description = "The authorization request expired or is no longer valid." })
            : Ok(result);
    }

    [HttpPost("oauth/token")]
    public async Task<IActionResult> Token([FromForm] TokenRequest input, CancellationToken ct)
    {
        if (!Enabled) return NotFound();
        var token = input.GrantType switch
        {
            "authorization_code" => await oauth.ExchangeCodeAsync(input.ClientId, input.Code, input.RedirectUri,
                input.CodeVerifier, input.Resource, ct),
            "refresh_token" => await oauth.RefreshAsync(input.ClientId, input.RefreshToken, input.Resource, ct),
            _ => null
        };
        if (token is null) return OAuthError("invalid_grant", "The authorization grant is invalid, expired, or already used.", StatusCodes.Status400BadRequest);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.Pragma = "no-cache";
        return Ok(new { access_token = token.AccessToken, token_type = token.TokenType,
            expires_in = token.ExpiresIn, refresh_token = token.RefreshToken, scope = token.Scope });
    }

    [HttpPost("oauth/revoke")]
    public async Task<IActionResult> Revoke([FromForm] RevocationRequest input, CancellationToken ct)
    {
        if (Enabled) await oauth.RevokeAsync(input.Token, ct);
        return Ok();
    }

    private IActionResult OAuthError(string error, string description, int status)
        => StatusCode(status, new { error, error_description = description });
}
