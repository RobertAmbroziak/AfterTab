using System.Data;
using AfterTab.Common.Errors;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Interfaces.Events;
using AfterTab.DAL.Interfaces.Expenses;
using Microsoft.EntityFrameworkCore;

namespace AfterTab.DAL.EF.Repositories;
public sealed class SettlementRepository(AppDbContext db) : ISettlementRepository
{
    private async Task<SettlementReadModel> Read(Guid eventId, Guid userId, CancellationToken ct)
    {
        var expenses = new ExpenseRepository(db);
        var data = await expenses.GetEventAsync(eventId, ct);
        if (data is null || !data.Participants.Any(x => x.Participant.UserId == userId))
            throw new AppException(ErrorKind.NotFound, "Nie znaleziono wydarzenia na Twojej liście.");
        var confirmations = await db.TransferConfirmations.AsNoTracking().Where(x => x.EventId == eventId
            && (x.FromUserId == userId || x.ToUserId == userId)).ToListAsync(ct);
        return new(data, await expenses.ListAsync(eventId, ct), confirmations);
    }
    public Task<SettlementReadModel> ReadAsync(Guid eventId, Guid userId, CancellationToken ct)
        => InTransaction(() => Read(eventId, userId, ct), ct);
    public Task<SettlementReadModel> SetClosedAsync(Guid eventId, Guid userId, bool closed, DateTimeOffset now, CancellationToken ct)
        => InTransaction(async () =>
        {
            var data = await Read(eventId, userId, ct);
            if (data.Event.Event.OwnerUserId != userId)
                throw new AppException(ErrorKind.Forbidden, "Tylko autor wydarzenia może je zamykać i otwierać.");
            var ev = await db.Events.SingleAsync(x => x.Id == eventId, ct);
            if (ev.Status == EventStatus.Archived) throw new AppException(ErrorKind.Conflict, "To wydarzenie jest zarchiwizowane.");
            if (closed && data.Expenses.Any(x => x.Expense.OthersAmountMinor != 0
                || x.Splits.Sum(s => s.AmountMinor) != x.Expense.AmountMinor))
                throw new AppException(ErrorKind.Conflict, "Najpierw uzupełnij podziały: kwota „Inni” musi wynosić 0 zł w każdej pozycji.");
            var target = closed ? EventStatus.Closed : EventStatus.Active;
            if (ev.Status != target)
            {
                ev.Status = target;
                ev.ClosedAtUtc = closed ? now : null;
                if (closed) ev.SettlementVersion = checked(ev.SettlementVersion + 1);
                await db.SaveChangesAsync(ct);
            }
            return data with { Event = new ExpenseEvent(ev, data.Event.Participants) };
        }, ct);
    public Task MarkAsync(Guid eventId, Guid userId, int version, Guid fromUserId, Guid toUserId, long amountMinor, string side, bool value, DateTimeOffset now, CancellationToken ct)
        => InTransaction(async () =>
        {
            var data = await Read(eventId, userId, ct);
            if (data.Event.Event.Status != EventStatus.Closed || data.Event.Event.SettlementVersion != version)
                throw new AppException(ErrorKind.Conflict, "Odśwież zatwierdzone rozliczenie przed oznaczeniem przelewu.");
            if ((side != "Sent" && side != "Received") || (side == "Sent" ? fromUserId : toUserId) != userId)
                throw new AppException(ErrorKind.Forbidden, "Możesz oznaczyć tylko własne wysłanie lub odbiór przelewu.");
            var row = await db.TransferConfirmations.SingleOrDefaultAsync(x => x.EventId == eventId && x.SettlementVersion == version
                && x.FromUserId == fromUserId && x.ToUserId == toUserId, ct);
            if (row is null)
            {
                row = new() { EventId = eventId, SettlementVersion = version, FromUserId = fromUserId, ToUserId = toUserId, AmountMinor = amountMinor };
                db.TransferConfirmations.Add(row);
            }
            if (row.AmountMinor != amountMinor) throw new AppException(ErrorKind.Conflict, "Kwota przelewu jest nieaktualna.");
            if (side == "Sent") row.SentAtUtc = value ? row.SentAtUtc ?? now : null;
            else row.ReceivedAtUtc = value ? row.ReceivedAtUtc ?? now : null;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    private Task<T> InTransaction<T>(Func<Task<T>> operation, CancellationToken ct)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var result = await operation();
            await tx.CommitAsync(ct);
            return result;
        });
}
