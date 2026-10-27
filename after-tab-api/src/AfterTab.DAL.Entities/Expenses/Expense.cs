using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Expenses;
public sealed class Expense : BaseEntity
{
    public Guid EventId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public long OthersAmountMinor { get; set; }
    public bool Balanced { get; set; }
    public Guid PaidByParticipantId { get; set; }
    public SplitMethod SplitMethod { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? Note { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<ExpenseSplit> Splits { get; set; } = new List<ExpenseSplit>();
}
