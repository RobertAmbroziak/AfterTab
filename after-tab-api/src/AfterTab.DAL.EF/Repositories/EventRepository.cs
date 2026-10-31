using System.Data;
using AfterTab.Common.Errors;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Interfaces.Events;
using AfterTab.DAL.Interfaces.Expenses;
using Microsoft.EntityFrameworkCore;

namespace AfterTab.DAL.EF.Repositories;

public sealed class EventRepository(AppDbContext db) : IEventRepository
{
    private IQueryable<EventReadModel> ReadQuery(IQueryable<Event> source)
        => from ev in source
           join owner in db.Users.AsNoTracking() on ev.OwnerUserId equals owner.Id
           select new EventReadModel(ev, owner.DisplayName,
               db.EventParticipants.Count(p => p.EventId == ev.Id && p.Status == ParticipantStatus.Active));

    public async Task<(IReadOnlyList<EventReadModel> Items, int TotalCount)> ListAsync(Guid userId, int page, CancellationToken ct)
    {
        var query = db.Events.AsNoTracking().Where(ev => ev.OwnerUserId == userId || db.EventParticipants.Any(p =>
            p.EventId == ev.Id && p.UserId == userId && p.Status == ParticipantStatus.Active));
        var count = await query.CountAsync(ct);
        var items = await ReadQuery(query.OrderByDescending(ev => ev.CreatedAtUtc).ThenByDescending(ev => ev.Id)
            .Skip((page - 1) * 10).Take(10)).ToListAsync(ct);
        return (items, count);
    }

    public Task<EventReadModel?> GetAsync(Guid id, CancellationToken ct)
        => ReadQuery(db.Events.AsNoTracking().Where(x => x.Id == id)).SingleOrDefaultAsync(ct);
    public Task<bool> IsMemberAsync(Guid eventId, Guid userId, CancellationToken ct)
        => db.EventParticipants.AnyAsync(p => p.EventId == eventId && p.UserId == userId && p.Status == ParticipantStatus.Active, ct);
    public async Task<IReadOnlyList<ExpenseParticipant>> ParticipantsAsync(Guid eventId, CancellationToken ct)
        => (await new ExpenseRepository(db).GetEventAsync(eventId, ct))?.Participants ?? [];

