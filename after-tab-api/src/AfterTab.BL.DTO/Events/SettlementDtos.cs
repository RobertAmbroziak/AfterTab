namespace AfterTab.BL.DTO.Events;
public sealed record SettlementBalanceDto(Guid UserId, string DisplayName, long PaidMinor,
    long CostMinor, long UnassignedMinor, long NetMinor);
public sealed record TransferAcknowledgementDto(bool Sent, bool Received, bool CanMarkSent, bool CanMarkReceived);
public sealed record MarkTransferDto(int Version, Guid FromUserId, Guid ToUserId, long AmountMinor, string Side, bool Value);
public sealed record SettlementTransferDto(Guid FromUserId, string FromName, Guid ToUserId, string ToName, long AmountMinor,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] TransferAcknowledgementDto? Acknowledgement = null);
public sealed record UnassignedExpenseDto(Guid ExpenseId, string Name, Guid AuthorUserId, string AuthorName, long AmountMinor);
public sealed record HistoricalPaymentDto(int Version, string FromName, string ToName, long AmountMinor, bool Sent, bool Received);
public sealed record SettlementDto(string Status, bool IsFinal, int Version, DateTimeOffset? ClosedAtUtc,
    int ExpenseCount, long TotalMinor, long UnassignedMinor, IReadOnlyList<SettlementBalanceDto> Balances,
    IReadOnlyList<SettlementTransferDto> Transfers, IReadOnlyList<UnassignedExpenseDto> UnassignedExpenses,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<HistoricalPaymentDto>? PreviousPayments = null);
