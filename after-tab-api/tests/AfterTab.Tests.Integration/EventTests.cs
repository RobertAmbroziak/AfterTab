using AfterTab.BL.DTO.Events;
using AfterTab.BL.Services.Events;
using AfterTab.BL.Services.Users;
using AfterTab.Common.Errors;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.EF.Repositories;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Users;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class EventTests
{
    private sealed class Actor : ICurrentUser { public Guid? UserId { get; set; } }
    private sealed class Scope : IAsyncDisposable
    {
        public required SqliteConnection Connection { get; init; }
        public required AppDbContext Db { get; init; }
        public required Actor Actor { get; init; }
        public required EventService Events { get; init; }
        public required UserService Users { get; init; }
        public required User Owner { get; init; }
        public required User Guest { get; init; }
        public required User Stranger { get; init; }
        public static async Task<Scope> Create()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var actor = new Actor();
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options, actor, TimeProvider.System);
            await db.Database.EnsureCreatedAsync();
            var owner = new User { DisplayName = "Ola", DisplayNameConfirmed = true };
            var guest = new User { DisplayName = "Michał", DisplayNameConfirmed = true };
            var stranger = new User { DisplayName = "Michał", DisplayNameConfirmed = true };
            db.Users.AddRange(owner, guest, stranger);
            await db.SaveChangesAsync();
            actor.UserId = owner.Id;
            var repository = new UserRepository(db);
            return new Scope { Connection = connection, Db = db, Actor = actor, Owner = owner, Guest = guest, Stranger = stranger,
                Events = new EventService(new EventRepository(db), repository, actor, TimeProvider.System), Users = new UserService(repository, actor) };
        }
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    [TestMethod]
    public async Task Create_Includes_Owner_And_Hashed_Invite_And_Lists_For_Owner()
    {
        await using var s = await Scope.Create();
        var created = await s.Events.CreateAsync(new("  Pizza w piątek  ", "  Nasza kolacja  "), default);
        Assert.AreEqual("Pizza w piątek", created.Details.Event.Name);
        Assert.AreEqual("Nasza kolacja", created.Details.Event.Description);
        Assert.AreEqual("PLN", created.Details.Event.Currency);
        Assert.IsTrue(created.Details.Event.IsOwner);
        Assert.AreEqual(s.Owner.Id, created.Details.Participants.Single().UserId);
        Assert.AreEqual(1, (await s.Events.ListAsync(default)).Count);
        Assert.AreNotEqual(created.InviteToken, (await s.Db.EventInvites.SingleAsync()).TokenHash);
    }

    [TestMethod]
    public async Task Preview_Does_Not_Join_And_Join_Is_Idempotent_And_Visible_To_Both()
    {
        await using var s = await Scope.Create();
        var created = await s.Events.CreateAsync(new("Weekend"), default);
        s.Actor.UserId = s.Guest.Id;
        Assert.AreEqual(0, (await s.Events.ListAsync(default)).Count);
        Assert.IsFalse((await s.Events.PreviewAsync(created.InviteToken, default)).AlreadyMember);
        Assert.AreEqual(1, await s.Db.EventParticipants.CountAsync());
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.GetAsync(created.Details.Event.Id, default));
        await s.Events.JoinAsync(created.InviteToken, default);
        await s.Events.JoinAsync(created.InviteToken, default);
        Assert.AreEqual(1, (await s.Events.ListAsync(default)).Count);
        Assert.IsTrue((await s.Events.PreviewAsync(created.InviteToken, default)).AlreadyMember);
        Assert.AreEqual(2, await s.Db.EventParticipants.CountAsync());
        s.Actor.UserId = s.Owner.Id;
        var ownerView = await s.Events.GetAsync(created.Details.Event.Id, default);
        Assert.AreEqual(2, ownerView.Participants.Count);
        Assert.IsTrue(ownerView.Participants.Any(x => x.DisplayName == "Michał"));
    }

    [TestMethod]
    public async Task Only_Owner_Manages_Invites_And_Unrelated_Users_Cannot_Read_Event()
    {
        await using var s = await Scope.Create();
        var created = await s.Events.CreateAsync(new("Kolacja"), default);
        s.Actor.UserId = s.Stranger.Id;
        Assert.AreEqual(0, (await s.Events.ListAsync(default)).Count);
        var read = await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.GetAsync(created.Details.Event.Id, default));
        Assert.AreEqual(ErrorKind.NotFound, read.Kind);
        var rotate = await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.RotateInviteAsync(created.Details.Event.Id, default));
        Assert.AreEqual(ErrorKind.Forbidden, rotate.Kind);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.RevokeInviteAsync(created.Details.Event.Id, default));
    }

    [TestMethod]
    public async Task Rotation_And_Revocation_Disable_Links_But_Preserve_Participants()
    {
        await using var s = await Scope.Create();
        var created = await s.Events.CreateAsync(new("Wyjazd"), default);
        s.Actor.UserId = s.Guest.Id;
        await s.Events.JoinAsync(created.InviteToken, default);
        s.Actor.UserId = s.Owner.Id;
        var replacement = await s.Events.RotateInviteAsync(created.Details.Event.Id, default);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.PreviewAsync(created.InviteToken, default));
        await s.Events.RevokeInviteAsync(created.Details.Event.Id, default);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.PreviewAsync(replacement.Token, default));
        s.Actor.UserId = s.Guest.Id;
        Assert.AreEqual(2, (await s.Events.GetAsync(created.Details.Event.Id, default)).Participants.Count);
        Assert.AreEqual(1, (await s.Events.ListAsync(default)).Count);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.JoinAsync(replacement.Token, default));
    }

    [TestMethod]
    public async Task Expired_Closed_And_Removed_Memberships_Cannot_Join()
    {
        await using var s = await Scope.Create();
        var created = await s.Events.CreateAsync(new("Spotkanie"), default);
        var invite = await s.Db.EventInvites.SingleAsync();
        invite.ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await s.Db.SaveChangesAsync();
        s.Actor.UserId = s.Guest.Id;
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.JoinAsync(created.InviteToken, default));
        invite = await s.Db.EventInvites.SingleAsync();
        invite.ExpiresAtUtc = null;
        var ev = await s.Db.Events.SingleAsync();
        ev.Status = EventStatus.Closed;
        await s.Db.SaveChangesAsync();
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.JoinAsync(created.InviteToken, default));
        ev = await s.Db.Events.SingleAsync(); ev.Status = EventStatus.Active;
        await s.Db.SaveChangesAsync();
        await s.Events.JoinAsync(created.InviteToken, default);
        var participant = await s.Db.EventParticipants.SingleAsync(x => x.UserId == s.Guest.Id);
        participant.Status = ParticipantStatus.Removed;
        await s.Db.SaveChangesAsync();
        var removed = await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.JoinAsync(created.InviteToken, default));
        Assert.AreEqual(ErrorKind.Forbidden, removed.Kind);
    }

    [TestMethod]
    public async Task Name_Confirmation_Is_Required_And_Renaming_Preserves_Event_Membership()
    {
        await using var s = await Scope.Create();
        s.Owner.DisplayNameConfirmed = false;
        await s.Db.SaveChangesAsync();
        Assert.IsTrue((await s.Users.GetCurrentAsync())!.RequiresDisplayName);
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.CreateAsync(new("Nowe"), default));
        foreach (var invalid in new[] { " ", "Anna\nMaria", new string('a', 101) })
            await Assert.ThrowsExactlyAsync<AppException>(() => s.Users.UpdateDisplayNameAsync(invalid));
        var renamed = await s.Users.UpdateDisplayNameAsync("  Aleksandra  ");
        Assert.AreEqual(s.Owner.Id, renamed.Id);
        Assert.AreEqual("Aleksandra", renamed.DisplayName);
        Assert.IsFalse(renamed.RequiresDisplayName);
        var created = await s.Events.CreateAsync(new("Imieniny"), default);
        await s.Users.UpdateDisplayNameAsync("Ola");
        var details = await s.Events.GetAsync(created.Details.Event.Id, default);
        Assert.AreEqual("Ola", details.Event.OwnerName);
        Assert.AreEqual("Ola", details.Participants.Single().DisplayName);
        Assert.AreEqual(1, (await s.Events.ListAsync(default)).Count);
    }

    [TestMethod]
    public async Task Invalid_Event_And_Failed_Aggregate_Do_Not_Persist_Partial_Data()
    {
        await using var s = await Scope.Create();
        await Assert.ThrowsExactlyAsync<AppException>(() => s.Events.CreateAsync(new("  "), default));
        var ev = new Event { OwnerUserId = s.Owner.Id, Name = "Rollback" };
        await Assert.ThrowsExactlyAsync<DbUpdateException>(() => new EventRepository(s.Db).CreateAsync(ev,
            new EventParticipant { EventId = ev.Id, UserId = Guid.NewGuid() }, new EventInvite { EventId = ev.Id, TokenHash = new string('A', 64) }, default));
        s.Db.ChangeTracker.Clear();
        Assert.AreEqual(0, await s.Db.Events.CountAsync());
        Assert.AreEqual(0, await s.Db.EventInvites.CountAsync());
        Assert.AreEqual(0, await s.Db.EventParticipants.CountAsync());
    }
}
