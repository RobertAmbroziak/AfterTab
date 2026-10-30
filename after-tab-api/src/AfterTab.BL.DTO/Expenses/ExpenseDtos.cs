namespace AfterTab.BL.DTO.Expenses;

// Money is always integer grosze; percentages are hundredths of one percent (3300 = 33%).
public sealed record ExpenseShareDto(Guid ParticipantId, long AmountMinor = 0, int PercentageBps = 0);
public sealed record CreateExpenseDto(string Name, long AmountMinor, string SplitMethod,
    IReadOnlyList<ExpenseShareDto> Shares, bool Balance = false, Guid? PaidByParticipantId = null);
public sealed record UpdateExpensePayerDto(Guid PaidByParticipantId, string RowVersion);
public sealed record UpdateExpenseSplitDto(string SplitMethod, IReadOnlyList<ExpenseShareDto> Shares, bool Balance, string RowVersion);
public sealed record ExpenseAllocationDto(Guid ParticipantId, long AmountMinor, int? PercentageBps);
public sealed record ExpenseCalculationDto(IReadOnlyList<ExpenseAllocationDto> Splits, long OthersAmountMinor);
public sealed record ExpensePersonDto(Guid ParticipantId, string DisplayName, long AmountMinor, int? PercentageBps);
public sealed record ExpenseDto(Guid Id, string Name, long AmountMinor, string AuthorName, Guid AuthorUserId,
    string SplitMethod, bool Balanced, long OthersAmountMinor, IReadOnlyList<ExpensePersonDto> Splits, string? RowVersion = null,
    Guid? PaidByParticipantId = null, string? PayerName = null);
