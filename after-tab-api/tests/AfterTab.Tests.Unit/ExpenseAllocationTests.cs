using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Services.Expenses;
using AfterTab.Common.Errors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Unit;
[TestClass]
public sealed class ExpenseAllocationTests
{
    private static readonly Guid Author = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    [TestMethod]
    public void Pizza_Three_33_Percent_Shares_Keep_Others_Or_Balance_To_Author()
    {
        var input = new CreateExpenseDto("Pizza", 6250, "Percentage", [new(Author, PercentageBps: 3300), new(B, PercentageBps: 3300), new(C, PercentageBps: 3300)]);
        var result = ExpenseAllocation.Calculate(input, Author);
        Assert.IsTrue(result.Splits.All(x => x.AmountMinor == 2062));
        Assert.AreEqual(64L, result.OthersAmountMinor);
        var balanced = ExpenseAllocation.Calculate(input with { Balance = true }, Author);
        Assert.AreEqual(2126L, balanced.Splits.Single(x => x.UserId == Author).AmountMinor);
        Assert.AreEqual(0L, balanced.OthersAmountMinor);
        Assert.AreEqual(6250L, balanced.Splits.Sum(x => x.AmountMinor));
        Assert.AreEqual(3300, balanced.Splits.Single(x => x.UserId == Author).PercentageBps);
    }
    [TestMethod]
    public void Amount_Remainder_Goes_To_Largest_And_Tie_Adds_Unselected_Author()
    {
        var input = new CreateExpenseDto("Pizza", 6250, "Amount", [new(B, 3000), new(C, 2000)], true);
        Assert.AreEqual(4250L, ExpenseAllocation.Calculate(input, Author).Splits.Single(x => x.UserId == B).AmountMinor);
        var tied = ExpenseAllocation.Calculate(input with { Shares = [new(B, 2000), new(C, 2000)] }, Author);
        Assert.AreEqual(2250L, tied.Splits.Single(x => x.UserId == Author).AmountMinor);
        Assert.AreEqual(6250L, tied.Splits.Sum(x => x.AmountMinor));
    }
    [TestMethod]
    public void Empty_Split_Is_All_Others_And_Balance_Assigns_To_Author()
    {
        var input = new CreateExpenseDto("Pizza", 6250, "Amount", []);
        Assert.AreEqual(6250L, ExpenseAllocation.Calculate(input, Author).OthersAmountMinor);
        Assert.AreEqual(6250L, ExpenseAllocation.Calculate(input with { Balance = true }, Author).Splits.Single().AmountMinor);
    }
    [TestMethod]
    public void Rejects_OverAllocation_Negative_Duplicate_Mixed_And_Overflow_Inputs()
    {
        var valid = new CreateExpenseDto("Pizza", 100, "Amount", [new(Author, 100)]);
        CreateExpenseDto[] invalid = [valid with { AmountMinor = 0 }, valid with { AmountMinor = long.MaxValue },
            valid with { Shares = [new(Author, 101)] }, valid with { Shares = [new(Author, -1)] },
            valid with { Shares = [new(Author, 50), new(Author, 50)] }, valid with { Shares = [new(Guid.Empty, 50)] },
            valid with { Shares = [new(Author, 50, 1)] }, valid with { Shares = null! }, valid with { SplitMethod = "Other" },
            valid with { SplitMethod = "Percentage", Shares = [new(Author, PercentageBps: 5001), new(B, PercentageBps: 5000)] },
            valid with { SplitMethod = "Percentage", Shares = [new(Author, 1, 5000)] }];
        foreach (var input in invalid) Assert.ThrowsExactly<AppException>(() => ExpenseAllocation.Calculate(input, Author));
    }
    [TestMethod]
    public void Hundred_Percent_Can_Leave_Grosz_And_Maximum_Amount_Remains_Exact()
    {
        var input = new CreateExpenseDto("Pizza", 1, "Percentage", [new(Author, PercentageBps: 5000), new(B, PercentageBps: 5000)]);
        Assert.AreEqual(1L, ExpenseAllocation.Calculate(input, Author).OthersAmountMinor);
        Assert.AreEqual(1L, ExpenseAllocation.Calculate(input with { Balance = true }, Author).Splits.Single(x => x.UserId == Author).AmountMinor);
        var big = ExpenseAllocation.Calculate(input with { AmountMinor = ExpenseAllocation.MaximumAmountMinor }, Author);
        Assert.AreEqual(ExpenseAllocation.MaximumAmountMinor, big.Splits.Sum(x => x.AmountMinor) + big.OthersAmountMinor);
    }
}
