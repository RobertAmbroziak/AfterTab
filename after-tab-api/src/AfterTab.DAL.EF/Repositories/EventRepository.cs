using System.Data;
using AfterTab.Common.Errors;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Interfaces.Events;
using Microsoft.EntityFrameworkCore;

namespace AfterTab.DAL.EF.Repositories;

public sealed class EventRepository(AppDbContext db) : IEventRepository
{
    private IQueryable<EventReadModel> ReadQuery(IQueryable<Event> source)
        => from ev in source
           join owner in db.Users.AsNoTracking() on ev.OwnerUserId equals owner.Id
           select new EventReadModel(ev, owner.DisplayName,
               db.EventParticipants.Count(p => p.EventId == ev.Id && p.Status == ParticipantStatus.Active));

    public async Task<IReadOnlyList<EventReadModel>> ListAsync(Guid userId, CancellationToken ct)
        => await ReadQuery(db.Events.AsNoTracking().Where(ev => ev.OwnerUserId == userId || db.EventParticipants.Any(p =>
            p.EventId == ev.Id && p.UserId == userId && p.Status == ParticipantStatus.Active))
            .OrderBy(ev => ev.Name).ThenBy(ev => ev.Id)).ToListAsync(ct);

    public Task<EventReadModel?> GetAsync(Guid id, CancellationToken ct)
        => ReadQuery(db.Events.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
    public Task<bool> IsMemberAsync(Guid eventId, Guid userId, CancellationToken ct)
        => db.EventParticipants.AnyAsync(p => p.EventId == eventId && p.UserId == userId && p.Status == ParticipantStatus.Active, ct);
    public async Task<IReadOnlyList<User>> ParticipantsAsync(Guid eventId, CancellationToken ct)
        => await (from p in db.EventParticipants.AsNoTracking()
                  join user in db.Users.AsNoTracking() on p.UserId equals user.Id
                  where p.EventId == eventId && p.Status == ParticipantStatus.Active
                  orderby user.DisplayName, user.Id select user).ToListAsync(ct);

    public async Task CreateAsync(Event ev, EventParticipant owner, EventInvite invite, CancellationToken ct)
    {
        db.Events.Add(ev);
        db.EventParticipants.Add(owner);
        db.EventInvites.Add(invite);
        await db.SaveChangesAsync(ct);
    }
    public Task<EventInvite?> FindInviteAsync(string hash, CancellationToken ct)
        => db.EventInvites.AsNoTracking().SingleOrDefaultAsync(x => x.TokenHash == hash, ct);

    public Task<Guid> JoinAsync(string hash, Guid userId, DateTimeOffset now, CancellationToken ct)
        => InTransaction(async () =>
        {
            var invite = await db.EventInvites.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
            if (invite is null || invite.RevokedAtUtc is not null || invite.ExpiresAtUtc <= now)
                throw new AppException(ErrorKind.NotFound, "Zaproszenie jest nieprawidłowe, wygasło lub zostało unieważnione.");
            var ev = await db.Events.SingleAsync(x => x.Id == invite.EventId, ct);
            if (ev.Status != EventStatus.Active)
                throw new AppException(ErrorKind.Conflict, "To wydarzenie nie przyjmuje już uczestników.");
            var participant = await db.EventParticipants.SingleOrDefaultAsync(x => x.EventId == ev.Id && x.UserId == userId, ct);
            if (participant?.Status == ParticipantStatus.Removed)
                throw new AppException(ErrorKind.Forbidden, "Autor usunął Cię z tego wydarzenia.");
            if (participant is null)
                db.EventParticipants.Add(new EventParticipant { EventId = ev.Id, UserId = userId, JoinedAtUtc = now });
            else if (participant.Status == ParticipantStatus.Left)
            {
                participant.Status = ParticipantStatus.Active;
                participant.JoinedAtUtc = now;
                participant.LeftAtUtc = null;
            }
            await db.SaveChangesAsync(ct);
            return ev.Id;
        }, ct);

    public Task RotateInviteAsync(Guid eventId, Guid ownerId, EventInvite? replacement, DateTimeOffset now, CancellationToken ct)
        => InTransaction(async () =>
        {
            var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct)
                ?? throw new AppException(ErrorKind.NotFound, "Nie znaleziono wydarzenia.");
            if (ev.OwnerUserId != ownerId) throw new AppException(ErrorKind.Forbidden, "Tylko autor może zarządzać zaproszeniem.");
            if (replacement is not null && ev.Status != EventStatus.Active)
                throw new AppException(ErrorKind.Conflict, "To wydarzenie nie przyjmuje już uczestników.");
            var previous = await db.EventInvites.Where(x => x.EventId == eventId && x.RevokedAtUtc == null).ToListAsync(ct);
            foreach (var invite in previous) invite.RevokedAtUtc = now;
            if (replacement is not null) db.EventInvites.Add(replacement);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

    private Task<T> InTransaction<T>(Func<Task<T>> operation, CancellationToken ct)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Retries must reload state after a rolled-back deadlock or transient failure.
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await operation();
            await transaction.CommitAsync(ct);
            return result;
        });
}
