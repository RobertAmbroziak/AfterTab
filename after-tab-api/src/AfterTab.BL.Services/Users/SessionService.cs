using System.Security.Cryptography;
using System.Text;
using AfterTab.BL.DTO.Users;
using AfterTab.BL.Interfaces.Users;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Interfaces.Users;

namespace AfterTab.BL.Services.Users;

public sealed class SessionService(
    ISessionRepository sessions, IUserRepository users, TimeProvider clock, IGuestBrowserRepository devices) : ISessionService
{
    public async Task<GuestSessionDto> CreateGuestAsync(string? existingToken, CancellationToken cancellationToken)
    {
        var existing = await ValidateAsync(existingToken, cancellationToken);
        if (existing is not null)
            return new GuestSessionDto(existingToken!, existing.User, existing.ExpiresAtUtc);

        var now = clock.GetUtcNow();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var user = new User { DisplayName = "Gość", LastSeenAtUtc = now };
        var session = new Session
        {
            UserId = user.Id,
            TokenHash = Hash(token),
            LastUsedAtUtc = now,
            ExpiresAtUtc = now.AddDays(30)
        };
        await sessions.CreateGuestAsync(user, session, cancellationToken);
        return new GuestSessionDto(token, new UserDto(user.Id, user.DisplayName, true, !user.DisplayNameConfirmed), session.ExpiresAtUtc);
    }

    public async Task<SessionProfileDto?> ValidateAsync(string? token, CancellationToken cancellationToken)
    {
        if (!IsToken(token)) return null;
        var session = await sessions.FindByTokenHashAsync(Hash(token!), cancellationToken);
        if (session is null || session.RevokedAtUtc is not null || session.ExpiresAtUtc <= clock.GetUtcNow())
            return null;
        if (session.GuestDeviceId is Guid deviceId)
        {
            var device = await devices.FindDeviceAsync(deviceId, cancellationToken);
            if (device is null || device.RevokedAtUtc is not null) return null;
        }
        var user = await users.GetByIdAsync(session.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active) return null;
        var hasIdentity = await users.HasIdentityAsync(user.Id, cancellationToken);
        return new SessionProfileDto(session.Id, new UserDto(user.Id, user.DisplayName, !hasIdentity, !user.DisplayNameConfirmed), session.ExpiresAtUtc);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        if (!IsToken(token)) return;
        var session = await sessions.FindByTokenHashAsync(Hash(token), cancellationToken);
        if (session is not null && session.RevokedAtUtc is null)
            await sessions.RevokeAsync(session, clock.GetUtcNow(), cancellationToken);
    }

    private static bool IsToken(string? token)
        => token is { Length: 64 } && token.All(Uri.IsHexDigit);

    private static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
