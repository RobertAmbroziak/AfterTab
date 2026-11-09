using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;

namespace AfterTab.DAL.Interfaces.Expenses;
public sealed record ExpenseParticipant(EventParticipant Participant, string DisplayName);
public sealed record ExpenseEvent(Event Event, IReadOnlyList<ExpenseParticipant> Participants);
public sealed record ExpenseReadModel(Expense Expense, string AuthorName, long AuthorUserId,
    IReadOnlyList<ExpenseSplitReadModel> Splits, long? PaidByParticipantId = null, string? PayerName = null);
public sealed record ExpenseSplitReadModel(long ParticipantId, string DisplayName, long AmountMinor, int? PercentageBps);
public interface IExpenseRepository
{
    Task<ExpenseEvent?> GetEventAsync(long eventId, CancellationToken ct);
    Task<IReadOnlyList<ExpenseReadModel>> ListAsync(long eventId, CancellationToken ct);
    Task CreateAsync(Expense expense, CancellationToken ct);
    Task UpdateSplitAsync(Expense replacement, long userId, byte[] expectedVersion, CancellationToken ct, bool balanceOnly = false);
    Task UpdatePayerAsync(long eventId, long expenseId, long userId, long payerId, byte[] expectedVersion, CancellationToken ct);
}
