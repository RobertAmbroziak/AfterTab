using System.Security.Cryptography;
using System.Text;
using AfterTab.BL.Services.Users;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.EF.Repositories;
using AfterTab.DAL.Entities.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class GuestSessionTests
{
    private sealed class Actor : ICurrentUser { public Guid? UserId => null; }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [TestMethod]
    public async Task Guest_Creates_User_And_Hashed_Session_And_Resumes_Without_Duplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = new Clock();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options, new Actor(), clock);
        await db.Database.EnsureCreatedAsync();
        var service = new SessionService(new SessionRepository(db), new UserRepository(db), clock, new GuestBrowserRepository(db));

        var issued = await service.CreateGuestAsync(null, default);
        db.ChangeTracker.Clear();
        var user = await db.Users.SingleAsync();
        var session = await db.Sessions.SingleAsync();
        Assert.IsTrue(issued.User.IsGuest);
        Assert.AreEqual(user.Id, issued.User.Id);
        Assert.AreEqual(user.Id, session.UserId);
        Assert.AreEqual(clock.Now.AddDays(30), session.ExpiresAtUtc);
        Assert.AreEqual(clock.Now, session.CreatedAtUtc);
        Assert.AreNotEqual(issued.Token, session.TokenHash);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(issued.Token))), session.TokenHash);
        Assert.AreEqual(0, await db.UserIdentities.CountAsync());

        var resumed = await service.CreateGuestAsync(issued.Token, default);
        Assert.AreEqual(issued, resumed);
        Assert.AreEqual(1, await db.Users.CountAsync());
        Assert.AreEqual(1, await db.Sessions.CountAsync());
        var profile = await service.ValidateAsync(issued.Token, default);
        Assert.AreEqual(user.Id, profile!.User.Id);
    }

    [TestMethod]
    public async Task Invalid_Expired_Revoked_And_Disabled_Sessions_Are_Rejected()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var clock = new Clock();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options, new Actor(), clock);
        await db.Database.EnsureCreatedAsync();
        var service = new SessionService(new SessionRepository(db), new UserRepository(db), clock, new GuestBrowserRepository(db));
        var issued = await service.CreateGuestAsync(null, default);

        Assert.IsNull(await service.ValidateAsync(null, default));
        Assert.IsNull(await service.ValidateAsync("invalid", default));
        Assert.IsNull(await service.ValidateAsync(new string('A', 64), default));
        clock.Now = issued.ExpiresAtUtc;
        Assert.IsNull(await service.ValidateAsync(issued.Token, default));
        clock.Now = clock.Now.AddDays(-1);

        var user = await db.Users.SingleAsync();
        user.Status = UserStatus.Deleted;
        await db.SaveChangesAsync();
        Assert.IsNull(await service.ValidateAsync(issued.Token, default));
        user.Status = UserStatus.Active;
        await db.SaveChangesAsync();
        Assert.IsNotNull(await service.ValidateAsync(issued.Token, default));

        await service.RevokeAsync(issued.Token, default);
        Assert.IsNull(await service.ValidateAsync(issued.Token, default));
        Assert.IsNotNull((await db.Sessions.SingleAsync()).RevokedAtUtc);
        var replacement = await service.CreateGuestAsync(issued.Token, default);
        Assert.AreNotEqual(issued.User.Id, replacement.User.Id);
    }
}
