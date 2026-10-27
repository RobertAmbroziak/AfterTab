namespace AfterTab.BL.DTO.Expenses;
public sealed record PercentageShare(Guid ParticipantId, int PercentageBps);
public sealed record SplitAmount(Guid ParticipantId, long AmountMinor, int PercentageBps);
public sealed record SplitPreviewRequest(long AmountMinor, IReadOnlyList<PercentageShare> Shares);
