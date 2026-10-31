using AfterTab.BL.DTO.Events;
using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Services.Events;
using AfterTab.BL.Services.Expenses;
using AfterTab.Common.Errors;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.EF.Repositories;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class VirtualParticipantTests
{
    private sealed class Actor : ICurrentUser { public Guid? UserId { get; set; } }
    private sealed class Scope : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required AppDbContext Db { get; init; }
        public required Actor Actor { get; init; }
        public required User Owner { get; init; }
        public required User Member { get; init; }
        public required User Stranger { get; init; }
        public required CreatedEventDto Created { get; init; }
        public Guid EventId => Created.Details.Event.Id;
        public EventService Events => new(new EventRepository(Db), new UserRepository(Db), Actor, TimeProvider.System);
        public ExpenseService Expenses => new(new ExpenseRepository(Db), new UserRepository(Db), Actor, TimeProvider.System);
        public SettlementService Settlements => new(new SettlementRepository(Db), Actor, TimeProvider.System);
        public static async Task<Scope> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var actor = new Actor();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection)
                .ReplaceService<IModelCustomizer, ExpenseTests.SqliteModel>().Options, actor, TimeProvider.System);
            await db.Database.EnsureCreatedAsync();
            var owner = new User { DisplayName = "Robert", DisplayNameConfirmed = true };
            var member = new User { DisplayName = "Grzegorz", DisplayNameConfirmed = true };
            var stranger = new User { DisplayName = "Obcy", DisplayNameConfirmed = true };
            db.Users.AddRange(owner, member, stranger); await db.SaveChangesAsync(); actor.UserId = owner.Id;
            var service = new EventService(new EventRepository(db), new UserRepository(db), actor, TimeProvider.System);
            var created = await service.CreateAsync(new("Pizza"), default);
            return new() { Connection = connection, Db = db, Actor = actor, Owner = owner, Member = member, Stranger = stranger, Created = created };
        }
        public async Task<EventMemberDto> Virtual(string name = "Grzegorz")
        {
            var before = (await Events.GetAsync(EventId, default)).Participants.Select(x => x.ParticipantId).ToHashSet();
            return (await Events.AddVirtualAsync(EventId, new(name), default)).Participants.Single(x => !before.Contains(x.ParticipantId));
        }
        public async Task<EventMemberDto> Join()
        {
            Actor.UserId = Member.Id;
            return (await Events.JoinAsync(Created.InviteToken, default)).Participants.Single(x => x.UserId == Member.Id);
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    [TestMethod]
    public async Task Virtual_People_Have_No_Accounts_And_Are_Included_In_Exact_Splits_And_Settlement()
    {
        await using var s = await Scope.Create();
        var owner = s.Created.Details.Participants.Single();
        Assert.AreNotEqual(s.Owner.Id, owner.ParticipantId);
        var guest = await s.Virtual(); var another = await s.Virtual("Grzegorz");
        Assert.IsTrue(guest.IsVirtual); Assert.IsNull(guest.UserId);
        Assert.AreNotEqual(guest.ParticipantId, another.ParticipantId);
        Assert.AreEqual(3, await s.Db.Users.CountAsync());
        Assert.AreEqual(0, await s.Db.Sessions.CountAsync());
        var pizza = await s.Expenses.CreateAsync(s.EventId, new("Pizza", 6250, "Percentage",
            [new(owner.ParticipantId, PercentageBps: 3300), new(guest.ParticipantId, PercentageBps: 3300), new(another.ParticipantId, PercentageBps: 3300)], BalanceParticipantId: owner.ParticipantId), default);
        Assert.AreEqual(0L, pizza.OthersAmountMinor); Assert.AreEqual(6250L, pizza.Splits.Sum(x => x.AmountMinor));
        var closed = await s.Settlements.SetClosedAsync(s.EventId, true, default);
        Assert.AreEqual(3, closed.Balances.Count); Assert.AreEqual(0L, closed.Balances.Sum(x => x.NetMinor));
        Assert.AreEqual(2, closed.Transfers.Count);
        Assert.IsTrue(closed.Transfers.All(x => x.ToParticipantId == owner.ParticipantId));
        Assert.IsTrue(closed.Transfers.All(x => x.Acknowledgement!.CanMarkReceived && !x.Acknowledgement.CanMarkSent));
        var transfer = closed.Transfers.First();
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Settlements.MarkAsync(s.EventId,
            new(1, transfer.FromParticipantId, transfer.ToParticipantId, transfer.AmountMinor, "Sent", true), default));
        var marked = await s.Settlements.MarkAsync(s.EventId,
            new(1, transfer.FromParticipantId, transfer.ToParticipantId, transfer.AmountMinor, "Received", true), default);
        Assert.IsTrue(marked.Transfers.Single(x => x.FromParticipantId == transfer.FromParticipantId).Acknowledgement!.Received);
    }

    [TestMethod]
    public async Task Owner_Can_Set_Virtual_Payer_But_Member_Cannot_Impersonate_Them()
    {
        await using var s = await Scope.Create();
        var guest = await s.Virtual(); var member = await s.Join();
        var input = new CreateExpenseDto("Napoje", 1000, "Amount", [new(member.ParticipantId, 1000)], PaidByParticipantId: guest.ParticipantId);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Expenses.CreateAsync(s.EventId, input, default));
        s.Actor.UserId = s.Owner.Id;
        var expense = await s.Expenses.CreateAsync(s.EventId, input, default);
        Assert.AreEqual(s.Owner.Id, expense.AuthorUserId); Assert.AreEqual(guest.ParticipantId, expense.PaidByParticipantId);
        var transfer = (await s.Settlements.GetAsync(s.EventId, default)).Transfers.Single();
        Assert.AreEqual(member.ParticipantId, transfer.FromParticipantId); Assert.AreEqual(guest.ParticipantId, transfer.ToParticipantId);
        Assert.IsNull(transfer.Acknowledgement);
        s.Actor.UserId = s.Member.Id;
        Assert.AreEqual(1000L, (await s.Expenses.ListAsync(s.EventId, default)).Single().AmountMinor);
    }

    [TestMethod]
    public async Task Link_Merges_Overlapping_Splits_And_Payers_Without_Losing_Amounts_Authors_Or_Access()
    {
        await using var s = await Scope.Create();
        var guest = await s.Virtual("Grzesiek bez telefonu"); var member = await s.Join(); s.Actor.UserId = s.Owner.Id;
        var owner = s.Created.Details.Participants.Single();
        var pizza = await s.Expenses.CreateAsync(s.EventId, new("Pizza", 10000, "Percentage",
            [new(guest.ParticipantId, PercentageBps: 6000), new(member.ParticipantId, PercentageBps: 4000)]), default);
        var drinks = await s.Expenses.CreateAsync(s.EventId, new("Napoje", 7000, "Amount", [new(owner.ParticipantId, 7000)], PaidByParticipantId: member.ParticipantId), default);
        var result = await s.Events.LinkVirtualAsync(s.EventId, guest.ParticipantId, new(member.ParticipantId), default);
        Assert.AreEqual(2, result.Participants.Count);
        var connected = result.Participants.Single(x => x.UserId == s.Member.Id);
        Assert.AreEqual(guest.ParticipantId, connected.ParticipantId); Assert.IsFalse(connected.IsVirtual);
        var rows = await s.Expenses.ListAsync(s.EventId, default);
        var split = rows.Single(x => x.Id == pizza.Id).Splits.Single();
        Assert.AreEqual(guest.ParticipantId, split.ParticipantId); Assert.AreEqual(10000L, split.AmountMinor); Assert.AreEqual(10000, split.PercentageBps);
        Assert.AreEqual(guest.ParticipantId, rows.Single(x => x.Id == drinks.Id).PaidByParticipantId);
        Assert.IsTrue(rows.All(x => x.AuthorUserId == s.Owner.Id));
        Assert.AreEqual(17000L, rows.Sum(x => x.AmountMinor)); Assert.AreEqual(17000L, rows.SelectMany(x => x.Splits).Sum(x => x.AmountMinor));
        Assert.AreEqual(3, await s.Db.Users.CountAsync());
        Assert.AreEqual(guest.ParticipantId, (await s.Db.EventParticipants.AsNoTracking().SingleAsync(x => x.Id == member.ParticipantId)).MergedIntoParticipantId);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.LinkVirtualAsync(s.EventId, guest.ParticipantId, new(member.ParticipantId), default));
        s.Actor.UserId = s.Member.Id;
        Assert.AreEqual(2, (await s.Events.GetAsync(s.EventId, default)).Participants.Count);
        Assert.AreEqual(2, (await s.Expenses.ListAsync(s.EventId, default)).Count);
        var settlement = await s.Settlements.GetAsync(s.EventId, default);
        Assert.AreEqual(0L, settlement.Balances.Sum(x => x.NetMinor)); Assert.AreEqual(3000L, settlement.Transfers.Single().AmountMinor);
        Assert.AreEqual(guest.ParticipantId, settlement.Transfers.Single().FromParticipantId);
        Assert.AreEqual(guest.ParticipantId, (await s.Events.JoinAsync(s.Created.InviteToken, default)).Participants.Single(x => x.UserId == s.Member.Id).ParticipantId);
    }

    [TestMethod]
    public async Task Management_Requires_Owner_Open_Event_And_Account_Member_Of_The_Same_Event()
    {
        await using var s = await Scope.Create();
        var guest = await s.Virtual(); var member = await s.Join();
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.AddVirtualAsync(s.EventId, new("Kamil"), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.RenameVirtualAsync(s.EventId, guest.ParticipantId, new("Kamil"), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.RemoveVirtualAsync(s.EventId, guest.ParticipantId, default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.LinkVirtualAsync(s.EventId, guest.ParticipantId, new(member.ParticipantId), default));
        s.Actor.UserId = s.Owner.Id;
        var other = await s.Events.CreateAsync(new("Inne"), default);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.LinkVirtualAsync(s.EventId, guest.ParticipantId, new(other.Details.Participants.Single().ParticipantId), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.LinkVirtualAsync(s.EventId, guest.ParticipantId, new(guest.ParticipantId), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.AddVirtualAsync(s.EventId, new("   "), default));
        await s.Settlements.SetClosedAsync(s.EventId, true, default);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.AddVirtualAsync(s.EventId, new("Kamil"), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.LinkVirtualAsync(s.EventId, guest.ParticipantId, new(member.ParticipantId), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.RenameVirtualAsync(s.EventId, guest.ParticipantId, new("Kamil"), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.RemoveVirtualAsync(s.EventId, guest.ParticipantId, default));
    }

    [TestMethod]
    public async Task Rename_Preserves_Debt_And_Only_Unused_Virtual_Participant_Can_Be_Removed()
    {
        await using var s = await Scope.Create();
        var guest = await s.Virtual(); var unused = await s.Virtual("Nieobecny");
        await s.Expenses.CreateAsync(s.EventId, new("Pizza", 3000, "Amount", [new(guest.ParticipantId, 3000)]), default);
        await s.Events.RenameVirtualAsync(s.EventId, guest.ParticipantId, new("Grzesiek"), default);
        Assert.AreEqual("Grzesiek", (await s.Expenses.ListAsync(s.EventId, default)).Single().Splits.Single().DisplayName);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.RemoveVirtualAsync(s.EventId, guest.ParticipantId, default));
        await s.Events.RemoveVirtualAsync(s.EventId, unused.ParticipantId, default);
        Assert.AreEqual(2, (await s.Events.GetAsync(s.EventId, default)).Participants.Count);
        Assert.AreEqual(3000L, (await s.Settlements.GetAsync(s.EventId, default)).Transfers.Single().AmountMinor);
    }

    [TestMethod]
    public async Task Linking_After_Reopen_Preserves_Private_Payment_History_And_Author_Edit_Rights()
    {
        await using var s = await Scope.Create();
        var guest = await s.Virtual(); var member = await s.Join();
        var item = await s.Expenses.CreateAsync(s.EventId, new("Pizza", 3000, "Amount", [new(guest.ParticipantId, 3000)]), default);
        s.Actor.UserId = s.Owner.Id;
        await s.Settlements.SetClosedAsync(s.EventId, true, default);
        s.Actor.UserId = s.Member.Id;
        await s.Settlements.MarkAsync(s.EventId, new(1, guest.ParticipantId, member.ParticipantId, 3000, "Received", true), default);
        s.Actor.UserId = s.Owner.Id; await s.Settlements.SetClosedAsync(s.EventId, false, default);
        await s.Events.LinkVirtualAsync(s.EventId, guest.ParticipantId, new(member.ParticipantId), default);
        Assert.IsNull((await s.Settlements.GetAsync(s.EventId, default)).PreviousPayments);
        s.Actor.UserId = s.Member.Id;
        var personal = await s.Settlements.GetAsync(s.EventId, default);
        Assert.AreEqual(1, personal.PreviousPayments!.Count); Assert.IsTrue(personal.PreviousPayments.Single().Received);
        Assert.AreEqual(0, personal.Transfers.Count);
        var saved = (await s.Expenses.ListAsync(s.EventId, default)).Single();
        Assert.AreEqual(s.Member.Id, saved.AuthorUserId);
        var updated = await s.Expenses.UpdateSplitAsync(s.EventId, item.Id, new("Amount", [new(guest.ParticipantId, 3000)], false, saved.RowVersion!), default);
        Assert.AreEqual(3000L, updated.Splits.Single().AmountMinor);
    }
}
