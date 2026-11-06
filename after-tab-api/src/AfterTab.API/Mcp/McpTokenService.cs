using System.Security.Cryptography;
using System.Text;
using AfterTab.Common.Errors;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace AfterTab.API.Mcp;

public sealed record CreateMcpToken(string Name, bool CanWrite = false, int ExpiresInDays = 30);
public sealed record McpTokenInfo(Guid Id, string Name, bool CanWrite, DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc, DateTimeOffset? RevokedAtUtc);
public sealed record IssuedMcpToken(McpTokenInfo Details, string Token);
public sealed record McpInstructionsDto(string Instructions);

public sealed class McpTokenService(AppDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    public async Task<IReadOnlyList<McpTokenInfo>> ListAsync(CancellationToken ct)
    {
        var userId = await Owner(ct);
        return await db.McpAccessTokens.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => new McpTokenInfo(x.PublicId, x.Name,
                x.CanWrite, x.CreatedAtUtc, x.ExpiresAtUtc, x.RevokedAtUtc)).ToListAsync(ct);
    }

    public async Task<IssuedMcpToken> CreateAsync(CreateMcpToken input, CancellationToken ct)
    {
        var userId = await Owner(ct);
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || input.ExpiresInDays is < 1 or > 90)
            throw new AppException(ErrorKind.Validation, "Podaj nazwę tokena od 1 do 100 znaków i ważność od 1 do 90 dni.");
        var now = clock.GetUtcNow();
        if (await db.McpAccessTokens.CountAsync(x => x.UserId == userId && x.RevokedAtUtc == null && x.ExpiresAtUtc > now, ct) >= 20)
            throw new AppException(ErrorKind.Validation, "Unieważnij istniejący token integracji przed utworzeniem kolejnego (limit: 20).");
        var raw = "atm_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var token = new McpAccessToken { UserId = userId, Name = name, TokenHash = Hash(raw),
            CanWrite = input.CanWrite, ExpiresAtUtc = now.AddDays(input.ExpiresInDays) };
        db.McpAccessTokens.Add(token);
        await db.SaveChangesAsync(ct);
        return new(new(token.PublicId, token.Name, token.CanWrite, token.CreatedAtUtc, token.ExpiresAtUtc, null), raw);
    }

    public async Task RevokeAsync(Guid id, CancellationToken ct)
    {
        var userId = await Owner(ct);
        var token = await db.McpAccessTokens.SingleOrDefaultAsync(x => x.PublicId == id && x.UserId == userId, ct)
            ?? throw new AppException(ErrorKind.NotFound, "Nie znaleziono tokena integracji.");
        if (token.RevokedAtUtc.HasValue) return;
        token.RevokedAtUtc = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
    }

    public async Task<McpInstructionsDto> GetInstructionsAsync(CancellationToken ct)
    {
        var userId = await Owner(ct);
        var instructions = await db.Users.AsNoTracking().Where(x => x.Id == userId)
            .Select(x => x.McpInstructions).SingleAsync(ct);
        return new(instructions);
    }

    public async Task<McpInstructionsDto> UpdateInstructionsAsync(string? instructions, CancellationToken ct)
    {
        var userId = await Owner(ct);
        var normalized = instructions?.Trim() ?? string.Empty;
        if (normalized.Length > 2000)
            throw new AppException(ErrorKind.Validation, "Instrukcje mogą mieć maksymalnie 2000 znaków.");
        var user = await db.Users.SingleAsync(x => x.Id == userId, ct);
        user.McpInstructions = normalized;
        await db.SaveChangesAsync(ct);
        return new(normalized);
    }

    public async Task<McpAccessToken?> ValidateAsync(string raw, CancellationToken ct)
    {
        // Never accept an ordinary API session or a token passed in a URL/cookie.
        if (raw.Length != 68 || !raw.StartsWith("atm_", StringComparison.Ordinal)
            || raw.AsSpan(4).ContainsAnyExcept("0123456789abcdef")) return null;
        var hash = Hash(raw);
        var now = clock.GetUtcNow();
        return await db.McpAccessTokens.AsNoTracking().Where(x => x.TokenHash == hash
            && x.RevokedAtUtc == null && x.ExpiresAtUtc > now
            && db.Users.Any(u => u.Id == x.UserId && u.Status == UserStatus.Active && u.DisplayNameConfirmed))
            .SingleOrDefaultAsync(ct);
    }

    private async Task<long> Owner(CancellationToken ct)
    {
        var id = currentUser.UserId;
        if (id is null || !await db.Users.AnyAsync(x => x.Id == id && x.Status == UserStatus.Active && x.DisplayNameConfirmed, ct))
            throw new AppException(ErrorKind.Forbidden, "Zaloguj się do AfterTab i najpierw potwierdź swoje imię.");
        return id.Value;
    }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
