using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Services.Expenses;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace AfterTab.Tests.Unit;
[TestClass]
public sealed class SplitCalculatorTests
{
    private readonly SplitCalculator _sut = new();
    private static readonly Guid A = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid B = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid C = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [TestMethod]
    public void Pizza_Allocates_50_25_25()
    {
        var result = _sut.CalculatePercentage(6000, [new(A, 5000), new(B, 2500), new(C, 2500)]);
        CollectionAssert.AreEqual(new long[] { 3000, 1500, 1500 }, result.Select(x => x.AmountMinor).ToArray());
    }
    [TestMethod]
    public void Rounding_Preserves_Total_And_Is_Independent_Of_Input_Order()
    {
        PercentageShare[] shares = [new(A, 5000), new(B, 5000)];
        var first = _sut.CalculatePercentage(1, shares).ToDictionary(x => x.ParticipantId);
        var second = _sut.CalculatePercentage(1, shares.Reverse().ToArray()).ToDictionary(x => x.ParticipantId);
        Assert.AreEqual(1L, first.Values.Sum(x => x.AmountMinor));
        Assert.AreEqual(first[A].AmountMinor, second[A].AmountMinor);
        Assert.AreEqual(first[B].AmountMinor, second[B].AmountMinor);
    }
    [TestMethod]
    public void Large_Amount_Does_Not_Overflow_Intermediate_Multiplication()
    {
        var result = _sut.CalculatePercentage(long.MaxValue, [new(A, 5000), new(B, 5000)]);
        Assert.AreEqual(long.MaxValue, result.Sum(x => x.AmountMinor));
    }
    [TestMethod]
    public void Duplicate_Participant_Is_Rejected()
        => Assert.ThrowsExactly<ArgumentException>(() => _sut.CalculatePercentage(6000, [new(A, 5000), new(A, 5000)]));
    [TestMethod]
    public void Incomplete_Percentage_Is_Rejected()
        => Assert.ThrowsExactly<ArgumentException>(() => _sut.CalculatePercentage(6000, [new(A, 5000), new(B, 2500)]));
    [TestMethod]
    public void Negative_Percentage_Is_Rejected()
        => Assert.ThrowsExactly<ArgumentException>(() => _sut.CalculatePercentage(6000, [new(A, -100), new(B, 10100)]));
}
