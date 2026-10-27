using AfterTab.BL.DTO.Expenses;
namespace AfterTab.BL.Interfaces.Expenses;
public interface ISplitCalculator
{
    IReadOnlyList<SplitAmount> CalculatePercentage(long amountMinor, IReadOnlyList<PercentageShare> shares);
}
