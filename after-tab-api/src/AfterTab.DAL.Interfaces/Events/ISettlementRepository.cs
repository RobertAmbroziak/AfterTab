using AfterTab.DAL.Interfaces.Expenses;
using AfterTab.DAL.Entities.Events;
namespace AfterTab.DAL.Interfaces.Events;
public sealed record SettlementReadModel(ExpenseEvent Event, IReadOnlyList<ExpenseReadModel> Expenses, IReadOnlyList<TransferConfirmation>? Confirmations = null, IReadOnlyDictionary<Guid, string>? HistoricalNames = null);
public interface ISettlementRepository
{
    Task<SettlementReadModel> ReadAsync(Guid eventId, Guid userId, CancellationToken ct);
    Task<SettlementReadModel> SetClosedAsync(Guid eventId, Guid userId, bool closed, DateTimeOffset now, CancellationToken ct);
    Task MarkAsync(Guid eventId, Guid userId, int version, Guid fromParticipantId, Guid toParticipantId, long amountMinor, string side, bool value, DateTimeOffset now, CancellationToken ct);
}
