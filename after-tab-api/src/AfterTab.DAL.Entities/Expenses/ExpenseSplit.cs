using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Expenses;
public sealed class ExpenseSplit : BaseEntity
{
    public Guid ExpenseId { get; set; }
    // Included in composite foreign keys to enforce membership in the same event.
    public Guid EventId { get; set; }
    public Guid ParticipantId { get; set; }
    public long AmountMinor { get; set; }
    public int? PercentageBps { get; set; }
}
