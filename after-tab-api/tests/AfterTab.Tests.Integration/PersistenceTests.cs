using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Entities.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace AfterTab.Tests.Integration;
[TestClass]
public sealed class PersistenceTests
{
    private sealed class Actor : ICurrentUser { public Guid? UserId { get; set; } }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    [TestMethod]
    public async Task Audit_Protects_Creation_And_Tracks_Async_And_Sync_Updates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var actor = new Actor();
        var clock = new Clock();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options, actor, clock);
        await db.Database.EnsureCreatedAsync();
        var user = new User { DisplayName = "Jarek" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var created = user.CreatedAtUtc;
        Assert.AreEqual(clock.Now, created);
        Assert.IsNull(user.CreatedByUserId);
        Assert.IsNull(user.ModifiedAtUtc);

        actor.UserId = user.Id;
        clock.Now = clock.Now.AddHours(1);
        user.DisplayName = "Jaro";
        user.CreatedAtUtc = clock.Now.AddYears(-5);
        user.CreatedByUserId = user.Id;
        db.SaveChanges();
        await db.Entry(user).ReloadAsync();
        Assert.AreEqual(created, user.CreatedAtUtc);
        Assert.IsNull(user.CreatedByUserId);
        Assert.AreEqual(clock.Now, user.ModifiedAtUtc);
        Assert.AreEqual(user.Id, user.ModifiedByUserId);

        clock.Now = clock.Now.AddHours(1);
        user.DisplayName = "Jaroslaw";
        await db.SaveChangesAsync(true);
        Assert.AreEqual(clock.Now, user.ModifiedAtUtc);
    }

    [TestMethod]
    public async Task Duplicate_Event_Membership_Is_Rejected_By_Database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options, new Actor(), new Clock());
        await db.Database.EnsureCreatedAsync();
        var user = new User { DisplayName = "Jarek" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var ev = new Event { Name = "Weekend", OwnerUserId = user.Id };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        db.EventParticipants.Add(new EventParticipant { EventId = ev.Id, UserId = user.Id });
        await db.SaveChangesAsync();
        db.EventParticipants.Add(new EventParticipant { EventId = ev.Id, UserId = user.Id });
        await Assert.ThrowsExactlyAsync<DbUpdateException>(async () => await db.SaveChangesAsync());
    }

    [TestMethod]
    public void SqlServer_Model_Uses_Independent_Tables_And_Composite_Foreign_Keys()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelOnly;Integrated Security=true;TrustServerCertificate=true").Options;
        using var db = new AppDbContext(options, new Actor(), new Clock());
        var types = db.Model.GetEntityTypes().ToList();
        Assert.AreEqual(11, types.Count);
        Assert.IsTrue(types.All(x => x.BaseType is null));
        var split = db.Model.FindEntityType(typeof(AfterTab.DAL.Entities.Expenses.ExpenseSplit))!;
        Assert.AreEqual(2, split.GetForeignKeys().Count(x => x.Properties.Count == 2));
        var sql = db.Database.GenerateCreateScript();
        StringAssert.Contains(sql, "rowversion");
        StringAssert.Contains(sql, "CK_ExpenseSplits_Percentage");
    }
}
