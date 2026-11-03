namespace AfterTab.DAL.Entities.Events;
public sealed class TransferConfirmation : BaseEntity
{
    public long EventId { get; set; }
    public int SettlementVersion { get; set; }
    public long FromParticipantId { get; set; }
    public long ToParticipantId { get; set; }
    public long AmountMinor { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public DateTimeOffset? ReceivedAtUtc { get; set; }
}
