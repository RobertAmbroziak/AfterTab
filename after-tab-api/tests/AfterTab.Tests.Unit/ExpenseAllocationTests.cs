using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Services.Expenses;
using AfterTab.Common.Errors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Unit;
[TestClass]
public sealed class ExpenseAllocationTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-000000000003");
    [TestMethod]
    public void Three_33_Percent_Shares_Balance_Only_To_Explicitly_Selected_Person()
    {
        var input = new CreateExpenseDto("Pizza", 6250, "Percentage", [new(A, PercentageBps: 3300), new(B, PercentageBps: 3300), new(C, PercentageBps: 3300)]);
        var result = ExpenseAllocation.Calculate(input);
        Assert.IsTrue(result.Splits.All(x => x.AmountMinor == 2062)); Assert.AreEqual(64L, result.OthersAmountMinor);
        foreach (var target in new[] { A, B, C })
        {
            var balanced = ExpenseAllocation.Calculate(input with { BalanceParticipantId = target });
            Assert.AreEqual(2126L, balanced.Splits.Single(x => x.ParticipantId == target).AmountMinor);
            Assert.AreEqual(0L, balanced.OthersAmountMinor); Assert.AreEqual(6250L, balanced.Splits.Sum(x => x.AmountMinor));
            Assert.AreEqual(target, balanced.BalanceParticipantId); Assert.AreEqual(64L, balanced.BalanceAmountMinor);
            Assert.AreEqual(3300, balanced.Splits.Single(x => x.ParticipantId == target).PercentageBps);
        }
    }
    [TestMethod]
    public void Amount_Balance_Can_Go_To_The_Smallest_Share_And_Never_To_An_Unselected_Author()
    {
        var input = new CreateExpenseDto("Pizza", 6250, "Amount", [new(B, 3000), new(C, 2000)], BalanceParticipantId: C);
        var result = ExpenseAllocation.Calculate(input);
        Assert.AreEqual(3000L, result.Splits.Single(x => x.ParticipantId == B).AmountMinor);
        Assert.AreEqual(3250L, result.Splits.Single(x => x.ParticipantId == C).AmountMinor);
        Assert.AreEqual(1250L, result.BalanceAmountMinor); Assert.IsFalse(result.Splits.Any(x => x.ParticipantId == A));
        Assert.ThrowsExactly<AppException>(() => ExpenseAllocation.Calculate(input with { BalanceParticipantId = A }));
    }
    [TestMethod]
    public void No_Selected_Target_Keeps_Others_And_Obsolete_Automatic_Balance_Is_Rejected()
    {
        var input = new CreateExpenseDto("Pizza", 6250, "Amount", []);
        Assert.AreEqual(6250L, ExpenseAllocation.Calculate(input).OthersAmountMinor);
        Assert.ThrowsExactly<AppException>(() => ExpenseAllocation.Calculate(input with { Balance = true }));
        Assert.ThrowsExactly<AppException>(() => ExpenseAllocation.Calculate(input with { BalanceParticipantId = A }));
    }
    [TestMethod]
    public void Exactly_Allocated_Amount_Has_No_Adjustment_Or_Selected_Target()
    {
        var result = ExpenseAllocation.Calculate(new("Pizza", 6250, "Amount", [new(A, 6250)], BalanceParticipantId: A));
        Assert.IsNull(result.BalanceParticipantId); Assert.AreEqual(0L, result.BalanceAmountMinor); Assert.AreEqual(0L, result.OthersAmountMinor);
    }
    [TestMethod]
    public void Rejects_OverAllocation_Negative_Duplicate_Mixed_And_Overflow_Inputs()
    {
        var valid = new CreateExpenseDto("Pizza", 100, "Amount", [new(A, 100)]);
        CreateExpenseDto[] invalid = [valid with { AmountMinor = 0 }, valid with { AmountMinor = long.MaxValue },
            valid with { Shares = [new(A, 101)] }, valid with { Shares = [new(A, -1)] },
            valid with { Shares = [new(A, 50), new(A, 50)] }, valid with { Shares = [new(Guid.Empty, 50)] },
            valid with { Shares = [new(A, 50, 1)] }, valid with { Shares = null! }, valid with { SplitMethod = "Other" },
            valid with { SplitMethod = "Percentage", Shares = [new(A, PercentageBps: 5001), new(B, PercentageBps: 5000)] },
            valid with { SplitMethod = "Percentage", Shares = [new(A, 1, 5000)] }];
        foreach (var input in invalid) Assert.ThrowsExactly<AppException>(() => ExpenseAllocation.Calculate(input));
    }
    [TestMethod]
    public void Rounding_One_Grosz_And_Maximum_Amount_Remain_Exact()
    {
        var input = new CreateExpenseDto("Pizza", 1, "Percentage", [new(A, PercentageBps: 5000), new(B, PercentageBps: 5000)]);
        Assert.AreEqual(1L, ExpenseAllocation.Calculate(input).OthersAmountMinor);
        Assert.AreEqual(1L, ExpenseAllocation.Calculate(input with { BalanceParticipantId = B }).Splits.Single(x => x.ParticipantId == B).AmountMinor);
        var big = ExpenseAllocation.Calculate(input with { AmountMinor = ExpenseAllocation.MaximumAmountMinor });
        Assert.AreEqual(ExpenseAllocation.MaximumAmountMinor, big.Splits.Sum(x => x.AmountMinor) + big.OthersAmountMinor);
    }
}
