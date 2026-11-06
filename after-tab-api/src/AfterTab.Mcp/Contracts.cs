using System.Text.Json.Nodes;

namespace AfterTab.Mcp;

public sealed record ExpenseShare(Guid ParticipantId, long AmountMinor = 0, int PercentageBps = 0);
public sealed record ExpenseInput(string Name, long AmountMinor, string SplitMethod,
    IReadOnlyList<ExpenseShare> Shares, Guid? PaidByParticipantId = null, Guid? BalanceParticipantId = null);
public sealed record SplitInput(string SplitMethod, IReadOnlyList<ExpenseShare> Shares,
    string RowVersion, Guid? BalanceParticipantId = null);

public interface IMcpWriteStore
{
    Task<JsonNode?> ExecuteAsync(Guid operationId, string tool, string fingerprint,
        Func<Task<JsonNode?>> operation, CancellationToken ct);
}

// The host adapter owns public-ID mapping, authentication and existing business services.
// This module has no database connection or deployment-specific addresses.
public interface IAfterTabMcpApplication
{
    Task EnsureEventAccessAsync(Guid eventId, CancellationToken ct);
    Task<JsonNode?> GetProfileAsync(CancellationToken ct);
    Task<JsonNode?> GetUserInstructionsAsync(CancellationToken ct);
    Task<JsonNode?> ListEventsAsync(int page, CancellationToken ct);
    Task<JsonNode?> GetEventAsync(Guid eventId, CancellationToken ct);
    Task<JsonNode?> CreateEventAsync(string name, string? description, CancellationToken ct);
    Task<JsonNode?> AddParticipantAsync(Guid eventId, string displayName, CancellationToken ct);
    Task<JsonNode?> RenameParticipantAsync(Guid eventId, Guid participantId, string displayName, CancellationToken ct);
    Task<JsonNode?> RemoveParticipantAsync(Guid eventId, Guid participantId, CancellationToken ct);
    Task<JsonNode?> ListExpensesAsync(Guid eventId, CancellationToken ct);
    Task<JsonNode?> PreviewExpenseAsync(Guid eventId, ExpenseInput input, CancellationToken ct);
    Task<JsonNode?> CreateExpenseAsync(Guid eventId, ExpenseInput input, CancellationToken ct);
    Task<JsonNode?> UpdateSplitAsync(Guid eventId, Guid expenseId, SplitInput input, CancellationToken ct);
    Task<JsonNode?> SetBalanceAsync(Guid eventId, Guid expenseId, Guid? participantId, string rowVersion, CancellationToken ct);
    Task<JsonNode?> SetPayerAsync(Guid eventId, Guid expenseId, Guid participantId, string rowVersion, CancellationToken ct);
    Task<JsonNode?> GetSettlementAsync(Guid eventId, CancellationToken ct);
    Task<JsonNode?> SetClosedAsync(Guid eventId, bool closed, CancellationToken ct);
    Task<JsonNode?> MarkTransferAsync(Guid eventId, int version, Guid fromParticipantId, Guid toParticipantId,
        long amountMinor, string side, bool value, CancellationToken ct);
}
