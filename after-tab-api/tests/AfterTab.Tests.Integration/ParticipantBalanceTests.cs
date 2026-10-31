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
public sealed class ParticipantBalanceTests
{
    private sealed class Actor : ICurrentUser { public Guid? UserId { get; set; } }
    private sealed class Scope : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required AppDbContext Db { get; init; }
        public required Actor Actor { get; init; }
        public required User Owner { get; init; }
        public required User Author { get; init; }
        public required User Payer { get; init; }
        public required User Other { get; init; }
        public required Event Event { get; init; }
        public required Dictionary<Guid, Guid> Participants { get; init; }
        public required ExpenseDto Item { get; set; }
        public ExpenseService Service => new(new ExpenseRepository(Db), new UserRepository(Db), Actor, TimeProvider.System);
        public static async Task<Scope> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            var actor = new Actor();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection)
                .ReplaceService<IModelCustomizer, ExpenseTests.SqliteModel>().Options, actor, TimeProvider.System);
            await db.Database.EnsureCreatedAsync();
            var owner = new User { DisplayName = "Robert", DisplayNameConfirmed = true };
            var author = new User { DisplayName = "Tomek", DisplayNameConfirmed = true };
            var payer = new User { DisplayName = "Magda", DisplayNameConfirmed = true };
            var other = new User { DisplayName = "Kamil", DisplayNameConfirmed = true };
            db.Users.AddRange(owner, author, payer, other); await db.SaveChangesAsync();
            var ev = new Event { Name = "Pizza", OwnerUserId = owner.Id }; db.Events.Add(ev);
            var members = new[] { owner, author, payer, other }.Select(user => new EventParticipant { EventId = ev.Id, UserId = user.Id }).ToArray();
            db.EventParticipants.AddRange(members); await db.SaveChangesAsync();
            var ids = members.ToDictionary(x => x.UserId!.Value, x => x.Id);
            actor.UserId = author.Id;
            var service = new ExpenseService(new ExpenseRepository(db), new UserRepository(db), actor, TimeProvider.System);
            var item = await service.CreateAsync(ev.Id, new("Pizza", 6250, "Percentage",
                [new(ids[owner.Id], PercentageBps: 3300), new(ids[author.Id], PercentageBps: 3300), new(ids[payer.Id], PercentageBps: 3300)]), default);
            actor.UserId = owner.Id;
            item = await service.UpdatePayerAsync(ev.Id, item.Id, new(ids[payer.Id], item.RowVersion!), default);
            return new() { Connection = connection, Db = db, Actor = actor, Owner = owner, Author = author, Payer = payer, Other = other, Event = ev, Participants = ids, Item = item };
        }
        public async Task<ExpenseDto> Balance(Guid? participant)
        {
            Item = await Service.UpdateBalanceAsync(Event.Id, Item.Id, new(participant, Item.RowVersion!), default);
            return Item;
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    [TestMethod]
    public async Task Owner_Can_Move_And_Undo_Persisted_Adjustment_Without_Changing_Base_Shares_Or_Author()
    {
        await using var s = await Scope.Create();
        var first = await s.Balance(s.Participants[s.Owner.Id]);
        Assert.AreEqual(64L, first.BalanceAmountMinor); Assert.AreEqual(0L, first.OthersAmountMinor);
        Assert.AreEqual(2126L, first.Splits.Single(x => x.ParticipantId == s.Participants[s.Owner.Id]).AmountMinor);
        var moved = await s.Balance(s.Participants[s.Payer.Id]);
        Assert.AreEqual(2062L, moved.Splits.Single(x => x.ParticipantId == s.Participants[s.Owner.Id]).AmountMinor);
        Assert.AreEqual(2126L, moved.Splits.Single(x => x.ParticipantId == s.Participants[s.Payer.Id]).AmountMinor);
        Assert.AreEqual(s.Author.Id, moved.AuthorUserId); Assert.AreEqual("Percentage", moved.SplitMethod);
        Assert.IsTrue(moved.Splits.All(x => x.PercentageBps == 3300));
        var again = await s.Balance(s.Participants[s.Author.Id]);
        Assert.AreEqual(6250L, again.Splits.Sum(x => x.AmountMinor));
        var undone = await s.Balance(null);
        Assert.IsFalse(undone.Balanced); Assert.IsNull(undone.BalanceParticipantId); Assert.AreEqual(0L, undone.BalanceAmountMinor);
        Assert.AreEqual(64L, undone.OthersAmountMinor); Assert.IsTrue(undone.Splits.All(x => x.AmountMinor == 2062));
        Assert.AreEqual(3, await s.Db.ExpenseSplits.CountAsync());
    }

    [TestMethod]
    public async Task Payer_And_Event_Owner_Can_Balance_But_Author_And_Other_Member_Cannot()
    {
        await using var s = await Scope.Create();
        foreach (var denied in new[] { s.Author.Id, s.Other.Id })
        {
            s.Actor.UserId = denied;
            var error = await Assert.ThrowsExactlyAsync<AppException>(() => s.Balance(s.Participants[s.Owner.Id]));
            Assert.AreEqual(ErrorKind.Forbidden, error.Kind);
        }
        s.Actor.UserId = s.Payer.Id;
        await s.Balance(s.Participants[s.Author.Id]);
        // Balancing does not grant the payer permission to rewrite the original split.
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdateSplitAsync(s.Event.Id, s.Item.Id,
            new("Amount", [new(s.Participants[s.Author.Id], 6250)], false, s.Item.RowVersion!), default));
        s.Actor.UserId = s.Owner.Id; await s.Balance(s.Participants[s.Payer.Id]);
        s.Actor.UserId = s.Author.Id;
        // The old split endpoint must not bypass the balancing permission.
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdateSplitAsync(s.Event.Id, s.Item.Id,
            new("Amount", [new(s.Participants[s.Author.Id], 6000)], false, s.Item.RowVersion!, s.Participants[s.Author.Id]), default));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdateSplitAsync(s.Event.Id, s.Item.Id,
            new("Amount", [new(s.Participants[s.Author.Id], 6250)], false, s.Item.RowVersion!), default));
    }

    [TestMethod]
    public async Task Unselected_Person_Stale_Version_And_Closed_Event_Are_Rejected()
    {
        await using var s = await Scope.Create();
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Balance(s.Participants[s.Other.Id]));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Balance(Guid.NewGuid()));
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdateBalanceAsync(s.Event.Id, s.Item.Id,
            new(s.Participants[s.Owner.Id], "Ag=="), default));
        await s.Balance(s.Participants[s.Owner.Id]);
        await new SettlementService(new SettlementRepository(s.Db), s.Actor, TimeProvider.System).SetClosedAsync(s.Event.Id, true, default);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Balance(s.Participants[s.Payer.Id]));
        var saved = (await s.Service.ListAsync(s.Event.Id, default)).Single();
        Assert.AreEqual(64L, saved.BalanceAmountMinor); Assert.AreEqual(s.Participants[s.Owner.Id], saved.BalanceParticipantId);
    }

    [TestMethod]
    public async Task Legacy_Balanced_Item_Allows_Author_To_Reenter_Base_Shares_Without_Granting_Balance_Permission()
    {
        await using var s = await Scope.Create();
        var legacy = await s.Db.Expenses.SingleAsync();
        legacy.Balanced = true; await s.Db.SaveChangesAsync();
        s.Actor.UserId = s.Author.Id;
        var corrected = await s.Service.UpdateSplitAsync(s.Event.Id, s.Item.Id,
            new("Amount", [new(s.Participants[s.Author.Id], 3000), new(s.Participants[s.Payer.Id], 2000)], false, s.Item.RowVersion!), default);
        Assert.AreEqual(1250L, corrected.OthersAmountMinor); Assert.IsFalse(corrected.Balanced);
        Assert.IsNull(corrected.BalanceParticipantId);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Service.UpdateBalanceAsync(s.Event.Id, s.Item.Id,
            new(s.Participants[s.Author.Id], corrected.RowVersion!), default));
        s.Actor.UserId = s.Payer.Id;
        var balanced = await s.Service.UpdateBalanceAsync(s.Event.Id, s.Item.Id,
            new(s.Participants[s.Author.Id], corrected.RowVersion!), default);
        Assert.AreEqual(4250L, balanced.Splits.Single(x => x.ParticipantId == s.Participants[s.Author.Id]).AmountMinor);
    }

    [TestMethod]
    public async Task Linking_Virtual_Adjustment_Target_Preserves_Amount_And_Remains_Movable()
    {
        await using var s = await Scope.Create();
        var events = new EventService(new EventRepository(s.Db), new UserRepository(s.Db), s.Actor, TimeProvider.System);
        var details = await events.AddVirtualAsync(s.Event.Id, new("Bez telefonu"), default);
        var virtualPerson = details.Participants.Single(x => x.IsVirtual);
        var item = await s.Service.CreateAsync(s.Event.Id, new("Napoje", 1000, "Amount",
            [new(virtualPerson.ParticipantId, 300), new(s.Participants[s.Other.Id], 500)], BalanceParticipantId: s.Participants[s.Other.Id]), default);
        await events.LinkVirtualAsync(s.Event.Id, virtualPerson.ParticipantId, new(s.Participants[s.Other.Id]), default);
        var linked = (await s.Service.ListAsync(s.Event.Id, default)).Single(x => x.Id == item.Id);
        Assert.AreEqual(virtualPerson.ParticipantId, linked.BalanceParticipantId); Assert.AreEqual(200L, linked.BalanceAmountMinor);
        Assert.AreEqual(1000L, linked.Splits.Single().AmountMinor);
        var undone = await s.Service.UpdateBalanceAsync(s.Event.Id, item.Id, new(null, linked.RowVersion!), default);
        Assert.AreEqual(800L, undone.Splits.Single().AmountMinor); Assert.AreEqual(200L, undone.OthersAmountMinor);
    }
}
