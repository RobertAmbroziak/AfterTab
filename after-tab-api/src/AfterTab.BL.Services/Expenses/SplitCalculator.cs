using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Interfaces.Expenses;
namespace AfterTab.BL.Services.Expenses;
public sealed class SplitCalculator : ISplitCalculator
{
    public IReadOnlyList<SplitAmount> CalculatePercentage(long amountMinor, IReadOnlyList<PercentageShare> shares)
    {
        if (amountMinor <= 0) throw new ArgumentException("Kwota musi być dodatnia.");
        ArgumentNullException.ThrowIfNull(shares);
        if (shares.Count == 0 || shares.Count > 10000)
            throw new ArgumentException("Wymagane jest od 1 do 10000 udziałów.");
        if (shares.Any(x => x is null || x.ParticipantId == Guid.Empty || x.PercentageBps <= 0 || x.PercentageBps > 10000))
            throw new ArgumentException("Nieprawidłowy uczestnik lub procent.");
        if (shares.Select(x => x.ParticipantId).Distinct().Count() != shares.Count)
            throw new ArgumentException("Uczestnik nie może występować dwukrotnie.");
        if (shares.Sum(x => (long)x.PercentageBps) != 10000)
            throw new ArgumentException("Suma procentów musi wynosić 100%.");

        // Largest remainder method; UUID breaks ties independently of input order.
        var rows = shares.Select(x => new
        {
            Share = x,
            Exact = (decimal)amountMinor * x.PercentageBps / 10000m
        }).ToList();
        var amounts = rows.ToDictionary(x => x.Share.ParticipantId, x => (long)decimal.Floor(x.Exact));
        var remainder = amountMinor - amounts.Values.Sum();
        foreach (var row in rows.OrderByDescending(x => x.Exact - decimal.Floor(x.Exact))
                     .ThenBy(x => x.Share.ParticipantId).Take(checked((int)remainder)))
            amounts[row.Share.ParticipantId]++;
        return shares.Select(x => new SplitAmount(x.ParticipantId, amounts[x.ParticipantId], x.PercentageBps)).ToList();
    }
}
