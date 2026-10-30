using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;

namespace AfterTab.DAL.Interfaces.Expenses;
public sealed record ExpenseParticipant(EventParticipant Participant, string DisplayName);
public sealed record ExpenseEvent(Event Event, IReadOnlyList<ExpenseParticipant> Participants);
public sealed record ExpenseReadModel(Expense Expense, string AuthorName, Guid AuthorUserId,
    IReadOnlyList<ExpenseSplitReadModel> Splits, Guid? PaidByParticipantId = null, string? PayerName = null);
public sealed record ExpenseSplitReadModel(Guid ParticipantId, string DisplayName, long AmountMinor, int? PercentageBps);
public interface IExpenseRepository
{
    Task<ExpenseEvent?> GetEventAsync(Guid eventId, CancellationToken ct);
    Task<IReadOnlyList<ExpenseReadModel>> ListAsync(Guid eventId, CancellationToken ct);
    Task CreateAsync(Expense expense, CancellationToken ct);
    Task UpdateSplitAsync(Expense replacement, Guid userId, byte[] expectedVersion, CancellationToken ct);
    Task UpdatePayerAsync(Guid eventId, Guid expenseId, Guid userId, Guid payerId, byte[] expectedVersion, CancellationToken ct);
}