    private async Task<Event> OwnedOpen(Guid eventId, Guid ownerId, CancellationToken ct)
    {
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct)
            ?? throw new AppException(ErrorKind.NotFound, "Nie znaleziono wydarzenia.");
        if (ev.OwnerUserId != ownerId) throw new AppException(ErrorKind.Forbidden, "Tylko autor wydarzenia może zarządzać uczestnikami bez konta.");
        if (ev.Status != EventStatus.Active) throw new AppException(ErrorKind.Conflict, "Najpierw otwórz wydarzenie.");
        return ev;
    }
    private async Task<EventParticipant> Virtual(Guid eventId, Guid participantId, CancellationToken ct)
        => await db.EventParticipants.SingleOrDefaultAsync(x => x.EventId == eventId && x.Id == participantId
            && x.UserId == null && x.Status == ParticipantStatus.Active && x.MergedIntoParticipantId == null, ct)
            ?? throw new AppException(ErrorKind.Conflict, "Uczestnik bez konta nie jest już dostępny. Odśwież wydarzenie.");
    public Task AddVirtualAsync(Guid eventId, Guid ownerId, string name, DateTimeOffset now, CancellationToken ct)
        => InTransaction(async () =>
        {
            await OwnedOpen(eventId, ownerId, ct);
            if (await db.EventParticipants.CountAsync(x => x.EventId == eventId && x.Status == ParticipantStatus.Active, ct) >= 1000)
                throw new AppException(ErrorKind.Validation, "Wydarzenie może mieć najwyżej 1000 uczestników.");
            db.EventParticipants.Add(new EventParticipant { EventId = eventId, DisplayName = name, JoinedAtUtc = now });
            await db.SaveChangesAsync(ct); return true;
        }, ct);
    public Task RenameVirtualAsync(Guid eventId, Guid ownerId, Guid participantId, string name, CancellationToken ct)
        => InTransaction(async () =>
        {
            await OwnedOpen(eventId, ownerId, ct);
            (await Virtual(eventId, participantId, ct)).DisplayName = name;
            await db.SaveChangesAsync(ct); return true;
        }, ct);
    public Task RemoveVirtualAsync(Guid eventId, Guid ownerId, Guid participantId, CancellationToken ct)
        => InTransaction(async () =>
        {
            await OwnedOpen(eventId, ownerId, ct);
            var participant = await Virtual(eventId, participantId, ct);
            if (await db.ExpenseSplits.AnyAsync(x => x.ParticipantId == participantId, ct)
                || await db.Expenses.AnyAsync(x => x.PaidByParticipantId == participantId, ct)
                || await db.TransferConfirmations.AnyAsync(x => x.FromParticipantId == participantId || x.ToParticipantId == participantId, ct))
                throw new AppException(ErrorKind.Conflict, "Uczestnik ma już wydatki lub podziały. Najpierw je popraw albo połącz go z osobą, która dołączyła.");
            participant.Status = ParticipantStatus.Removed;
            await db.SaveChangesAsync(ct); return true;
        }, ct);
    public Task LinkVirtualAsync(Guid eventId, Guid ownerId, Guid participantId, Guid targetParticipantId, CancellationToken ct)
        => InTransaction(async () =>
        {
            await OwnedOpen(eventId, ownerId, ct);
            var source = await Virtual(eventId, participantId, ct);
            var target = await db.EventParticipants.SingleOrDefaultAsync(x => x.Id == targetParticipantId && x.EventId == eventId
                && x.Status == ParticipantStatus.Active && x.UserId != null, ct)
                ?? throw new AppException(ErrorKind.Validation, "Wybierz osobę z kontem, która już dołączyła do tego wydarzenia.");
            var user = await db.Users.SingleAsync(x => x.Id == target.UserId, ct);
            if (user.Status != UserStatus.Active) throw new AppException(ErrorKind.Conflict, "Konto uczestnika jest nieaktywne.");
            var affected = await db.Expenses.Include(x => x.Splits).Where(x => x.EventId == eventId
                && (x.PaidByParticipantId == targetParticipantId || x.Splits.Any(s => s.ParticipantId == targetParticipantId))).ToListAsync(ct);
            foreach (var expense in affected)
            {
                if (expense.PaidByParticipantId == targetParticipantId) expense.PaidByParticipantId = source.Id;
                if (expense.BalanceParticipantId == targetParticipantId) expense.BalanceParticipantId = source.Id;
                var old = expense.Splits.SingleOrDefault(x => x.ParticipantId == targetParticipantId);
                if (old is not null)
                {
                    var existing = expense.Splits.SingleOrDefault(x => x.ParticipantId == source.Id);
                    if (existing is null) old.ParticipantId = source.Id;
                    else
                    {
                        existing.AmountMinor = checked(existing.AmountMinor + old.AmountMinor);
                        existing.PercentageBps = existing.PercentageBps is null && old.PercentageBps is null ? null
                            : checked((existing.PercentageBps ?? 0) + (old.PercentageBps ?? 0));
                        db.ExpenseSplits.Remove(old);
                    }
                }
                // Invalidate forms opened before linking, including split-only changes.
                db.Entry(expense).Property(x => x.Balanced).IsModified = true;
            }
            // Keep historical transfer confirmations attached to their original rows.
            // Flatten aliases so their private history remains accessible to the same account.
            var aliases = await db.EventParticipants.Where(x => x.EventId == eventId && x.MergedIntoParticipantId == target.Id).ToListAsync(ct);
            foreach (var alias in aliases) alias.MergedIntoParticipantId = source.Id;
            var userId = target.UserId;
            target.DisplayName = user.DisplayName;
            target.UserId = null; target.Status = ParticipantStatus.Removed; target.MergedIntoParticipantId = source.Id;
            // Release the filtered unique account membership before attaching it to the preserved row.
            await db.SaveChangesAsync(ct);
            source.UserId = userId; source.DisplayName = user.DisplayName;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);

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
