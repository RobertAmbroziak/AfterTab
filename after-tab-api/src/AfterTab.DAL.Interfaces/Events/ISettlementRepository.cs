using AfterTab.DAL.Interfaces.Expenses;
using AfterTab.DAL.Entities.Events;
namespace AfterTab.DAL.Interfaces.Events;
public sealed record SettlementReadModel(ExpenseEvent Event, IReadOnlyList<ExpenseReadModel> Expenses, IReadOnlyList<TransferConfirmation>? Confirmations = null, IReadOnlyDictionary<long, string>? HistoricalNames = null);
public interface ISettlementRepository
{
    Task<SettlementReadModel> ReadAsync(long eventId, long userId, CancellationToken ct);
    Task<SettlementReadModel> SetClosedAsync(long eventId, long userId, bool closed, DateTimeOffset now, CancellationToken ct);
    Task MarkAsync(long eventId, long userId, int version, long fromParticipantId, long toParticipantId, long amountMinor, string side, bool value, DateTimeOffset now, CancellationToken ct);
}
