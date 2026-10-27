using System.Security.Cryptography;
using System.Text;
using AfterTab.BL.DTO.Users;
using AfterTab.BL.Services.Users;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.EF.Repositories;
using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Entities.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class RememberedGuestTests
{
    private const string Verifier = "1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF1234567890ABCDEF";
    private static string Challenge => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Verifier)));
    private sealed class Actor : ICurrentUser { public Guid? UserId => null; }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Scope : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required AppDbContext Db { get; init; }
        public required SessionService Sessions { get; init; }
        public required GuestBrowserService Browser { get; init; }
        public required Clock Clock { get; init; }
        public static async Task<Scope> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var clock = new Clock();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options, new Actor(), clock);
            await db.Database.EnsureCreatedAsync();
            var users = new UserRepository(db);
            var repository = new GuestBrowserRepository(db);
            var sessions = new SessionRepository(db);
            var service = new SessionService(sessions, users, clock, repository);
            return new Scope { Connection = connection, Db = db, Clock = clock, Sessions = service,
                Browser = new GuestBrowserService(repository, users, sessions, service, clock) };
        }
        public async Task<GuestBrowserRedirectDto> Start(string action = "login", string? cookie = null, string? sessionToken = null, string? displayName = null)
        {
            var request = await Browser.PrepareAsync(new(Challenge, Verifier, "https://pwa.example", action, displayName), sessionToken, default);
            Assert.IsNotNull(request);
            var redirect = await Browser.ContinueAsync(request, Verifier, cookie, default);
            Assert.IsNotNull(redirect);
            return redirect;
        }
        public async Task<GuestBrowserResultDto> Finish(GuestBrowserRedirectDto redirect)
        {
            var result = await Browser.ExchangeAsync(new(redirect.Code, Verifier), default);
            Assert.IsNotNull(result);
            Db.ChangeTracker.Clear();
            return result;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    [TestMethod]
    public async Task First_Login_Confirms_Name_And_Returning_Login_Does_Not_Overwrite_It()
    {
        await using var scope = await Scope.Create();
        var redirect = await scope.Start(displayName: "  Zosia  ");
        var first = (await scope.Finish(redirect)).Session!;
        Assert.AreEqual("Zosia", first.User.DisplayName);
        Assert.IsFalse(first.User.RequiresDisplayName);
        await scope.Sessions.RevokeAsync(first.Token, default);
        var returning = (await scope.Finish(await scope.Start(cookie: redirect.DeviceToken, displayName: "Inna nazwa"))).Session!;
        Assert.AreEqual(first.User.Id, returning.User.Id);
        Assert.AreEqual("Zosia", returning.User.DisplayName);
    }

    [TestMethod]
    public async Task Logout_Then_Login_Reuses_User_But_Issues_New_Session()
    {
        await using var scope = await Scope.Create();
        var firstRedirect = await scope.Start();
        var first = (await scope.Finish(firstRedirect)).Session!;
        var device = await scope.Db.GuestDevices.AsNoTracking().SingleAsync();
        Assert.AreNotEqual(firstRedirect.DeviceToken, device.TokenHash);
        Assert.AreEqual(Challenge.Length, device.TokenHash.Length);
        await scope.Sessions.RevokeAsync(first.Token, default);
        Assert.IsNull(await scope.Sessions.ValidateAsync(first.Token, default));
        var secondRedirect = await scope.Start(cookie: firstRedirect.DeviceToken);
        var second = (await scope.Finish(secondRedirect)).Session!;
        Assert.AreEqual(first.User.Id, second.User.Id);
        Assert.AreNotEqual(first.Token, second.Token);
        Assert.AreEqual(1, await scope.Db.Users.CountAsync());
        Assert.AreEqual(2, await scope.Db.Sessions.CountAsync());
        Assert.IsNotNull(await scope.Sessions.ValidateAsync(second.Token, default));
    }

    [TestMethod]
    public async Task Forget_Revokes_Device_And_All_Its_Sessions_Without_Deleting_User()
    {
        await using var scope = await Scope.Create();
        var redirect = await scope.Start();
        var first = (await scope.Finish(redirect)).Session!;
        var second = (await scope.Finish(await scope.Start(cookie: redirect.DeviceToken))).Session!;
        var forgotten = await scope.Finish(await scope.Start("forget", redirect.DeviceToken, second.Token));
        Assert.IsFalse(forgotten.Remembered);
        Assert.IsNull(forgotten.Session);
        Assert.IsNull(await scope.Sessions.ValidateAsync(first.Token, default));
        Assert.IsNull(await scope.Sessions.ValidateAsync(second.Token, default));
        Assert.IsNotNull((await scope.Db.GuestDevices.SingleAsync()).RevokedAtUtc);
        Assert.AreEqual(1, await scope.Db.Users.CountAsync());
        var replacement = (await scope.Finish(await scope.Start(cookie: redirect.DeviceToken))).Session!;
        Assert.AreNotEqual(first.User.Id, replacement.User.Id);
    }

    [TestMethod]
    public async Task Exchange_Requires_Verifier_And_Is_Single_Use_And_Expires()
    {
        await using var scope = await Scope.Create();
        var redirect = await scope.Start();
        Assert.IsNull(await scope.Browser.ExchangeAsync(new(redirect.Code, new string('A', 64)), default));
        Assert.AreEqual(0, await scope.Db.Sessions.CountAsync());
        var issued = await scope.Finish(redirect);
        Assert.IsNotNull(issued.Session);
        Assert.IsNull(await scope.Browser.ExchangeAsync(new(redirect.Code, Verifier), default));
        var expired = await scope.Start(cookie: redirect.DeviceToken);
        scope.Clock.Now = scope.Clock.Now.AddMinutes(6);
        Assert.IsNull(await scope.Browser.ExchangeAsync(new(expired.Code, Verifier), default));
        Assert.AreEqual(1, await scope.Db.Sessions.CountAsync());
    }

    [TestMethod]
    public async Task Request_Handle_Alone_Cannot_Create_A_Remembered_Credential()
    {
        await using var scope = await Scope.Create();
        var legacy = await scope.Sessions.CreateGuestAsync(null, default);
        var request = await scope.Browser.PrepareAsync(new(Challenge, Verifier, "https://pwa.example"), legacy.Token, default);
        Assert.IsNotNull(request);
        Assert.IsNull(await scope.Browser.ContinueAsync(request, new string('A', 64), null, default));
        Assert.AreEqual(0, await scope.Db.GuestDevices.CountAsync());
        var authorized = await scope.Browser.ContinueAsync(request, Verifier, null, default);
        Assert.IsNotNull(authorized);
        var adopted = (await scope.Finish(authorized)).Session!;
        Assert.AreEqual(legacy.User.Id, adopted.User.Id);
    }

    [TestMethod]
    public async Task Existing_Guest_Is_Adopted_And_Old_Session_Is_Revoked()
    {
        await using var scope = await Scope.Create();
        var legacy = await scope.Sessions.CreateGuestAsync(null, default);
        var adopted = (await scope.Finish(await scope.Start(sessionToken: legacy.Token))).Session!;
        Assert.AreEqual(legacy.User.Id, adopted.User.Id);
        Assert.AreEqual(1, await scope.Db.Users.CountAsync());
        Assert.IsNull(await scope.Sessions.ValidateAsync(legacy.Token, default));
        Assert.IsNotNull(await scope.Sessions.ValidateAsync(adopted.Token, default));
    }

    [TestMethod]
    public async Task Lost_Cookie_Creates_New_Guest_And_Expired_Session_Still_Resumes_With_Cookie()
    {
        await using var scope = await Scope.Create();
        var redirect = await scope.Start();
        var first = (await scope.Finish(redirect)).Session!;
        scope.Clock.Now = scope.Clock.Now.AddDays(31);
        Assert.IsNull(await scope.Sessions.ValidateAsync(first.Token, default));
        var resumed = (await scope.Finish(await scope.Start(cookie: redirect.DeviceToken))).Session!;
        Assert.AreEqual(first.User.Id, resumed.User.Id);
        var lost = (await scope.Finish(await scope.Start())).Session!;
        Assert.AreNotEqual(first.User.Id, lost.User.Id);
    }

    [TestMethod]
    public async Task Blocked_User_Cannot_Be_Resumed_From_Device()
    {
        await using var scope = await Scope.Create();
        var redirect = await scope.Start();
        var first = (await scope.Finish(redirect)).Session!;
        var user = await scope.Db.Users.SingleAsync();
        user.Status = UserStatus.Blocked;
        await scope.Db.SaveChangesAsync();
        var next = (await scope.Finish(await scope.Start(cookie: redirect.DeviceToken))).Session!;
        Assert.AreNotEqual(first.User.Id, next.User.Id);
    }

    [TestMethod]
    public async Task Device_Does_Not_Bypass_Registered_Account_Login()
    {
        await using var scope = await Scope.Create();
        var redirect = await scope.Start();
        var first = (await scope.Finish(redirect)).Session!;
        scope.Db.UserIdentities.Add(new UserIdentity { UserId = first.User.Id,
            Provider = IdentityProvider.Google, ProviderSubject = "test-google-subject" });
        await scope.Db.SaveChangesAsync();
        var next = (await scope.Finish(await scope.Start(cookie: redirect.DeviceToken))).Session!;
        Assert.AreNotEqual(first.User.Id, next.User.Id);
    }

    [TestMethod]
    public async Task Legacy_Session_Takes_Precedence_Over_Different_Remembered_Guest()
    {
        await using var scope = await Scope.Create();
        var redirect = await scope.Start();
        await scope.Finish(redirect);
        var legacy = await scope.Sessions.CreateGuestAsync(null, default);
        var adopted = await scope.Start(cookie: redirect.DeviceToken, sessionToken: legacy.Token);
        var result = (await scope.Finish(adopted)).Session!;
        Assert.AreEqual(legacy.User.Id, result.User.Id);
        Assert.AreNotEqual(redirect.DeviceToken, adopted.DeviceToken);
    }
}
