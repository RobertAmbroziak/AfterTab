using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Interfaces.Expenses;
using AfterTab.Common.Errors;
using AfterTab.Common.Security;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Expenses;
using AfterTab.DAL.Interfaces.Expenses;
using AfterTab.DAL.Interfaces.Users;

namespace AfterTab.BL.Services.Expenses;
public sealed class ExpenseService(IExpenseRepository expenses, IUserRepository users, ICurrentUser actor, TimeProvider clock) : IExpenseService
{
    private async Task<ExpenseEvent> Access(Guid eventId, bool write, CancellationToken ct)
    {
        var user = actor.UserId is Guid id ? await users.GetByIdAsync(id, ct) : null;
        if (user?.Status != UserStatus.Active) throw new AppException(ErrorKind.Forbidden, "Zaloguj się ponownie.");
        var ev = await expenses.GetEventAsync(eventId, ct);
        if (ev is null || !ev.Participants.Any(x => x.Participant.UserId == user.Id))
            throw new AppException(ErrorKind.NotFound, "Nie znaleziono wydarzenia na Twojej liście.");
        if (write && !user.DisplayNameConfirmed)
            throw new AppException(ErrorKind.Conflict, "Najpierw podaj swoje imię.");
        if (write && ev.Event.Status != EventStatus.Active)
            throw new AppException(ErrorKind.Conflict, "Pozycje można dodawać tylko do aktywnego wydarzenia.");
        return ev;
    }
    private static ExpenseDto Map(ExpenseReadModel x) => new(x.Expense.Id, x.Expense.Name, x.Expense.AmountMinor,
        x.AuthorName, x.AuthorUserId, x.Expense.SplitMethod.ToString(), x.Expense.Balanced, x.Expense.OthersAmountMinor,
        x.Splits.Select(s => new ExpensePersonDto(s.ParticipantId, s.DisplayName, s.AmountMinor, s.PercentageBps)).ToList(), Convert.ToBase64String(x.Expense.RowVersion), x.PaidByParticipantId, x.PayerName);
    public async Task<IReadOnlyList<ExpenseDto>> ListAsync(Guid eventId, CancellationToken ct)
    {
        await Access(eventId, false, ct);
        return (await expenses.ListAsync(eventId, ct)).Select(Map).ToList();
    }
    public async Task<ExpenseDto> CreateAsync(Guid eventId, CreateExpenseDto input, CancellationToken ct)
    {
        var ev = await Access(eventId, true, ct);
        var name = input.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200 || name.Any(char.IsControl))
            throw new AppException(ErrorKind.Validation, "Nazwa pozycji musi mieć od 1 do 200 znaków.");
        var author = ev.Participants.Single(x => x.Participant.UserId == actor.UserId);
        var calculated = ExpenseAllocation.Calculate(input, author.Participant.Id);
        var participants = ev.Participants.ToDictionary(x => x.Participant.Id);
        var payerId = input.PaidByParticipantId ?? author.Participant.Id;
        if (payerId != author.Participant.Id && ev.Event.OwnerUserId != actor.UserId!.Value)
            throw new AppException(ErrorKind.Forbidden, "Tylko autor wydarzenia może wskazać innego płacącego.");
        if (!participants.ContainsKey(payerId)) throw new AppException(ErrorKind.Validation, "Płacący musi być aktywnym uczestnikiem wydarzenia.");
        if (calculated.Splits.Any(x => !participants.ContainsKey(x.ParticipantId)))
            throw new AppException(ErrorKind.Validation, "Podział może zawierać tylko aktywnych uczestników tego wydarzenia.");
        var expense = new Expense { EventId = eventId, AuthorUserId = actor.UserId!.Value, Name = name, AmountMinor = input.AmountMinor,
            OthersAmountMinor = calculated.OthersAmountMinor, Balanced = input.Balance,
            PaidByParticipantId = participants[payerId].Participant.Id,
            SplitMethod = input.SplitMethod == "Percentage" ? SplitMethod.Percentage : SplitMethod.Amount,
            ExpenseDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime) };
        expense.Splits = calculated.Splits.Select(x => new ExpenseSplit { ExpenseId = expense.Id, EventId = eventId,
            ParticipantId = participants[x.ParticipantId].Participant.Id, AmountMinor = x.AmountMinor, PercentageBps = x.PercentageBps }).ToList();
        await expenses.CreateAsync(expense, ct);
        return Map(new(expense, author.DisplayName, actor.UserId!.Value,
            calculated.Splits.Select(x => new ExpenseSplitReadModel(x.ParticipantId, participants[x.ParticipantId].DisplayName, x.AmountMinor, x.PercentageBps)).ToList(), payerId, participants[payerId].DisplayName));
    }
    public async Task<ExpenseDto> UpdateSplitAsync(Guid eventId, Guid expenseId, UpdateExpenseSplitDto input, CancellationToken ct)
    {
        var ev = await Access(eventId, true, ct);
        var existing = (await expenses.ListAsync(eventId, ct)).SingleOrDefault(x => x.Expense.Id == expenseId)
            ?? throw new AppException(ErrorKind.NotFound, "Nie znaleziono pozycji.");
        if (existing.AuthorUserId != actor.UserId)
            throw new AppException(ErrorKind.Forbidden, "Tylko autor pozycji może poprawić jej podział.");
        byte[] version;
        try { version = Convert.FromBase64String(input.RowVersion ?? ""); }
        catch (FormatException) { throw new AppException(ErrorKind.Validation, "Nieprawidłowa wersja pozycji."); }
        if (version.Length == 0) throw new AppException(ErrorKind.Validation, "Odśwież wydarzenie przed zmianą podziału.");
        var result = ExpenseAllocation.Calculate(new(existing.Expense.Name, existing.Expense.AmountMinor, input.SplitMethod, input.Shares, input.Balance), ev.Participants.Single(x => x.Participant.UserId == actor.UserId).Participant.Id);
        var people = ev.Participants.ToDictionary(x => x.Participant.Id, x => x.Participant.Id);
        if (result.Splits.Any(x => !people.ContainsKey(x.ParticipantId)))
            throw new AppException(ErrorKind.Validation, "Podział może zawierać tylko aktywnych uczestników tego wydarzenia.");
        var replacement = new Expense { Id = expenseId, EventId = eventId, AuthorUserId = existing.AuthorUserId, PaidByParticipantId = existing.Expense.PaidByParticipantId,
            SplitMethod = input.SplitMethod == "Percentage" ? SplitMethod.Percentage : SplitMethod.Amount,
            Balanced = input.Balance, OthersAmountMinor = result.OthersAmountMinor,
            Splits = result.Splits.Select(x => new ExpenseSplit { ExpenseId = expenseId, EventId = eventId,
                ParticipantId = people[x.ParticipantId], AmountMinor = x.AmountMinor, PercentageBps = x.PercentageBps }).ToList() };
        await expenses.UpdateSplitAsync(replacement, actor.UserId!.Value, version, ct);
        return Map((await expenses.ListAsync(eventId, ct)).Single(x => x.Expense.Id == expenseId));
    }
    public async Task<ExpenseDto> UpdatePayerAsync(Guid eventId, Guid expenseId, UpdateExpensePayerDto input, CancellationToken ct)
    {
        var ev = await Access(eventId, true, ct);
        if (ev.Event.OwnerUserId != actor.UserId) throw new AppException(ErrorKind.Forbidden, "Tylko autor wydarzenia może zmienić płacącego.");
        byte[] version;
        try { version = Convert.FromBase64String(input.RowVersion ?? ""); }
        catch (FormatException) { throw new AppException(ErrorKind.Validation, "Nieprawidłowa wersja pozycji."); }
        if (version.Length == 0) throw new AppException(ErrorKind.Validation, "Odśwież wydarzenie przed zmianą płacącego.");
        await expenses.UpdatePayerAsync(eventId, expenseId, actor.UserId!.Value, input.PaidByParticipantId, version, ct);
        return Map((await expenses.ListAsync(eventId, ct)).Single(x => x.Expense.Id == expenseId));
    }
}
