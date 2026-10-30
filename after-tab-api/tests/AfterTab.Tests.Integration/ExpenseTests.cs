using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Services.Expenses;
using AfterTab.BL.Services.Events;
using AfterTab.Common.Errors;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.EF.Repositories;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using AfterTab.DAL.Entities.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;
[TestClass]
public sealed class ExpenseTests
{
    private sealed class Actor : ICurrentUser { public Guid? UserId { get; set; } }
    // SQLite has no SQL Server rowversion generator. Stale-version behavior is also checked on LocalDB.
    public sealed class SqliteModel(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder builder, DbContext context)
        {
            base.Customize(builder, context);
            builder.Entity<Expense>().Property(x => x.RowVersion).HasDefaultValue(new byte[] { 1 });
            builder.Entity<Expense>().Property(x => x.CreatedAtUtc)
                .HasConversion(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        }
    }
    private sealed class Scope : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required AppDbContext Db { get; init; }
        public required Actor Actor { get; init; }
        public required ExpenseService Service { get; init; }
        public required Event Event { get; init; }
        public required User Owner { get; init; }
        public required User Member { get; init; }
        public required User Stranger { get; init; }
        public static async Task<Scope> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var actor = new Actor();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection)
                .ReplaceService<IModelCustomizer, SqliteModel>().Options, actor, TimeProvider.System);
            await db.Database.EnsureCreatedAsync();
            var owner = new User { DisplayName = "Ola", DisplayNameConfirmed = true };
            var member = new User { DisplayName = "Adam", DisplayNameConfirmed = true };
            var stranger = new User { DisplayName = "Ewa", DisplayNameConfirmed = true };
            db.Users.AddRange(owner, member, stranger); await db.SaveChangesAsync();
            var ev = new Event { Name = "Kolacja", OwnerUserId = owner.Id };
            db.Events.Add(ev);
            db.EventParticipants.AddRange(new EventParticipant { Id = owner.Id, EventId = ev.Id, UserId = owner.Id }, new EventParticipant { Id = member.Id, EventId = ev.Id, UserId = member.Id });
            await db.SaveChangesAsync(); actor.UserId = member.Id;
            return new() { Connection = connection, Db = db, Actor = actor, Event = ev, Owner = owner, Member = member, Stranger = stranger,
                Service = new ExpenseService(new ExpenseRepository(db), new UserRepository(db), actor, TimeProvider.System) };
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }
    [TestMethod]
    public async Task Participant_Creates_Atomic_Split_And_All_Members_See_It_After_Rename()
    {
        await using var s = await Scope.Create();
        var result = await s.Service.CreateAsync(s.Event.Id, new(" Pizza ", 6250, "Amount", [new(s.Owner.Id, 3000), new(s.Member.Id, 2000)]), default);
        Assert.AreEqual("Pizza", result.Name); Assert.AreEqual(s.Member.Id, result.AuthorUserId);
        Assert.AreEqual(1250L, result.OthersAmountMinor);
        Assert.AreEqual(6250L, result.Splits.Sum(x => x.AmountMinor) + result.OthersAmountMinor);
        Assert.AreEqual(1, await s.Db.Expenses.CountAsync()); Assert.AreEqual(2, await s.Db.ExpenseSplits.CountAsync());
        s.Member.DisplayName = "Adam Nowy"; s.Db.Users.Update(s.Member); await s.Db.SaveChangesAsync();
        s.Actor.UserId = s.Owner.Id;
        var listed = (await s.Service.ListAsync(s.Event.Id, default)).Single();
        Assert.AreEqual("Adam Nowy", listed.AuthorName);
        Assert.AreEqual("Adam Nowy", listed.Splits.Single(x => x.ParticipantId == s.Member.Id).DisplayName);
    }
    [TestMethod]
    public async Task Invalid_Person_OverAllocation_And_Unrelated_User_Do_Not_Write()
    {
        await using var s = await Scope.Create();
        var input = new CreateExpenseDto("Pizza", 6250, "Amount", [new(s.Stranger.Id, 1000)]);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.CreateAsync(s.Event.Id, input, default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.CreateAsync(s.Event.Id, input with { Shares = [new(s.Owner.Id, 6251)] }, default));
        s.Actor.UserId = s.Stranger.Id;
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.CreateAsync(s.Event.Id, input with { Shares = [] }, default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.ListAsync(s.Event.Id, default));
        Assert.AreEqual(0, await s.Db.Expenses.CountAsync()); Assert.AreEqual(0, await s.Db.ExpenseSplits.CountAsync());
    }
    [TestMethod]
    public async Task Closed_Event_Unconfirmed_Or_Removed_User_Cannot_Create()
    {
        await using var s = await Scope.Create();
        var input = new CreateExpenseDto("Pizza", 6250, "Amount", []);
        s.Event.Status = EventStatus.Closed; await s.Db.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.CreateAsync(s.Event.Id, input, default));
        s.Event.Status = EventStatus.Active; s.Member.DisplayNameConfirmed = false; await s.Db.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.CreateAsync(s.Event.Id, input, default));
        s.Member.DisplayNameConfirmed = true;
        (await s.Db.EventParticipants.SingleAsync(x => x.UserId == s.Member.Id)).Status = ParticipantStatus.Removed;
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.CreateAsync(s.Event.Id, input, default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.ListAsync(s.Event.Id, default));
        Assert.AreEqual(0, await s.Db.Expenses.CountAsync());
    }
    [TestMethod]
    public async Task Percentage_Balance_Persists_Exact_Amounts_And_Join_Does_Not_Redistribute_Others()
    {
        await using var s = await Scope.Create();
        var input = new CreateExpenseDto("Pizza", 6250, "Percentage", [new(s.Owner.Id, PercentageBps: 3300), new(s.Member.Id, PercentageBps: 3300)]);
        var partial = await s.Service.CreateAsync(s.Event.Id, input, default);
        Assert.AreEqual(2126L, partial.OthersAmountMinor);
        var balanced = await s.Service.CreateAsync(s.Event.Id, input with { Balance = true }, default);
        Assert.AreEqual(4188L, balanced.Splits.Single(x => x.ParticipantId == s.Member.Id).AmountMinor);
        Assert.AreEqual(0L, balanced.OthersAmountMinor);
        s.Db.EventParticipants.Add(new EventParticipant { Id = s.Stranger.Id, EventId = s.Event.Id, UserId = s.Stranger.Id }); await s.Db.SaveChangesAsync();
        s.Actor.UserId = s.Stranger.Id;
        var listed = await s.Service.ListAsync(s.Event.Id, default);
        Assert.AreEqual(2126L, listed.Single(x => x.Id == partial.Id).OthersAmountMinor);
        Assert.AreEqual(6250L, listed.Single(x => x.Id == balanced.Id).Splits.Sum(x => x.AmountMinor));
    }
    [TestMethod]
    public async Task Settlement_Close_Reopen_Reclose_Updates_Version_And_Blocks_All_Writes()
    {
        await using var s = await Scope.Create();
        var settlements = new SettlementService(new SettlementRepository(s.Db), s.Actor, TimeProvider.System);
        var item = await s.Service.CreateAsync(s.Event.Id, new("Pizza", 1200, "Amount", [new(s.Owner.Id, 1200)]), default);
        s.Actor.UserId = s.Owner.Id;
        await s.Service.CreateAsync(s.Event.Id, new("Napoje", 300, "Amount", [new(s.Member.Id, 300)]), default);
        var preview = await settlements.GetAsync(s.Event.Id, default);
        Assert.IsFalse(preview.IsFinal); Assert.AreEqual(900L, preview.Transfers.Single().AmountMinor);
        var closed = await settlements.SetClosedAsync(s.Event.Id, true, default);
        Assert.IsTrue(closed.IsFinal); Assert.AreEqual(1, closed.Version); Assert.IsNotNull(closed.ClosedAtUtc);
        Assert.AreEqual(1, (await settlements.SetClosedAsync(s.Event.Id, true, default)).Version);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.CreateAsync(s.Event.Id, new("Nowa", 100, "Amount", []), default));
        s.Actor.UserId = s.Member.Id;
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdateSplitAsync(s.Event.Id, item.Id, new("Amount", [new(s.Owner.Id, 1200)], false, item.RowVersion!), default));
        Assert.IsTrue((await settlements.GetAsync(s.Event.Id, default)).IsFinal);
        await Assert.ThrowsExactlyAsync<AppException>(() => settlements.SetClosedAsync(s.Event.Id, false, default));
        s.Actor.UserId = s.Owner.Id;
        var reopened = await settlements.SetClosedAsync(s.Event.Id, false, default); Assert.IsFalse(reopened.IsFinal);
        await s.Service.CreateAsync(s.Event.Id, new("Dodatek", 100, "Amount", [new(s.Member.Id, 100)]), default);
        var reclosed = await settlements.SetClosedAsync(s.Event.Id, true, default);
        Assert.AreEqual(2, reclosed.Version); Assert.AreEqual(800L, reclosed.Transfers.Single().AmountMinor);
        s.Actor.UserId = s.Stranger.Id;
        await Assert.ThrowsExactlyAsync<AppException>(() => settlements.GetAsync(s.Event.Id, default));
        await Assert.ThrowsExactlyAsync<AppException>(() => settlements.SetClosedAsync(s.Event.Id, false, default));
    }
    [TestMethod]
    public async Task Others_Block_Close_Author_Can_Fix_Split_And_Stale_Revision_Is_Rejected()
    {
        await using var s = await Scope.Create();
        var settlements = new SettlementService(new SettlementRepository(s.Db), s.Actor, TimeProvider.System);
        var item = await s.Service.CreateAsync(s.Event.Id, new("Pizza", 6250, "Amount", [new(s.Owner.Id, 2000), new(s.Member.Id, 2000)]), default);
        s.Actor.UserId = s.Owner.Id;
        var failed = await Assert.ThrowsExactlyAsync<AppException>(() => settlements.SetClosedAsync(s.Event.Id, true, default));
        Assert.AreEqual(ErrorKind.Conflict, failed.Kind); Assert.IsFalse((await settlements.GetAsync(s.Event.Id, default)).IsFinal);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdateSplitAsync(s.Event.Id, item.Id, new("Amount", [], true, item.RowVersion!), default));
        s.Actor.UserId = s.Member.Id;
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdateSplitAsync(s.Event.Id, item.Id, new("Amount", [], true, "Ag=="), default));
        var fixedItem = await s.Service.UpdateSplitAsync(s.Event.Id, item.Id, new("Amount", [new(s.Owner.Id, 2000), new(s.Member.Id, 2000)], true, item.RowVersion!), default);
        Assert.AreEqual(0L, fixedItem.OthersAmountMinor); Assert.AreEqual(4250L, fixedItem.Splits.Single(x => x.ParticipantId == s.Member.Id).AmountMinor);
        s.Actor.UserId = s.Owner.Id;
        Assert.IsTrue((await settlements.SetClosedAsync(s.Event.Id, true, default)).IsFinal);
    }
    [TestMethod]
    public async Task Repository_Rejects_Stale_Create_After_Event_Was_Closed()
    {
        await using var s = await Scope.Create();
        var participant = await s.Db.EventParticipants.AsNoTracking().SingleAsync(x => x.UserId == s.Member.Id);
        var stale = new Expense { Name = "Spóźniona", EventId = s.Event.Id, AmountMinor = 100, OthersAmountMinor = 100,
            PaidByParticipantId = participant.Id, SplitMethod = SplitMethod.Amount, ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow) };
        s.Actor.UserId = s.Owner.Id;
        await new SettlementService(new SettlementRepository(s.Db), s.Actor, TimeProvider.System).SetClosedAsync(s.Event.Id, true, default);
        s.Actor.UserId = s.Member.Id;
        var error = await Assert.ThrowsExactlyAsync<AppException>(() => new ExpenseRepository(s.Db).CreateAsync(stale, default));
        Assert.AreEqual(ErrorKind.Conflict, error.Kind); Assert.AreEqual(0, await s.Db.Expenses.CountAsync());
    }
    [TestMethod]
    public async Task Only_Event_Owner_Selects_Or_Changes_Payer_Without_Changing_Author()
    {
        await using var s = await Scope.Create();
        var forbidden = new AfterTab.BL.DTO.Expenses.CreateExpenseDto("Pizza", 3000, "Amount", [new(s.Owner.Id, 3000)], PaidByParticipantId: s.Owner.Id);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.CreateAsync(s.Event.Id, forbidden, default));
        s.Actor.UserId = s.Owner.Id;
        var item = await s.Service.CreateAsync(s.Event.Id, forbidden with { PaidByParticipantId = s.Member.Id }, default);
        Assert.AreEqual(s.Owner.Id, item.AuthorUserId); Assert.AreEqual(s.Member.Id, item.PaidByParticipantId);
        var settlements = new SettlementService(new SettlementRepository(s.Db), s.Actor, TimeProvider.System);
        var transfer = (await settlements.GetAsync(s.Event.Id, default)).Transfers.Single();
        Assert.AreEqual(s.Owner.Id, transfer.FromParticipantId); Assert.AreEqual(s.Member.Id, transfer.ToParticipantId); Assert.AreEqual(3000L, transfer.AmountMinor);
        // The original author retains split-edit rights even when someone else paid.
        item = await s.Service.UpdateSplitAsync(s.Event.Id, item.Id, new("Amount", [new(s.Owner.Id, 3000)], false, item.RowVersion!), default);
        s.Actor.UserId = s.Member.Id;
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdatePayerAsync(s.Event.Id, item.Id, new(s.Member.Id, item.RowVersion!), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdateSplitAsync(s.Event.Id, item.Id, new("Amount", [], true, item.RowVersion!), default));
        s.Actor.UserId = s.Owner.Id;
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdatePayerAsync(s.Event.Id, item.Id, new(s.Stranger.Id, item.RowVersion!), default));
        var changed = await s.Service.UpdatePayerAsync(s.Event.Id, item.Id, new(s.Owner.Id, item.RowVersion!), default);
        Assert.AreEqual(s.Owner.Id, changed.PaidByParticipantId); Assert.AreEqual(s.Owner.Id, changed.AuthorUserId);
        Assert.AreEqual(0, (await settlements.GetAsync(s.Event.Id, default)).Transfers.Count);
        await settlements.SetClosedAsync(s.Event.Id, true, default);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdatePayerAsync(s.Event.Id, item.Id, new(s.Member.Id, changed.RowVersion!), default));
    }
    [TestMethod]
    public async Task Transfer_Marks_Are_Private_Party_Restricted_And_Scoped_To_Version()
    {
        await using var s = await Scope.Create();
        s.Db.EventParticipants.Add(new EventParticipant { Id = s.Stranger.Id, EventId = s.Event.Id, UserId = s.Stranger.Id }); await s.Db.SaveChangesAsync();
        s.Actor.UserId = s.Owner.Id;
        await s.Service.CreateAsync(s.Event.Id, new("Pizza", 3000, "Amount", [new(s.Stranger.Id, 3000)], PaidByParticipantId: s.Member.Id), default);
        var settlements = new SettlementService(new SettlementRepository(s.Db), s.Actor, TimeProvider.System);
        await Assert.ThrowsExactlyAsync<AppException>(() => settlements.MarkAsync(s.Event.Id, new(0, s.Stranger.Id, s.Member.Id, 3000, "Sent", true), default));
        var closed = await settlements.SetClosedAsync(s.Event.Id, true, default);
        Assert.IsNull(closed.Transfers.Single().Acknowledgement);
        await Assert.ThrowsExactlyAsync<AppException>(() => settlements.MarkAsync(s.Event.Id, new(1, s.Stranger.Id, s.Member.Id, 3000, "Sent", true), default));
        s.Actor.UserId = s.Stranger.Id;
        await Assert.ThrowsExactlyAsync<AppException>(() => settlements.MarkAsync(s.Event.Id, new(1, s.Stranger.Id, s.Member.Id, 3000, "Received", true), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => settlements.MarkAsync(s.Event.Id, new(1, s.Stranger.Id, s.Member.Id, 3001, "Sent", true), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => settlements.MarkAsync(s.Event.Id, new(2, s.Stranger.Id, s.Member.Id, 3000, "Sent", true), default));
        var sent = await settlements.MarkAsync(s.Event.Id, new(1, s.Stranger.Id, s.Member.Id, 3000, "Sent", true), default);
        Assert.IsTrue(sent.Transfers.Single().Acknowledgement!.Sent); Assert.IsTrue(sent.Transfers.Single().Acknowledgement!.CanMarkSent);
        s.Actor.UserId = s.Member.Id;
        var received = await settlements.MarkAsync(s.Event.Id, new(1, s.Stranger.Id, s.Member.Id, 3000, "Received", true), default);
        Assert.IsTrue(received.Transfers.Single().Acknowledgement!.Sent); Assert.IsTrue(received.Transfers.Single().Acknowledgement!.Received);
        Assert.IsFalse(received.Transfers.Single().Acknowledgement!.CanMarkSent);
        s.Actor.UserId = s.Owner.Id;
        var hidden = await settlements.GetAsync(s.Event.Id, default);
        Assert.IsNull(hidden.Transfers.Single().Acknowledgement); Assert.IsNull(hidden.PreviousPayments);
        var json = System.Text.Json.JsonSerializer.Serialize(hidden, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.IsFalse(json.Contains("acknowledgement")); Assert.IsFalse(json.Contains("previousPayments"));
        await settlements.SetClosedAsync(s.Event.Id, false, default);
        await settlements.SetClosedAsync(s.Event.Id, true, default);
        s.Actor.UserId = s.Member.Id;
        var newVersion = await settlements.GetAsync(s.Event.Id, default);
        Assert.AreEqual(2, newVersion.Version); Assert.IsFalse(newVersion.Transfers.Single().Acknowledgement!.Sent);
        Assert.IsFalse(newVersion.Transfers.Single().Acknowledgement!.Received);
        Assert.AreEqual(1, newVersion.PreviousPayments!.Count); Assert.IsTrue(newVersion.PreviousPayments[0].Sent);
        // Unchecking one's own current flag is also persisted without changing the other side.
        await settlements.MarkAsync(s.Event.Id, new(2, s.Stranger.Id, s.Member.Id, 3000, "Received", true), default);
        var undone = await settlements.MarkAsync(s.Event.Id, new(2, s.Stranger.Id, s.Member.Id, 3000, "Received", false), default);
        Assert.IsFalse(undone.Transfers.Single().Acknowledgement!.Received);
    }
}
