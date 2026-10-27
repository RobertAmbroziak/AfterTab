using AfterTab.BL.DTO.Events;
using AfterTab.BL.DTO.Expenses;
namespace AfterTab.BL.Services.Events;
public static class SettlementCalculator
{
    public static SettlementDto Calculate(string status, int version, DateTimeOffset? closedAt,
        IReadOnlyList<EventMemberDto> members, IReadOnlyList<ExpenseDto> expenses)
    {
        var names = members.ToDictionary(x => x.UserId, x => x.DisplayName);
        var paid = new Dictionary<Guid, long>();
        var cost = new Dictionary<Guid, long>();
        var pending = new Dictionary<Guid, long>();
        long total = 0, unassigned = 0;
        foreach (var e in expenses)
        {
            if (e.AmountMinor <= 0 || e.OthersAmountMinor < 0 || e.Splits.Any(s => s.AmountMinor < 0)
                || checked(e.Splits.Sum(s => s.AmountMinor) + e.OthersAmountMinor) != e.AmountMinor)
                throw new InvalidOperationException("Niespójny podział pozycji. Popraw podział przed rozliczeniem.");
            var payerId = e.PaidByUserId ?? e.AuthorUserId;
            names[payerId] = e.PayerName ?? e.AuthorName;
            paid[payerId] = checked(paid.GetValueOrDefault(payerId) + e.AmountMinor);
            pending[payerId] = checked(pending.GetValueOrDefault(payerId) + e.OthersAmountMinor);
            foreach (var split in e.Splits)
            {
                names[split.UserId] = split.DisplayName;
                cost[split.UserId] = checked(cost.GetValueOrDefault(split.UserId) + split.AmountMinor);
            }
            total = checked(total + e.AmountMinor); unassigned = checked(unassigned + e.OthersAmountMinor);
        }
        // Pending money has no debtor yet, so it cannot create a transfer between named people.
        var balances = names.Select(x => new SettlementBalanceDto(x.Key, x.Value, paid.GetValueOrDefault(x.Key),
            cost.GetValueOrDefault(x.Key), pending.GetValueOrDefault(x.Key),
            checked(paid.GetValueOrDefault(x.Key) - pending.GetValueOrDefault(x.Key) - cost.GetValueOrDefault(x.Key))))
            .OrderBy(x => x.DisplayName).ThenBy(x => x.UserId).ToList();
        var remaining = balances.ToDictionary(x => x.UserId, x => x.NetMinor);
        var transfers = new List<SettlementTransferDto>();
        while (true)
        {
            var debtor = remaining.Where(x => x.Value < 0).OrderBy(x => x.Value).ThenBy(x => x.Key).FirstOrDefault();
            var creditor = remaining.Where(x => x.Value > 0).OrderByDescending(x => x.Value).ThenBy(x => x.Key).FirstOrDefault();
            if (debtor.Value == 0 || creditor.Value == 0) break;
            var amount = Math.Min(checked(-debtor.Value), creditor.Value);
            transfers.Add(new(debtor.Key, names[debtor.Key], creditor.Key, names[creditor.Key], amount));
            remaining[debtor.Key] += amount; remaining[creditor.Key] -= amount;
        }
        if (remaining.Values.Any(x => x != 0)) throw new InvalidOperationException("Suma sald rozliczenia musi wynosić zero.");
        return new(status, status == "Closed", version, closedAt, expenses.Count, total, unassigned, balances, transfers,
            expenses.Where(x => x.OthersAmountMinor > 0).Select(x => new UnassignedExpenseDto(x.Id, x.Name, x.AuthorUserId, x.AuthorName, x.OthersAmountMinor)).ToList());
    }
}
