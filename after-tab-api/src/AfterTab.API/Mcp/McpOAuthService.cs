using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AfterTab.DAL.EF;
using AfterTab.DAL.Entities.Users;
using AfterTab.Mcp;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AfterTab.API.Mcp;

public sealed record McpOAuthClientRegistration(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("client_name")] string ClientName,
    [property: JsonPropertyName("redirect_uris")] string[] RedirectUris,
    [property: JsonPropertyName("token_endpoint_auth_method")] string TokenEndpointAuthMethod,
    [property: JsonPropertyName("grant_types")] string[] GrantTypes,
    [property: JsonPropertyName("response_types")] string[] ResponseTypes);
public sealed record McpOAuthAuthorizationRequest(Guid RequestId, string ClientName, string[] Scopes);
public sealed record McpOAuthConnectedClient(string ClientId, string ClientName);
public sealed record McpOAuthApproveRequest(Guid RequestId, bool Approve);
public sealed record McpOAuthRedirect([property: JsonPropertyName("redirectUrl")] string RedirectUrl);
public sealed record McpOAuthTokenResponse(string AccessToken, string TokenType, int ExpiresIn,
    string RefreshToken, string Scope);

public sealed class McpOAuthService(AppDbContext db, IHttpContextAccessor http,
    IOptions<McpOptions> options, IConfiguration configuration, TimeProvider clock)
{
    private static readonly HashSet<string> SupportedScopes = [McpOptions.ReadScope, McpOptions.WriteScope, "offline_access"];
    private const string AccessType = "access";
    private const string RefreshType = "refresh";

    public string Origin
    {
        get
        {
            var configured = options.Value.PublicOrigin?.Trim().TrimEnd('/');
            if (!string.IsNullOrEmpty(configured))
            {
                if (!Uri.TryCreate(configured, UriKind.Absolute, out var configuredUri)
                    || configuredUri.AbsolutePath != "/" || !string.IsNullOrEmpty(configuredUri.Query)
                    || !string.IsNullOrEmpty(configuredUri.Fragment)
                    || (configuredUri.Scheme != Uri.UriSchemeHttps && !IsLoopback(configuredUri.Host)))
                    throw new InvalidOperationException("Mcp:PublicOrigin must be an HTTPS origin (HTTP is allowed only for localhost). ");
                return configured;
            }

            var request = http.HttpContext?.Request ?? throw new InvalidOperationException("The MCP public origin is not configured.");
            var origin = $"{request.Scheme}://{request.Host}";
            if (request.Scheme != Uri.UriSchemeHttps && !IsLoopback(request.Host.Host))
                throw new InvalidOperationException("Set Mcp:PublicOrigin to the public HTTPS API origin.");
            return origin;
        }
    }

    public string Resource => Origin + "/mcp";
    public string FrontendOrigin => (configuration["Authentication:FrontendOrigin"]
        ?? throw new InvalidOperationException("Authentication:FrontendOrigin is required for MCP OAuth consent.")).TrimEnd('/');

    public async Task<McpOAuthClientRegistration> RegisterAsync(JsonElement input, CancellationToken ct)
    {
        if (!input.TryGetProperty("redirect_uris", out var redirects) || redirects.ValueKind != JsonValueKind.Array)
            throw new McpOAuthException("invalid_client_metadata", "redirect_uris must be an array.");
        var uris = redirects.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String)
            .Select(x => x.GetString()!).Distinct(StringComparer.Ordinal).ToArray();
        if (uris.Length is < 1 or > 10 || uris.Any(x => !IsAllowedRedirectUri(x)))
            throw new McpOAuthException("invalid_redirect_uri", "Each redirect URI must be HTTPS or a loopback HTTP URI.");

        var method = String(input, "token_endpoint_auth_method") ?? "none";
        if (method != "none") throw new McpOAuthException("invalid_client_metadata", "Only public PKCE clients are supported.");
        var grants = Strings(input, "grant_types", ["authorization_code", "refresh_token"]);
        var responses = Strings(input, "response_types", ["code"]);
        if (!grants.Contains("authorization_code") || !responses.Contains("code")
            || grants.Any(x => x is not ("authorization_code" or "refresh_token")))
            throw new McpOAuthException("invalid_client_metadata", "Only authorization_code and refresh_token grants are supported.");

        var name = String(input, "client_name")?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            name = "MCP client";
        var client = new McpOAuthClient
        {
            ClientId = "atc_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)),
            ClientName = name,
            RedirectUrisJson = JsonSerializer.Serialize(uris),
            CreatedAtUtc = clock.GetUtcNow()
        };
        db.McpOAuthClients.Add(client);
        await db.SaveChangesAsync(ct);
        return new(client.ClientId, client.ClientName, uris, "none", ["authorization_code", "refresh_token"], ["code"]);
    }

    public async Task<(Guid RequestId, string RedirectUri, string State)?> BeginAuthorizationAsync(
        string? responseType, string? clientId, string? redirectUri, string? state,
        string? challenge, string? challengeMethod, string? scope, string? resource, CancellationToken ct)
    {
        if (responseType != "code" || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(redirectUri) || string.IsNullOrWhiteSpace(state)
            || state.Length > 512 || string.IsNullOrWhiteSpace(challenge)
            || challenge.Length is < 43 or > 128 || challengeMethod != "S256") return null;
        var client = await db.McpOAuthClients.AsNoTracking().SingleOrDefaultAsync(x => x.ClientId == clientId, ct);
        if (client is null || !RedirectUris(client).Contains(redirectUri, StringComparer.Ordinal)) return null;
        if (!string.IsNullOrEmpty(resource) && resource != Resource) return null;
        var scopes = ParseScopes(scope, defaultToRead: true);
        if (scopes is null) return null;
        if (!scopes.Contains("offline_access", StringComparer.Ordinal)) scopes = [.. scopes, "offline_access"];
        var request = new McpOAuthRequest
        {
            ClientId = clientId,
            RedirectUri = redirectUri,
            State = state,
            CodeChallenge = challenge,
            Scope = string.Join(' ', scopes),
            Resource = Resource,
            ExpiresAtUtc = clock.GetUtcNow().AddMinutes(5)
        };
        db.McpOAuthRequests.Add(request);
        await db.SaveChangesAsync(ct);
        return (request.PublicId, redirectUri, state);
    }

    public async Task<McpOAuthAuthorizationRequest?> GetAuthorizationRequestAsync(Guid id, CancellationToken ct)
    {
        var request = await db.McpOAuthRequests.AsNoTracking().SingleOrDefaultAsync(x => x.PublicId == id
            && x.ExpiresAtUtc > clock.GetUtcNow(), ct);
        if (request is null) return null;
        var clientName = await db.McpOAuthClients.AsNoTracking().Where(x => x.ClientId == request.ClientId)
            .Select(x => x.ClientName).SingleOrDefaultAsync(ct);
        return clientName is null ? null : new(id, clientName,
            request.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(x => x != "offline_access").ToArray());
    }

    public async Task<McpOAuthRedirect?> CompleteAuthorizationAsync(Guid requestId, long? userId, bool approve, CancellationToken ct)
        => await InTransactionAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var request = await db.McpOAuthRequests.SingleOrDefaultAsync(x => x.PublicId == requestId && x.ExpiresAtUtc > now, ct);
            if (request is null) return null;
            var deleted = await db.McpOAuthRequests.Where(x => x.Id == request.Id)
                .ExecuteDeleteAsync(ct);
            if (deleted != 1) return null;
            string redirect;
            if (!approve)
            {
                redirect = AddQuery(request.RedirectUri, ("error", "access_denied"), ("state", request.State));
            }
            else
            {
                if (userId is null || !await db.Users.AnyAsync(x => x.Id == userId && x.Status == DAL.Entities.Enums.UserStatus.Active
                    && x.DisplayNameConfirmed, ct)) return null;
                var rawCode = "atc_code_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
                db.McpOAuthAuthorizationCodes.Add(new()
                {
                    CodeHash = Hash(rawCode), ClientId = request.ClientId, UserId = userId.Value,
                    RedirectUri = request.RedirectUri, CodeChallenge = request.CodeChallenge,
                    Scope = request.Scope, Resource = request.Resource,
                    ExpiresAtUtc = now.AddMinutes(2)
                });
                await db.SaveChangesAsync(ct);
                redirect = AddQuery(request.RedirectUri, ("code", rawCode), ("state", request.State));
            }
            return new McpOAuthRedirect(redirect);
        }, ct);

    public async Task<McpOAuthTokenResponse?> ExchangeCodeAsync(string? clientId, string? code, string? redirectUri,
        string? verifier, string? resource, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(redirectUri) || string.IsNullOrWhiteSpace(verifier)
            || verifier.Length is < 43 or > 128 || string.IsNullOrWhiteSpace(resource) || resource != Resource) return null;
        return await InTransactionAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var hash = Hash(code);
            var grant = await db.McpOAuthAuthorizationCodes.SingleOrDefaultAsync(x => x.CodeHash == hash
                && x.ClientId == clientId && x.RedirectUri == redirectUri && x.Resource == resource
                && x.ExpiresAtUtc > now && x.ConsumedAtUtc == null, ct);
            if (grant is null || !FixedEquals(grant.CodeChallenge, Challenge(verifier))) return null;
            var consumed = await db.McpOAuthAuthorizationCodes.Where(x => x.Id == grant.Id && x.ConsumedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.ConsumedAtUtc, now), ct);
            if (consumed != 1) return null;
            return await IssueTokensAsync(grant.UserId, grant.ClientId, grant.Scope, grant.Resource, Guid.NewGuid(), now, ct);
        }, ct);
    }

    public async Task<McpOAuthTokenResponse?> RefreshAsync(string? clientId, string? rawToken, string? resource, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(rawToken) || resource != Resource) return null;
        return await InTransactionAsync(async () =>
        {
            var now = clock.GetUtcNow();
            var hash = Hash(rawToken);
            var token = await db.McpOAuthTokens.SingleOrDefaultAsync(x => x.TokenHash == hash && x.TokenType == RefreshType
                && x.ClientId == clientId && x.Resource == resource, ct);
            if (token is null) return null;
            if (token.RevokedAtUtc is not null)
            {
                await db.McpOAuthTokens.Where(x => x.FamilyId == token.FamilyId && x.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAtUtc, now), ct);
                return null;
            }
            if (token.ExpiresAtUtc <= now || !await ActiveUser(token.UserId, ct)) return null;
            var changed = await db.McpOAuthTokens.Where(x => x.Id == token.Id && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAtUtc, now), ct);
            if (changed != 1) return null;
            return await IssueTokensAsync(token.UserId, token.ClientId, token.Scope, token.Resource, token.FamilyId, now, ct);
        }, ct);
    }

    public async Task RevokeAsync(string? rawToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;
        var now = clock.GetUtcNow();
        var hash = Hash(rawToken);
        var token = await db.McpOAuthTokens.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (token is null) return;
        await db.McpOAuthTokens.Where(x => token.TokenType == RefreshType
                ? x.FamilyId == token.FamilyId && x.RevokedAtUtc == null
                : x.Id == token.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAtUtc, now), ct);
    }

    public async Task<IReadOnlyList<McpOAuthConnectedClient>> ListConnectedClientsAsync(long userId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var active = await (from token in db.McpOAuthTokens.AsNoTracking()
            join client in db.McpOAuthClients.AsNoTracking() on token.ClientId equals client.ClientId
            where token.UserId == userId && token.TokenType == RefreshType
                && token.RevokedAtUtc == null && token.ExpiresAtUtc > now
            select new { token.ClientId, client.ClientName })
            .ToListAsync(ct);
        return active.GroupBy(x => new { x.ClientId, x.ClientName })
            .Select(group => new McpOAuthConnectedClient(group.Key.ClientId, group.Key.ClientName))
            .OrderBy(x => x.ClientName, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public Task RevokeClientAsync(long userId, string clientId, CancellationToken ct)
        => db.McpOAuthTokens.Where(x => x.UserId == userId && x.ClientId == clientId && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAtUtc, clock.GetUtcNow()), ct);

    public async Task<McpOAuthToken?> ValidateAccessTokenAsync(string rawToken, CancellationToken ct)
    {
        if (rawToken.Length is < 40 or > 200) return null;
        var hash = Hash(rawToken);
        var now = clock.GetUtcNow();
        return await db.McpOAuthTokens.AsNoTracking().Where(x => x.TokenHash == hash && x.TokenType == AccessType
            && x.Resource == Resource && x.RevokedAtUtc == null && x.ExpiresAtUtc > now
            && db.Users.Any(u => u.Id == x.UserId && u.Status == DAL.Entities.Enums.UserStatus.Active && u.DisplayNameConfirmed))
            .SingleOrDefaultAsync(ct);
    }

    public async Task<McpOAuthTokenResponse> IssueTokensAsync(long userId, string clientId, string scope,
        string resource, Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        var scopes = scope.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var access = "atm_oauth_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var refresh = "atr_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(48));
        db.McpOAuthTokens.AddRange(
            new McpOAuthToken { TokenHash = Hash(access), TokenType = AccessType, ClientId = clientId, UserId = userId,
                Scope = string.Join(' ', scopes.Where(x => x != "offline_access")), Resource = resource,
                FamilyId = familyId, ExpiresAtUtc = now.AddMinutes(10) },
            new McpOAuthToken { TokenHash = Hash(refresh), TokenType = RefreshType, ClientId = clientId, UserId = userId,
                Scope = scope, Resource = resource, FamilyId = familyId, ExpiresAtUtc = now.AddDays(30) });
        await db.SaveChangesAsync(ct);
        return new(access, "Bearer", 600, refresh, string.Join(' ', scopes));
    }

    private Task<bool> ActiveUser(long id, CancellationToken ct)
        => db.Users.AnyAsync(x => x.Id == id && x.Status == DAL.Entities.Enums.UserStatus.Active && x.DisplayNameConfirmed, ct);

    private Task<T?> InTransactionAsync<T>(Func<Task<T?>> operation, CancellationToken ct)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A retry must start with fresh tracked state and a new transaction.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        });

    private static string[]? ParseScopes(string? value, bool defaultToRead)
    {
        var requested = (value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
        if (requested.Length == 0 && defaultToRead) requested = [McpOptions.ReadScope];
        if (requested.Any(x => !SupportedScopes.Contains(x))) return null;
        if (requested.Contains(McpOptions.WriteScope) && !requested.Contains(McpOptions.ReadScope)) return null;
        return requested;
    }

    private static string[] Strings(JsonElement input, string property, string[] fallback)
        => input.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).Distinct().ToArray()
            : fallback;
    private static string? String(JsonElement input, string property)
        => input.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static string[] RedirectUris(McpOAuthClient client)
        => JsonSerializer.Deserialize<string[]>(client.RedirectUrisJson) ?? [];
    private static bool IsAllowedRedirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo)) return false;
        return uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && IsLoopback(uri.Host));
    }
    private static bool IsLoopback(string host)
        => host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || System.Net.IPAddress.TryParse(host, out var ip) && System.Net.IPAddress.IsLoopback(ip);
    private static string AddQuery(string uri, params (string Key, string Value)[] values)
    {
        var separator = uri.Contains('?') ? '&' : '?';
        return uri + separator + string.Join('&', values.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
    }
    private static string Challenge(string verifier)
        => WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static bool FixedEquals(string left, string right)
        => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
}

public sealed class McpOAuthException(string error, string description) : Exception(description)
{
    public string Error { get; } = error;
}
