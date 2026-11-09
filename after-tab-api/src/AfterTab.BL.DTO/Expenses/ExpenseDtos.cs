namespace AfterTab.BL.DTO.Expenses;

// Money is always integer grosze; percentages are hundredths of one percent (3300 = 33%).
public sealed record ExpenseShareDto(long ParticipantId, long AmountMinor = 0, int PercentageBps = 0);
public sealed record CreateExpenseDto(string Name, long AmountMinor, string SplitMethod,
    IReadOnlyList<ExpenseShareDto> Shares, bool Balance = false, long? PaidByParticipantId = null, long? BalanceParticipantId = null);
public sealed record UpdateExpensePayerDto(long PaidByParticipantId, string RowVersion);
public sealed record UpdateExpenseSplitDto(string SplitMethod, IReadOnlyList<ExpenseShareDto> Shares, bool Balance, string RowVersion, long? BalanceParticipantId = null);
public sealed record UpdateExpenseBalanceDto(long? ParticipantId, string RowVersion);
public sealed record ExpenseAllocationDto(long ParticipantId, long AmountMinor, int? PercentageBps);
public sealed record ExpenseCalculationDto(IReadOnlyList<ExpenseAllocationDto> Splits, long OthersAmountMinor, long? BalanceParticipantId = null, long BalanceAmountMinor = 0);
public sealed record ExpensePersonDto(long ParticipantId, string DisplayName, long AmountMinor, int? PercentageBps);
public sealed record ExpenseDto(long Id, string Name, long AmountMinor, string AuthorName, long AuthorUserId,
    string SplitMethod, bool Balanced, long OthersAmountMinor, IReadOnlyList<ExpensePersonDto> Splits, string? RowVersion = null,
    long? PaidByParticipantId = null, string? PayerName = null, long? BalanceParticipantId = null, long BalanceAmountMinor = 0);
