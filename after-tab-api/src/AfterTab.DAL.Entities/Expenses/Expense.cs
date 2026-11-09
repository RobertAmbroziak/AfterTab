using AfterTab.DAL.Entities.Enums;
namespace AfterTab.DAL.Entities.Expenses;
public sealed class Expense : PublicEntity
{
    public long EventId { get; set; }
    public long AuthorUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public long OthersAmountMinor { get; set; }
    public bool Balanced { get; set; }
    public long? BalanceParticipantId { get; set; }
    public long BalanceAmountMinor { get; set; }
    public long PaidByParticipantId { get; set; }
    public SplitMethod SplitMethod { get; set; }
    public DateOnly ExpenseDate { get; set; }
    public string? Note { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<ExpenseSplit> Splits { get; set; } = new List<ExpenseSplit>();
}
