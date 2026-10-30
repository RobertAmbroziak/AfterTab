using AfterTab.BL.DTO.Events;
using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Services.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace AfterTab.Tests.Unit;
[TestClass]
public sealed class SettlementCalculatorTests
{
    private static readonly Guid Tomek = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid Jarek = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid Adam = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static ExpenseDto Expense(Guid payer, Guid debtor, long amount, long others = 0) => new(Guid.NewGuid(), "Pizza", amount,
        payer.ToString(), payer, "Amount", false, others, [new(debtor, debtor.ToString(), amount - others, null)]);
    private static SettlementDto Calculate(params ExpenseDto[] expenses) => SettlementCalculator.Calculate("Active", 0, null, [], expenses);
    [TestMethod]
    public void Mutual_Debts_Three_And_Twelve_Become_Nine_In_One_Direction()
    {
        var result = Calculate(Expense(Tomek, Jarek, 300), Expense(Jarek, Tomek, 1200));
        var transfer = result.Transfers.Single();
        Assert.AreEqual(Tomek, transfer.FromParticipantId); Assert.AreEqual(Jarek, transfer.ToParticipantId); Assert.AreEqual(900L, transfer.AmountMinor);
    }
    [TestMethod]
    public void Chains_Are_Netted_And_Cycles_Disappear()
    {
        var chain = Calculate(Expense(Tomek, Jarek, 1500), Expense(Jarek, Adam, 1500));
        Assert.AreEqual(Adam, chain.Transfers.Single().FromParticipantId); Assert.AreEqual(Tomek, chain.Transfers.Single().ToParticipantId);
        var cycle = Calculate(Expense(Tomek, Jarek, 1500), Expense(Jarek, Adam, 1500), Expense(Adam, Tomek, 1500));
        Assert.AreEqual(0, cycle.Transfers.Count); Assert.IsTrue(cycle.Balances.All(x => x.NetMinor == 0));
    }
    [TestMethod]
    public void Own_Cost_And_Unassigned_Are_Not_Charged_To_Other_People()
    {
        var result = Calculate(Expense(Tomek, Tomek, 500), Expense(Jarek, Adam, 1000, 400));
        Assert.AreEqual(1500L, result.TotalMinor); Assert.AreEqual(400L, result.UnassignedMinor);
        Assert.AreEqual(600L, result.Transfers.Single().AmountMinor);
        Assert.AreEqual(400L, result.Balances.Single(x => x.ParticipantId == Jarek).UnassignedMinor);
        Assert.AreEqual(0L, result.Balances.Single(x => x.ParticipantId == Tomek).NetMinor);
    }
    [TestMethod]
    public void Same_Name_Does_Not_Merge_People_And_Empty_Event_Is_Zero()
    {
        var members = new[] { new EventMemberDto(Tomek, "Adam", true), new EventMemberDto(Jarek, "Adam", false) };
        var result = SettlementCalculator.Calculate("Closed", 2, DateTimeOffset.UtcNow, members, []);
        Assert.AreEqual(2, result.Balances.Count); Assert.AreEqual(0L, result.TotalMinor); Assert.IsTrue(result.IsFinal); Assert.AreEqual(2, result.Version);
    }
    [TestMethod]
    public void Many_Splits_Produce_Exact_Deterministic_Balances_Without_Reciprocal_Transfers()
    {
        var random = new Random(42);
        var people = Enumerable.Range(1, 30).Select(i => new Guid(i, 0, 0, new byte[8])).ToArray();
        var expenses = Enumerable.Range(0, 500).Select(_ => Expense(people[random.Next(people.Length)], people[random.Next(people.Length)], random.Next(1, 100000))).ToArray();
        var result = Calculate(expenses);
        var reversed = Calculate(expenses.Reverse().ToArray());
        CollectionAssert.AreEqual(result.Transfers.ToArray(), reversed.Transfers.ToArray());
        foreach (var person in result.Balances)
            Assert.AreEqual(person.NetMinor, result.Transfers.Where(x => x.ToParticipantId == person.ParticipantId).Sum(x => x.AmountMinor)
                - result.Transfers.Where(x => x.FromParticipantId == person.ParticipantId).Sum(x => x.AmountMinor));
        Assert.IsTrue(result.Transfers.All(x => x.AmountMinor > 0 && x.FromParticipantId != x.ToParticipantId));
        Assert.IsTrue(result.Transfers.Count <= people.Length - 1);
        Assert.IsFalse(result.Transfers.Any(x => result.Transfers.Any(y => y.FromParticipantId == x.ToParticipantId && y.ToParticipantId == x.FromParticipantId)));
    }
    [TestMethod]
    public void Inconsistent_Expense_Is_Rejected_Instead_Of_Producing_False_Debts()
    {
        var invalid = Expense(Tomek, Jarek, 1000) with { OthersAmountMinor = 1 };
        Assert.ThrowsExactly<InvalidOperationException>(() => Calculate(invalid));
    }
}
