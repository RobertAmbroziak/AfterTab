using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Expenses;
using AfterTab.DAL.Interfaces.Expenses;
using Microsoft.EntityFrameworkCore;
using System.Data;
using AfterTab.Common.Errors;

namespace AfterTab.DAL.EF.Repositories;
public sealed class ExpenseRepository(AppDbContext db) : IExpenseRepository
{
    public async Task<ExpenseEvent?> GetEventAsync(Guid eventId, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, ct);
        if (ev is null) return null;
        var participants = await (from p in db.EventParticipants.AsNoTracking()
            join account in db.Users on p.UserId equals account.Id into accounts
            from u in accounts.DefaultIfEmpty()
            where p.EventId == eventId && p.Status == ParticipantStatus.Active && (p.UserId == null || u.Status == UserStatus.Active)
            select new ExpenseParticipant(p, u == null ? p.DisplayName : u.DisplayName)).ToListAsync(ct);
        return new(ev, participants);
    }
    public async Task<IReadOnlyList<ExpenseReadModel>> ListAsync(Guid eventId, CancellationToken ct)
    {
        var expenses = await (from e in db.Expenses.AsNoTracking()
            join p in db.EventParticipants on e.PaidByParticipantId equals p.Id
            join account in db.Users on p.UserId equals account.Id into accounts
            from u in accounts.DefaultIfEmpty()
            join author in db.Users on e.AuthorUserId equals author.Id
            where e.EventId == eventId
            orderby e.CreatedAtUtc, e.Id
            select new { Expense = e, AuthorName = author.DisplayName, AuthorUserId = author.Id, PaidByParticipantId = p.Id, PayerName = u == null ? p.DisplayName : u.DisplayName }).ToListAsync(ct);
        var splits = await (from s in db.ExpenseSplits.AsNoTracking()
            join p in db.EventParticipants on s.ParticipantId equals p.Id
            join account in db.Users on p.UserId equals account.Id into accounts
            from u in accounts.DefaultIfEmpty()
            where s.EventId == eventId
            select new { s.ExpenseId, Row = new ExpenseSplitReadModel(p.Id, u == null ? p.DisplayName : u.DisplayName, s.AmountMinor, s.PercentageBps) }).ToListAsync(ct);
        return expenses.Select(x => new ExpenseReadModel(x.Expense, x.AuthorName, x.AuthorUserId,
            splits.Where(s => s.ExpenseId == x.Expense.Id).Select(s => s.Row).OrderBy(s => s.DisplayName).ThenBy(s => s.ParticipantId).ToList(), x.PaidByParticipantId, x.PayerName)).ToList();
    }
    private async Task ValidateWrite(Expense expense, Guid userId, CancellationToken ct)
    {
        var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == expense.EventId, ct);
        if (ev is null) throw new AppException(ErrorKind.NotFound, "Nie znaleziono wydarzenia.");
        if (ev.Status != EventStatus.Active) throw new AppException(ErrorKind.Conflict, "Wydarzenie jest zamknięte. Autor musi je najpierw otworzyć.");
        var participants = await (from p in db.EventParticipants join account in db.Users on p.UserId equals account.Id into accounts
            from u in accounts.DefaultIfEmpty()
            where p.EventId == expense.EventId && p.Status == ParticipantStatus.Active && (p.UserId == null || u.Status == UserStatus.Active)
            select p).ToListAsync(ct);
        if (!participants.Any(x => x.UserId == userId) || !participants.Any(x => x.Id == expense.PaidByParticipantId))
            throw new AppException(ErrorKind.Forbidden, "Nie masz uprawnień do zmiany tej pozycji.");
        if (expense.BalanceParticipantId.HasValue && !expense.Splits.Any(x => x.ParticipantId == expense.BalanceParticipantId))
            throw new AppException(ErrorKind.Validation, "Wyrównanie wymaga osoby z podziału.");
        if (expense.Splits.Any(x => !participants.Any(p => p.Id == x.ParticipantId)))
            throw new AppException(ErrorKind.Validation, "Podział może zawierać tylko aktywnych uczestników wydarzenia.");
    }
    public Task CreateAsync(Expense expense, CancellationToken ct)
        => InTransaction(async () =>
        {
            await ValidateWrite(expense, expense.AuthorUserId, ct);
            var ev = await db.Events.SingleAsync(x => x.Id == expense.EventId, ct);
            var payer = await db.EventParticipants.SingleAsync(x => x.Id == expense.PaidByParticipantId, ct);
            if (payer.UserId != expense.AuthorUserId && ev.OwnerUserId != expense.AuthorUserId)
                throw new AppException(ErrorKind.Forbidden, "Tylko autor wydarzenia może wskazać innego płacącego.");
            db.Expenses.Add(expense);
            await db.SaveChangesAsync(ct);
        }, ct);
    public Task UpdateSplitAsync(Expense replacement, Guid userId, byte[] expectedVersion, CancellationToken ct, bool balanceOnly = false)
        => InTransaction(async () =>
        {
            // Read the event before touching expenses, just like close/create, to serialize lifecycle changes.
            await ValidateWrite(replacement, userId, ct);
            var current = await db.Expenses.Include(x => x.Splits).SingleOrDefaultAsync(x => x.Id == replacement.Id && x.EventId == replacement.EventId, ct)
                ?? throw new AppException(ErrorKind.NotFound, "Nie znaleziono pozycji.");
            var ev = await db.Events.SingleAsync(x => x.Id == current.EventId, ct);
            var payer = await db.EventParticipants.SingleAsync(x => x.Id == current.PaidByParticipantId, ct);
            var canBalance = ev.OwnerUserId == userId || payer.UserId == userId;
            if (current.PaidByParticipantId != replacement.PaidByParticipantId || (balanceOnly ? !canBalance : current.AuthorUserId != userId))
                throw new AppException(ErrorKind.Forbidden, "Nie masz uprawnień do zmiany tego podziału lub wyrównania.");
            if (!canBalance && ((!current.Balanced && replacement.Balanced) || current.BalanceParticipantId != replacement.BalanceParticipantId || current.BalanceAmountMinor != replacement.BalanceAmountMinor))
                throw new AppException(ErrorKind.Forbidden, "Wyrównanie może zmienić tylko autor wydarzenia albo płacący.");
            if (!current.RowVersion.SequenceEqual(expectedVersion))
                throw new AppException(ErrorKind.Conflict, "Podział został zmieniony. Odśwież wydarzenie i spróbuj ponownie.");
            current.SplitMethod = replacement.SplitMethod; current.Balanced = replacement.Balanced;
            current.BalanceParticipantId = replacement.BalanceParticipantId; current.BalanceAmountMinor = replacement.BalanceAmountMinor;
            current.OthersAmountMinor = replacement.OthersAmountMinor;
            foreach (var old in current.Splits.ToList())
            {
                var next = replacement.Splits.SingleOrDefault(x => x.ParticipantId == old.ParticipantId);
                if (next is null) db.ExpenseSplits.Remove(old);
                else { old.AmountMinor = next.AmountMinor; old.PercentageBps = next.PercentageBps; }
            }
            foreach (var next in replacement.Splits.Where(x => !current.Splits.Any(s => s.ParticipantId == x.ParticipantId))) current.Splits.Add(next);
            // Mark the aggregate modified even when only its children changed, updating SQL Server rowversion.
            db.Entry(current).Property(x => x.Balanced).IsModified = true;
            await db.SaveChangesAsync(ct);
        }, ct);
    public Task UpdatePayerAsync(Guid eventId, Guid expenseId, Guid userId, Guid payerId, byte[] expectedVersion, CancellationToken ct)
        => InTransaction(async () =>
        {
            var ev = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId, ct)
                ?? throw new AppException(ErrorKind.NotFound, "Nie znaleziono wydarzenia.");
            if (ev.OwnerUserId != userId) throw new AppException(ErrorKind.Forbidden, "Tylko autor wydarzenia może zmienić płacącego.");
            if (ev.Status != EventStatus.Active) throw new AppException(ErrorKind.Conflict, "Najpierw otwórz wydarzenie.");
            var payer = await (from p in db.EventParticipants join account in db.Users on p.UserId equals account.Id into accounts
            from u in accounts.DefaultIfEmpty()
                where p.EventId == eventId && p.Id == payerId && p.Status == ParticipantStatus.Active && (p.UserId == null || u.Status == UserStatus.Active) select p).SingleOrDefaultAsync(ct)
                ?? throw new AppException(ErrorKind.Validation, "Płacący musi być aktywnym uczestnikiem wydarzenia.");
            var expense = await db.Expenses.SingleOrDefaultAsync(x => x.Id == expenseId && x.EventId == eventId, ct)
                ?? throw new AppException(ErrorKind.NotFound, "Nie znaleziono pozycji.");
            if (!expense.RowVersion.SequenceEqual(expectedVersion)) throw new AppException(ErrorKind.Conflict, "Pozycja została zmieniona. Odśwież wydarzenie.");
            expense.PaidByParticipantId = payer.Id;
            db.Entry(expense).Property(x => x.PaidByParticipantId).IsModified = true;
            await db.SaveChangesAsync(ct);
        }, ct);
    private Task InTransaction(Func<Task> operation, CancellationToken ct)
        => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            await operation(); await tx.CommitAsync(ct);
        });
}
