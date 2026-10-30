using AfterTab.API.Security;
using AfterTab.BL.Services.Users;
using AfterTab.Common.Errors;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.EF.Repositories;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class AccountTests
{
    private sealed class Actor : ICurrentUser { public Guid? UserId => null; }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Mail : IAccountMail
    {
        public List<string> Links { get; } = [];
        public Task SendAsync(string email, string subject, string link, CancellationToken ct) { Links.Add(link); return Task.CompletedTask; }
        public string Token => Links[^1].Split('=')[1];
    }
    private sealed class Scope : IAsyncDisposable
    {
        public SqliteConnection Connection { get; } = new("Data Source=:memory:");
        public Clock Clock { get; } = new();
        public Mail Mail { get; } = new();
        public AppDbContext Db { get; private set; } = null!;
        public AccountService Accounts { get; private set; } = null!;
        public SessionService Sessions { get; private set; } = null!;
        public static async Task<Scope> Create()
        {
            var s = new Scope(); await s.Connection.OpenAsync();
            s.Db = new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(s.Connection)
                .ReplaceService<IModelCustomizer, SqliteUtcModelCustomizer>().Options, new Actor(), s.Clock);
            await s.Db.Database.EnsureCreatedAsync();
            s.Sessions = new(new SessionRepository(s.Db), new UserRepository(s.Db), s.Clock, new GuestBrowserRepository(s.Db));
            s.Accounts = new(s.Db, s.Sessions, s.Mail, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Authentication:FrontendOrigin"] = "http://localhost:5173" }).Build(), s.Clock);
            return s;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
    private const string Password = "Test-password-12345";

    [TestMethod]
    public async Task Registration_Hashes_Password_Requires_Confirmation_And_Does_Not_Expose_Existing_Account()
    {
        await using var s = await Scope.Create();
        await s.Accounts.RegisterAsync("Test@example.com", Password, "Test", null, default);
        var identity = await s.Db.UserIdentities.SingleAsync();
        Assert.AreNotEqual(Password, identity.PasswordHash);
        Assert.AreEqual("TEST@EXAMPLE.COM", identity.NormalizedEmail);
        Assert.AreNotEqual(s.Mail.Token, (await s.Db.AccountActions.SingleAsync()).TokenHash);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Accounts.LoginAsync("test@example.com", Password, default));
        var token = s.Mail.Token;
        await s.Accounts.CompleteEmailAsync(token, null, default);
        Assert.AreEqual(identity.UserId, await s.Accounts.LoginAsync("test@example.com", Password, default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Accounts.CompleteEmailAsync(token, null, default));
        await s.Accounts.RegisterAsync("test@example.com", "Different-password-123", "Other", null, default);
        Assert.AreEqual(1, await s.Db.Users.CountAsync());
        Assert.AreEqual(1, s.Mail.Links.Count);
        Assert.AreEqual(identity.UserId, await s.Accounts.LoginAsync("test@example.com", Password, default));
    }
    [TestMethod]
    public async Task Reset_Is_One_Use_Invalidates_Sessions_And_Older_Reset_Links()
    {
        await using var s = await Scope.Create();
        await s.Accounts.RegisterAsync("test@example.com", Password, "Test", null, default);
        await s.Accounts.CompleteEmailAsync(s.Mail.Token, null, default);
        var id = await s.Accounts.LoginAsync("test@example.com", Password, default);
        var session = await s.Accounts.IssueSessionAsync(id, default); await s.Db.SaveChangesAsync();
        Assert.IsNotNull(await s.Sessions.ValidateAsync(session.Token, default));
        await s.Accounts.SendAsync("test@example.com", true, default); var first = s.Mail.Token;
        await s.Accounts.SendAsync("test@example.com", true, default); var second = s.Mail.Token;
        await s.Accounts.CompleteEmailAsync(second, "Changed-password-987", default);
        Assert.IsNull(await s.Sessions.ValidateAsync(session.Token, default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Accounts.CompleteEmailAsync(first, Password, default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Accounts.LoginAsync("test@example.com", Password, default));
        Assert.AreEqual(id, await s.Accounts.LoginAsync("test@example.com", "Changed-password-987", default));
    }
    [TestMethod]
    public async Task Expired_Confirmation_Reset_And_Bridge_Are_Rejected()
    {
        await using var s = await Scope.Create();
        await s.Accounts.RegisterAsync("test@example.com", Password, "Test", null, default);
        var token = s.Mail.Token;
        s.Clock.Now = s.Clock.Now.AddHours(25);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Accounts.CompleteEmailAsync(token, null, default));
        var bridge = s.Accounts.AddAction(null, "exchange", TimeSpan.FromMinutes(1)); await s.Db.SaveChangesAsync();
        s.Clock.Now = s.Clock.Now.AddMinutes(2);
        Assert.IsNull(await s.Accounts.FindAsync(bridge, "exchange", default));
        await s.Accounts.SendAsync("test@example.com", false, default);
        await s.Accounts.CompleteEmailAsync(s.Mail.Token, null, default);
        await s.Accounts.SendAsync("test@example.com", true, default); token = s.Mail.Token;
        s.Clock.Now = s.Clock.Now.AddMinutes(31);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Accounts.CompleteEmailAsync(token, Password, default));
    }
    [TestMethod]
    public async Task Guest_Registration_Preserves_User_And_Event_Ownership()
    {
        await using var s = await Scope.Create();
        var guest = await s.Sessions.CreateGuestAsync(null, default);
        var ev = new AfterTab.DAL.Entities.Events.Event { OwnerUserId = guest.User.Id, Name = "Existing" };
        s.Db.Events.Add(ev); await s.Db.SaveChangesAsync();
        await s.Accounts.RegisterAsync("guest@example.com", Password, "My name", guest.Token, default);
        await s.Accounts.CompleteEmailAsync(s.Mail.Token, null, default);
        Assert.AreEqual(guest.User.Id, await s.Accounts.LoginAsync("guest@example.com", Password, default));
        Assert.AreEqual(1, await s.Db.Users.CountAsync());
        Assert.AreEqual(guest.User.Id, (await s.Db.Events.SingleAsync()).OwnerUserId);
        Assert.IsFalse((await s.Sessions.ValidateAsync(guest.Token, default))!.User.IsGuest);
    }
    [TestMethod]
    public async Task Social_Identity_Is_Stable_Preserves_Guest_And_Never_Merges_By_Email()
    {
        await using var s = await Scope.Create();
        var guest = await s.Sessions.CreateGuestAsync(null, default);
        var google = await s.Accounts.ExternalAsync(IdentityProvider.Google, "google-id", "Google Name", "same@example.com", guest.User.Id, default);
        Assert.AreEqual(guest.User.Id, google);
        var repeat = await s.Accounts.ExternalAsync(IdentityProvider.Google, "google-id", "New Name", "changed@example.com", null, default);
        Assert.AreEqual(google, repeat);
        var facebook = await s.Accounts.ExternalAsync(IdentityProvider.Facebook, "facebook-id", "Facebook Name", "same@example.com", null, default);
        Assert.AreNotEqual(google, facebook);
        var newUser = await s.Db.Users.SingleAsync(x => x.Id == facebook);
        Assert.IsFalse(newUser.DisplayNameConfirmed);
        var secondGuest = await s.Sessions.CreateGuestAsync(null, default);
        Assert.AreEqual(google, await s.Accounts.ExternalAsync(IdentityProvider.Google, "google-id", "Name", null, secondGuest.User.Id, default));
        Assert.IsFalse(await s.Db.UserIdentities.AnyAsync(x => x.UserId == secondGuest.User.Id));
    }
    [TestMethod]
    public async Task Five_Bad_Passwords_Lock_Account_And_Unknown_Email_Has_No_Side_Effects()
    {
        await using var s = await Scope.Create();
        await s.Accounts.RegisterAsync("test@example.com", Password, "Test", null, default);
        await s.Accounts.CompleteEmailAsync(s.Mail.Token, null, default);
        for (var i = 0; i < 5; i++) await Assert.ThrowsExactlyAsync<AppException>(() => s.Accounts.LoginAsync("test@example.com", "Wrong password", default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Accounts.LoginAsync("test@example.com", Password, default));
        s.Clock.Now = s.Clock.Now.AddMinutes(16);
        await s.Accounts.LoginAsync("test@example.com", Password, default);
        var count = s.Mail.Links.Count;
        await s.Accounts.SendAsync("unknown@example.com", true, default);
        await s.Accounts.SendAsync("unknown@example.com", false, default);
        Assert.AreEqual(count, s.Mail.Links.Count);
    }
    [TestMethod]
    public async Task Proof_Is_Verifier_Bound_And_Consumed_Action_Cannot_Be_Reused()
    {
        await using var s = await Scope.Create();
        var verifier = AccountService.Secret();
        Assert.IsTrue(AccountService.Matches(AccountService.Hash(verifier), verifier));
        Assert.IsFalse(AccountService.Matches(AccountService.Hash(verifier), AccountService.Secret()));
        var token = s.Accounts.AddAction(null, "exchange", TimeSpan.FromMinutes(1)); await s.Db.SaveChangesAsync();
        var action = await s.Accounts.FindAsync(token, "exchange", default);
        Assert.IsNotNull(action);
        s.Accounts.Consume(action); await s.Accounts.SaveOnceAsync(default);
        Assert.IsNull(await s.Accounts.FindAsync(token, "exchange", default));
    }
}
