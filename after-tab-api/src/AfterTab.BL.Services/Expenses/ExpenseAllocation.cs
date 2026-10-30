using AfterTab.BL.DTO.Expenses;
using AfterTab.Common.Errors;

namespace AfterTab.BL.Services.Expenses;

public static class ExpenseAllocation
{
    public const long MaximumAmountMinor = 100_000_000_000;
    public static ExpenseCalculationDto Calculate(CreateExpenseDto input, Guid authorId)
    {
        if (input.AmountMinor <= 0 || input.AmountMinor > MaximumAmountMinor)
            throw Invalid("Podaj dodatnią kwotę do 1 000 000 000 zł.");
        if (input.SplitMethod is not ("Amount" or "Percentage")) throw Invalid("Wybierz podział kwotowy lub procentowy.");
        if (input.Shares is null || input.Shares.Count > 1000 || input.Shares.Any(x => x is null || x.ParticipantId == Guid.Empty)
            || input.Shares.Select(x => x.ParticipantId).Distinct().Count() != input.Shares.Count)
            throw Invalid("Nieprawidłowa lub powtórzona osoba w podziale.");
        var percentage = input.SplitMethod == "Percentage";
        if (input.Shares.Any(x => x.AmountMinor < 0 || x.AmountMinor > MaximumAmountMinor || x.PercentageBps < 0 || x.PercentageBps > 10000
            || (percentage ? x.AmountMinor != 0 : x.PercentageBps != 0)))
            throw Invalid("Udziały muszą być dodatnie lub zerowe. Nie mieszaj kwot i procentów.");
        if (percentage && input.Shares.Sum(x => (long)x.PercentageBps) > 10000)
            throw Invalid("Suma procentów nie może przekraczać 100%.");
        var rows = input.Shares.Select(x => new ExpenseAllocationDto(x.ParticipantId,
            percentage ? (long)decimal.Floor((decimal)input.AmountMinor * x.PercentageBps / 10000m) : x.AmountMinor,
            percentage && x.PercentageBps > 0 ? x.PercentageBps : null)).ToList();
        var remaining = input.AmountMinor - rows.Sum(x => x.AmountMinor);
        if (remaining < 0) throw Invalid("Suma podziału przekracza kwotę pozycji.");
        if (input.Balance && remaining > 0)
        {
            var maximum = rows.Count == 0 ? 0 : rows.Max(x => x.AmountMinor);
            var largest = rows.Where(x => x.AmountMinor == maximum).ToList();
            // A tie always goes to the item author, even when not selected yet.
            var target = largest.Count == 1 ? largest[0].ParticipantId : authorId;
            var index = rows.FindIndex(x => x.ParticipantId == target);
            if (index < 0) rows.Add(new(target, remaining, null));
            else rows[index] = rows[index] with { AmountMinor = checked(rows[index].AmountMinor + remaining) };
            remaining = 0;
        }
        return new(rows, remaining);
    }
    private static AppException Invalid(string message) => new(ErrorKind.Validation, message);
}
