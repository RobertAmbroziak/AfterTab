namespace AfterTab.DAL.Entities.Events;
public sealed class TransferConfirmation : BaseEntity
{
    public Guid EventId { get; set; }
    public int SettlementVersion { get; set; }
    public Guid FromParticipantId { get; set; }
    public Guid ToParticipantId { get; set; }
    public long AmountMinor { get; set; }
    public DateTimeOffset? SentAtUtc { get; set; }
    public DateTimeOffset? ReceivedAtUtc { get; set; }
}
