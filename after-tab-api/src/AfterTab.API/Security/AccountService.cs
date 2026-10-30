using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using AfterTab.BL.DTO.Users;
using AfterTab.BL.Interfaces.Users;
using AfterTab.Common.Errors;
using AfterTab.Common.Validation;
using AfterTab.DAL.EF;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AfterTab.API.Security;

public sealed class AccountService(AppDbContext db, ISessionService sessions, IAccountMail mail,
    IConfiguration configuration, TimeProvider clock)
{
    private readonly PasswordHasher<UserIdentity> hasher = new(new Microsoft.Extensions.Options.OptionsWrapper<PasswordHasherOptions>(
        new() { IterationCount = 210000 }));
    public static string Secret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public static string Hash(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
    public static bool IsSecret(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    public static bool Matches(string? challenge, string? verifier) => IsSecret(challenge) && IsSecret(verifier)
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(challenge!), Encoding.ASCII.GetBytes(Hash(verifier!)));
    private DateTimeOffset Now => clock.GetUtcNow();
    private static AppException Invalid(string message = "Nieprawidłowe lub wygasłe dane. Spróbuj ponownie.") => new(ErrorKind.Validation, message);
    private static string Email(string value)
    {
        var email = value.Trim();
        if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
            throw Invalid("Podaj prawidłowy adres e-mail.");
        return email.ToUpperInvariant();
    }
    private static void Password(string value)
    {
        if (value.Length < 12 || value.Length > 128) throw Invalid("Hasło musi mieć od 12 do 128 znaków.");
    }
    public string Frontend => configuration["Authentication:FrontendOrigin"]
        ?? throw new InvalidOperationException("Missing Authentication:FrontendOrigin");

    public async Task RegisterAsync(string email, string password, string name, string? currentToken, CancellationToken ct)
    {
        var normalized = Email(email); Password(password);
        var displayName = DisplayNames.Normalize(name);
        var identity = await db.UserIdentities.SingleOrDefaultAsync(x => x.Provider == IdentityProvider.Local && x.NormalizedEmail == normalized, ct);
        // Existing accounts receive the same public response; never replace an existing password.
        if (identity is not null) return;
        var profile = await sessions.ValidateAsync(currentToken, ct);
        User user;
        if (profile?.User.IsGuest == true) user = await db.Users.SingleAsync(x => x.Id == profile.User.Id, ct);
        else { user = new User { DisplayName = displayName, DisplayNameConfirmed = true }; db.Users.Add(user); }
        identity = new UserIdentity { UserId = user.Id, Provider = IdentityProvider.Local, Email = email.Trim(), NormalizedEmail = normalized };
        identity.PasswordHash = hasher.HashPassword(identity, password);
        db.UserIdentities.Add(identity);
        var secret = AddAction(user.Id, "confirm", TimeSpan.FromHours(24));
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); if (await db.UserIdentities.AnyAsync(x => x.Provider == IdentityProvider.Local && x.NormalizedEmail == normalized, ct)) return; throw; }
        await mail.SendAsync(identity.Email, "Potwierdź adres e-mail", Frontend + "/#account-confirm=" + secret, ct);
    }

    public async Task SendAsync(string email, bool reset, CancellationToken ct)
    {
        var normalized = Email(email);
        var identity = await db.UserIdentities.SingleOrDefaultAsync(x => x.Provider == IdentityProvider.Local && x.NormalizedEmail == normalized, ct);
        if (identity is null || (reset && identity.EmailVerifiedAtUtc is null) || (!reset && identity.EmailVerifiedAtUtc is not null)) return;
        var secret = AddAction(identity.UserId, reset ? "reset" : "confirm", TimeSpan.FromMinutes(reset ? 30 : 1440));
        await db.SaveChangesAsync(ct);
        await mail.SendAsync(identity.Email!, reset ? "Ustaw nowe hasło" : "Potwierdź adres e-mail",
            Frontend + (reset ? "/#account-reset=" : "/#account-confirm=") + secret, ct);
    }
    public async Task CompleteEmailAsync(string token, string? password, CancellationToken ct)
    {
        var action = await FindAsync(token, password is null ? "confirm" : "reset", ct) ?? throw Invalid();
        var identity = await db.UserIdentities.SingleAsync(x => x.UserId == action.UserId && x.Provider == IdentityProvider.Local, ct);
        if (password is null) identity.EmailVerifiedAtUtc = Now;
        else
        {
            Password(password);
            identity.PasswordHash = hasher.HashPassword(identity, password);
            identity.FailedLoginCount = 0; identity.LockedUntilUtc = null;
            var active = await db.Sessions.Where(x => x.UserId == identity.UserId && x.RevokedAtUtc == null).ToListAsync(ct);
            foreach (var session in active) session.RevokedAtUtc = Now;
        }
        // Invalidate all outstanding tokens of this kind, not only the submitted link.
        var siblings = await db.AccountActions.Where(x => x.UserId == identity.UserId && x.ConsumedAtUtc == null
            && (x.Purpose == action.Purpose || (password != null && (x.Purpose == "flow-login" || x.Purpose == "exchange" || x.Purpose == "exchange-remember" || x.Purpose == "remember")))).ToListAsync(ct);
        foreach (var sibling in siblings) Consume(sibling);
        await SaveOnceAsync(ct);
    }
    public async Task<Guid> LoginAsync(string email, string password, CancellationToken ct)
    {
        var normalized = Email(email);
        if (password.Length > 128) throw Invalid("Nieprawidłowy e-mail lub hasło.");
        var identity = await db.UserIdentities.SingleOrDefaultAsync(x => x.Provider == IdentityProvider.Local && x.NormalizedEmail == normalized, ct);
        // Always perform the expensive hash check, including unknown accounts.
        var candidate = identity ?? new UserIdentity();
        var hash = candidate.PasswordHash ?? DummyPasswordHash.Value;
        var result = hasher.VerifyHashedPassword(candidate, hash, password);
        if (identity is null || identity.LockedUntilUtc > Now) throw Invalid("Nieprawidłowy e-mail lub hasło. Po wielu próbach odczekaj 15 minut.");
        if (result == PasswordVerificationResult.Failed)
        {
            var lockedUntil = Now.AddMinutes(15);
            // Increment in SQL so parallel failed attempts cannot overwrite the counter.
            await db.UserIdentities.Where(x => x.Id == identity.Id).ExecuteUpdateAsync(set => set
                .SetProperty(x => x.LockedUntilUtc, x => x.FailedLoginCount >= 4 ? lockedUntil : x.LockedUntilUtc)
                .SetProperty(x => x.FailedLoginCount, x => x.FailedLoginCount >= 4 ? 0 : x.FailedLoginCount + 1), ct);
            await db.Entry(identity).ReloadAsync(ct);
            throw Invalid("Nieprawidłowy e-mail lub hasło. Po wielu próbach odczekaj 15 minut.");
        }
        if (identity.EmailVerifiedAtUtc is null) throw Invalid("Potwierdź najpierw adres e-mail. Możesz ponownie wysłać wiadomość potwierdzającą.");
        if (!await db.Users.AnyAsync(x => x.Id == identity.UserId && x.Status == UserStatus.Active, ct)) throw Invalid();
        identity.FailedLoginCount = 0; identity.LockedUntilUtc = null;
        if (result == PasswordVerificationResult.SuccessRehashNeeded) identity.PasswordHash = hasher.HashPassword(identity, password);
        await db.SaveChangesAsync(ct);
        return identity.UserId;
    }
    private static readonly Lazy<string> DummyPasswordHash = new(() => new PasswordHasher<UserIdentity>(
        new Microsoft.Extensions.Options.OptionsWrapper<PasswordHasherOptions>(new() { IterationCount = 210000 }))
        .HashPassword(new(), "unusable-dummy-password"));

    public string AddAction(Guid? userId, string purpose, TimeSpan lifetime, string? challenge = null, string? state = null, string? origin = null)
    {
        var token = Secret();
        db.AccountActions.Add(new AccountAction { UserId = userId, Purpose = purpose, TokenHash = Hash(token), ExpiresAtUtc = Now.Add(lifetime),
            Challenge = challenge, State = state, ReturnOrigin = origin });
        return token;
    }
    public async Task<AccountAction?> FindAsync(string? token, string purpose, CancellationToken ct)
    {
        if (!IsSecret(token)) return null;
        var hash = Hash(token!);
        var action = await db.AccountActions.SingleOrDefaultAsync(x => x.TokenHash == hash && x.Purpose == purpose && x.ConsumedAtUtc == null, ct);
        return action?.ExpiresAtUtc > Now ? action : null;
    }
    public void Consume(AccountAction action) { action.ConsumedAtUtc = Now; action.Version = Guid.NewGuid(); }
    public async Task SaveOnceAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); throw Invalid(); }
    }
    public async Task<Guid> ExternalAsync(IdentityProvider provider, string subject, string? name, string? email, Guid? guestId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255) throw Invalid();
        var identity = await db.UserIdentities.SingleOrDefaultAsync(x => x.Provider == provider && x.ProviderSubject == subject, ct);
        if (identity is not null)
        {
            if (!await db.Users.AnyAsync(x => x.Id == identity.UserId && x.Status == UserStatus.Active, ct)) throw Invalid();
            // Existing external identity always wins. Never merge on email or overwrite ownership.
            return identity.UserId;
        }
        var user = guestId.HasValue && !await db.UserIdentities.AnyAsync(x => x.UserId == guestId, ct)
            ? await db.Users.SingleOrDefaultAsync(x => x.Id == guestId && x.Status == UserStatus.Active, ct) : null;
        if (user is null)
        {
            user = new User { DisplayName = string.IsNullOrWhiteSpace(name) ? "Użytkownik" : name[..Math.Min(100, name.Length)], DisplayNameConfirmed = false };
            db.Users.Add(user);
        }
        db.UserIdentities.Add(new UserIdentity { UserId = user.Id, Provider = provider, ProviderSubject = subject, Email = email });
        await db.SaveChangesAsync(ct);
        return user.Id;
    }
    public async Task<GuestSessionDto> IssueSessionAsync(Guid userId, CancellationToken ct, bool persistentCookie = false)
    {
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == userId && x.Status == UserStatus.Active, ct) ?? throw Invalid();
        var token = Secret();
        var session = new Session { UserId = userId, TokenHash = Hash(token), ExpiresAtUtc = Now.AddDays(persistentCookie ? 365 : 30), LastUsedAtUtc = Now };
        db.Sessions.Add(session);
        return new(token, new(user.Id, user.DisplayName, false, !user.DisplayNameConfirmed), session.ExpiresAtUtc);
    }
}
