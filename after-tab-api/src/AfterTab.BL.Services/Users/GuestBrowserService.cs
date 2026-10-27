using System.Security.Cryptography;
using System.Text;
using AfterTab.BL.DTO.Users;
using AfterTab.BL.Interfaces.Users;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Interfaces.Users;
using AfterTab.Common.Validation;

namespace AfterTab.BL.Services.Users;

public sealed class GuestBrowserService(IGuestBrowserRepository repository, IUserRepository users,
    ISessionRepository sessions, ISessionService sessionService, TimeProvider clock) : IGuestBrowserService
{
    public async Task<string?> PrepareAsync(GuestBrowserPrepareDto input, string? sessionToken, CancellationToken ct)
    {
        if (!IsSecret(input.Challenge) || !IsSecret(input.State) || input.Action is not ("login" or "logout" or "forget")) return null;
        var profile = await sessionService.ValidateAsync(sessionToken, ct);
        var displayName = input.DisplayName is null ? null : DisplayNames.Normalize(input.DisplayName);
        var secret = Secret();
        repository.AddRequest(new GuestLoginRequest
        {
            RequestHash = Hash(secret), Challenge = input.Challenge, State = input.State,
            ReturnOrigin = input.ReturnOrigin, Action = input.Action,
            ProposedDisplayName = displayName,
            LegacyUserId = profile?.User.IsGuest == true ? profile.User.Id : null,
            LegacySessionId = profile?.User.IsGuest == true ? profile.SessionId : null,
            ExpiresAtUtc = clock.GetUtcNow().AddMinutes(5)
        });
        return await repository.SaveAsync(ct) ? secret : null;
    }

    public async Task<GuestBrowserRedirectDto?> ContinueAsync(string requestSecret, string verifier, string? deviceToken, CancellationToken ct)
    {
        if (!IsSecret(requestSecret) || !IsSecret(verifier)) return null;
        var request = await repository.FindRequestAsync(Hash(requestSecret), ct);
        var now = clock.GetUtcNow();
        if (request is null || request.ExpiresAtUtc <= now || request.CodeHash is not null || request.ConsumedAtUtc is not null) return null;
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(request.Challenge), Encoding.ASCII.GetBytes(Hash(verifier)))) return null;
        var device = IsSecret(deviceToken) ? await repository.FindDeviceAsync(Hash(deviceToken!), ct) : null;
        User? user = null;
        // A valid old session can adopt its existing account instead of creating a new guest.
        if (request.LegacyUserId is Guid legacyUser && request.LegacySessionId is Guid legacySession)
        {
            // Preparation happened at most five minutes ago, but logout/revocation must still be respected.
            var session = await sessions.FindByIdAsync(legacySession, ct);
            if (session is not null && session.RevokedAtUtc is null && session.ExpiresAtUtc > now)
                user = await ActiveGuestAsync(legacyUser, ct);
        }
        if (device is not null && (device.RevokedAtUtc is not null || device.ExpiresAtUtc <= now)) device = null;
        if (device is not null && user is not null && device.UserId != user.Id && request.Action != "forget") device = null;
        if (user is null && device is not null) user = await ActiveGuestAsync(device.UserId, ct);
        if (user is null) device = null;
        string? newDeviceToken = null;
        if (request.Action != "forget")
        {
            if (user is null)
            {
                user = new User { DisplayName = request.ProposedDisplayName ?? "Gość",
                    DisplayNameConfirmed = request.ProposedDisplayName is not null, LastSeenAtUtc = now };
                repository.AddUser(user);
            }
            if (device is null)
            {
                newDeviceToken = Secret();
                device = new GuestDevice { UserId = user.Id, TokenHash = Hash(newDeviceToken), ExpiresAtUtc = now.AddDays(365) };
                repository.AddDevice(device);
            }
            else
            {
                // Extend the first-party cookie without changing its secret during parallel tabs.
                device.ExpiresAtUtc = now.AddDays(365);
                newDeviceToken = deviceToken;
            }
        }
        request.UserId = user?.Id;
        request.GuestDeviceId = device?.Id;
        var code = Secret();
        request.CodeHash = Hash(code);
        request.Version = Guid.NewGuid();
        if (!await repository.SaveAsync(ct)) return null;
        return new(request.ReturnOrigin, request.State, code, newDeviceToken, device?.ExpiresAtUtc);
    }

    public async Task<GuestBrowserResultDto?> ExchangeAsync(GuestBrowserExchangeDto input, CancellationToken ct)
    {
        if (!IsSecret(input.Code) || !IsSecret(input.Verifier)) return null;
        var request = await repository.FindCodeAsync(Hash(input.Code), ct);
        var now = clock.GetUtcNow();
        if (request is null || request.ExpiresAtUtc <= now || request.ConsumedAtUtc is not null
            || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(request.Challenge), Encoding.ASCII.GetBytes(Hash(input.Verifier)))) return null;
        var device = request.GuestDeviceId is Guid deviceId ? await repository.FindDeviceAsync(deviceId, ct) : null;
        if (request.Action != "forget" && (device is null || device.RevokedAtUtc is not null || device.ExpiresAtUtc <= now
            || request.UserId is not Guid id || await ActiveGuestAsync(id, ct) is null)) return null;
        if (request.Action == "forget" && device is not null)
        {
            device.RevokedAtUtc = now;
            await repository.RevokeDeviceSessionsAsync(device.Id, now, ct);
        }
        if (request.LegacySessionId is Guid legacySessionId)
        {
            var legacySession = await sessions.FindByIdAsync(legacySessionId, ct);
            if (legacySession is not null && legacySession.RevokedAtUtc is null) legacySession.RevokedAtUtc = now;
        }
        GuestSessionDto? result = null;
        if (request.Action == "login")
        {
            var token = Secret();
            var session = new Session { UserId = request.UserId!.Value, GuestDeviceId = device!.Id,
                TokenHash = Hash(token), LastUsedAtUtc = now, ExpiresAtUtc = now.AddDays(30) };
            repository.AddSession(session);
            var user = await users.GetByIdAsync(session.UserId, ct);
            result = new(token, new UserDto(user!.Id, user.DisplayName, true, !user.DisplayNameConfirmed), session.ExpiresAtUtc);
        }
        request.ConsumedAtUtc = now;
        request.Version = Guid.NewGuid();
        return await repository.SaveAsync(ct) ? new(result, request.Action != "forget") : null;
    }

    private async Task<User?> ActiveGuestAsync(Guid id, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct);
        return user?.Status == UserStatus.Active && !await users.HasIdentityAsync(id, ct) ? user : null;
    }
    private static bool IsSecret(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
