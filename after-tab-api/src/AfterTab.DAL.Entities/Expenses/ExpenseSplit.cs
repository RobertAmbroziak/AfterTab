using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Expenses;
public sealed class ExpenseSplit : BaseEntity
{
    public long ExpenseId { get; set; }
    // Included in composite foreign keys to enforce membership in the same event.
    public long EventId { get; set; }
    public long ParticipantId { get; set; }
    public long AmountMinor { get; set; }
    public int? PercentageBps { get; set; }
}
