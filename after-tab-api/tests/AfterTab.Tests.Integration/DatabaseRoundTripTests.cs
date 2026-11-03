using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Services.Expenses;
using AfterTab.BL.Services.Events;
using AfterTab.BL.Services.Users;
using AfterTab.Common.Errors;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.EF.Repositories;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using AfterTab.DAL.Entities.Users;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Npgsql;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class DatabaseRoundTripTests
{
    private sealed class Actor : ICurrentUser { public long? UserId { get; set; } }

    [TestMethod]
    [TestCategory("DatabaseServer")]
    [DataRow("SqlServer", "AFTERTAB_TEST_SQLSERVER")]
    [DataRow("PostgreSql", "AFTERTAB_TEST_POSTGRESQL")]
    public async Task Migrations_Sessions_Expenses_Constraints_And_Concurrency_Work_On_Real_Server(string providerName, string variable)
    {
        var serverConnection = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(serverConnection))
        {
            Assert.Inconclusive($"Set {variable} to a local test server connection with permission to create temporary databases.");
            return;
        }
        // Always override the database name. Cleanup can only delete the database created by this test.
        var name = "aftertab_test_" + Guid.NewGuid().ToString("N");
        var provider = DatabaseConfiguration.ParseProvider(providerName);
        var connection = provider == DatabaseProvider.SqlServer
            ? new SqlConnectionStringBuilder(serverConnection) { InitialCatalog = name }.ConnectionString
            : new NpgsqlConnectionStringBuilder(serverConnection) { Database = name, Pooling = false }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAfterTabDatabase(connection, provider);
        var actor = new Actor();
        await using var db = new AppDbContext(options.Options, actor, TimeProvider.System);
        try
        {
            await db.MigrateFreshSchemaAsync();
            await db.MigrateFreshSchemaAsync(); // Reapplying must be a no-op.
            Assert.IsFalse((await db.Database.GetPendingMigrationsAsync()).Any());
            if (provider == DatabaseProvider.SqlServer)
                Assert.AreEqual(0, await db.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) AS [Value] FROM sys.indexes WHERE is_primary_key = 1 AND type <> 1").SingleAsync());

            var sessions = new SessionService(new SessionRepository(db), new UserRepository(db),
                TimeProvider.System, new GuestBrowserRepository(db));
            var session = await sessions.CreateGuestAsync(null, default);
            Assert.IsNotNull(await sessions.ValidateAsync(session.Token, default));
            await sessions.RevokeAsync(session.Token, default);
            Assert.IsNull(await sessions.ValidateAsync(session.Token, default));

            var owner = new User { DisplayName = "Owner", DisplayNameConfirmed = true };
            var member = new User { DisplayName = "Member", DisplayNameConfirmed = true };
            db.Users.AddRange(owner, member);
            await db.SaveChangesAsync();
            actor.UserId = member.Id;
            var events = new EventService(new EventRepository(db), new UserRepository(db), actor, TimeProvider.System);
            var eventResult = await events.CreateAsync(new("Dinner"), default);
            var ev = await db.Events.SingleAsync(x => x.Id == eventResult.Details.Event.Id);
            var memberParticipant = await db.EventParticipants.SingleAsync(x => x.EventId == ev.Id && x.UserId == member.Id);
            var ownerParticipant = new EventParticipant { EventId = ev.Id, UserId = owner.Id };
            db.EventParticipants.Add(ownerParticipant);
            await db.SaveChangesAsync();
            Assert.AreEqual(member.Id, ev.CreatedByUserId);
            Assert.AreEqual(TimeSpan.Zero, ev.CreatedAtUtc.Offset);
            Assert.AreEqual(1, (await new EventRepository(db).ListAsync(member.Id, 1, default)).TotalCount);

            var service = new ExpenseService(new ExpenseRepository(db), new UserRepository(db), actor, TimeProvider.System);
            var created = await service.CreateAsync(ev.Id,
                new CreateExpenseDto("Pizza", 6250, "Amount", [new(ownerParticipant.Id, 3000), new(memberParticipant.Id, 3000)],
                    Balance: true, BalanceParticipantId: ownerParticipant.Id), default);
            Assert.IsFalse(string.IsNullOrWhiteSpace(created.RowVersion));
            Assert.IsTrue(created.Balanced);
            var update = new UpdateExpenseSplitDto("Amount", [new(ownerParticipant.Id, 3100), new(memberParticipant.Id, 3150)], false, created.RowVersion!);
            var changed = await service.UpdateSplitAsync(ev.Id, created.Id, update, default);
            Assert.AreNotEqual(created.RowVersion, changed.RowVersion);
            var stale = await Assert.ThrowsExactlyAsync<AppException>(() => service.UpdateSplitAsync(ev.Id, created.Id, update, default));
            Assert.AreEqual(ErrorKind.Conflict, stale.Kind);

            // Two independently loaded versions must conflict at the EF/database boundary too.
            await using (var first = new AppDbContext(options.Options, actor, TimeProvider.System))
            await using (var second = new AppDbContext(options.Options, actor, TimeProvider.System))
            {
                var a = await first.Expenses.SingleAsync(x => x.Id == created.Id);
                var b = await second.Expenses.SingleAsync(x => x.Id == created.Id);
                a.Note = "First writer";
                first.SaveChanges(); // Both synchronous and asynchronous saves must refresh the token.
                b.Note = "Stale writer";
                await Assert.ThrowsExactlyAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
            }

            db.ChangeTracker.Clear();
            var invalid = await db.Expenses.SingleAsync(x => x.Id == created.Id);
            invalid.AmountMinor = -1;
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            // New contexts/processes reserve different HiLo blocks, including after a restart.
            await using (var another = new AppDbContext(options.Options, actor, TimeProvider.System))
            {
                var users = Enumerable.Range(0, 25).Select(i => new User { DisplayName = $"Additional {i}" }).ToArray();
                another.Users.AddRange(users);
                Assert.IsTrue(users.All(x => x.Id > 0));
                Assert.AreEqual(users.Length, users.Select(x => x.Id).Distinct().Count());
                Assert.IsFalse(users.Any(x => x.Id == owner.Id || x.Id == member.Id));
                await another.SaveChangesAsync();
            }
            var original = await db.Users.AsNoTracking().SingleAsync(x => x.Id == owner.Id);
            db.Users.Add(new User { DisplayName = "Duplicate public identifier", PublicId = original.PublicId });
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            var countBefore = await db.Users.CountAsync();
            await db.Database.ExecuteSqlRawAsync("INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('legacy-guid-schema', '9.0.21')");
            var incompatible = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => db.MigrateFreshSchemaAsync());
            StringAssert.Contains(incompatible.Message, "new empty database");
            Assert.AreEqual(countBefore, await db.Users.CountAsync());
            db.ChangeTracker.Clear();

            // Local identities may share a null ProviderSubject; normalized local email must be unique.
            db.UserIdentities.AddRange(
                new UserIdentity { UserId = owner.Id, Provider = IdentityProvider.Local, NormalizedEmail = "ONE@EXAMPLE.TEST" },
                new UserIdentity { UserId = member.Id, Provider = IdentityProvider.Local, NormalizedEmail = "TWO@EXAMPLE.TEST" });
            await db.SaveChangesAsync();
            db.UserIdentities.Add(new UserIdentity
                { UserId = member.Id, Provider = IdentityProvider.Local, NormalizedEmail = "ONE@EXAMPLE.TEST" });
            await Assert.ThrowsExactlyAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
