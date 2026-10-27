using AfterTab.BL.DTO.Events;
using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Interfaces.Events;
using AfterTab.Common.Errors;
using AfterTab.Common.Security;
using AfterTab.DAL.Interfaces.Events;
namespace AfterTab.BL.Services.Events;
public sealed class SettlementService(ISettlementRepository settlements, ICurrentUser actor, TimeProvider clock) : ISettlementService
{
    private Guid Actor => actor.UserId ?? throw new AppException(ErrorKind.Forbidden, "Zaloguj się ponownie.");
    public async Task<SettlementDto> GetAsync(Guid eventId, CancellationToken ct) => Map(await settlements.ReadAsync(eventId, Actor, ct));
    public async Task<SettlementDto> SetClosedAsync(Guid eventId, bool closed, CancellationToken ct)
        => Map(await settlements.SetClosedAsync(eventId, Actor, closed, clock.GetUtcNow(), ct));
    public async Task<SettlementDto> MarkAsync(Guid eventId, MarkTransferDto input, CancellationToken ct)
    {
        var current = await GetAsync(eventId, ct);
        if (!current.IsFinal || current.Version != input.Version) throw new AppException(ErrorKind.Conflict, "Przelewy można oznaczać tylko w aktualnym, zamkniętym rozliczeniu.");
        if ((input.Side != "Sent" && input.Side != "Received") || (input.Side == "Sent" ? input.FromUserId : input.ToUserId) != Actor)
            throw new AppException(ErrorKind.Forbidden, "Możesz oznaczyć tylko własne wysłanie lub odbiór przelewu.");
        if (!current.Transfers.Any(x => x.FromUserId == input.FromUserId && x.ToUserId == input.ToUserId && x.AmountMinor == input.AmountMinor))
            throw new AppException(ErrorKind.Validation, "Taki przelew nie występuje w rozliczeniu.");
        await settlements.MarkAsync(eventId, Actor, input.Version, input.FromUserId, input.ToUserId, input.AmountMinor, input.Side, input.Value, clock.GetUtcNow(), ct);
        return await GetAsync(eventId, ct);
    }
    private SettlementDto Map(SettlementReadModel data)
    {
        var result = SettlementCalculator.Calculate(data.Event.Event.Status.ToString(),
        data.Event.Event.SettlementVersion, data.Event.Event.ClosedAtUtc,
        data.Event.Participants.Select(x => new EventMemberDto(x.Participant.UserId, x.DisplayName, x.Participant.UserId == data.Event.Event.OwnerUserId)).ToList(),
        data.Expenses.Select(x => new ExpenseDto(x.Expense.Id, x.Expense.Name, x.Expense.AmountMinor, x.AuthorName,
            x.AuthorUserId, x.Expense.SplitMethod.ToString(), x.Expense.Balanced, x.Expense.OthersAmountMinor,
            x.Splits.Select(s => new ExpensePersonDto(s.UserId, s.DisplayName, s.AmountMinor, s.PercentageBps)).ToList(), null, x.PaidByUserId, x.PayerName)).ToList());
        var names = result.Balances.ToDictionary(x => x.UserId, x => x.DisplayName);
        var history = data.Confirmations?.Where(x => (x.SettlementVersion < result.Version || !result.IsFinal)
            && (x.SentAtUtc != null || x.ReceivedAtUtc != null)).Select(x => new HistoricalPaymentDto(x.SettlementVersion,
                names.GetValueOrDefault(x.FromUserId, "Uczestnik"), names.GetValueOrDefault(x.ToUserId, "Uczestnik"), x.AmountMinor,
                x.SentAtUtc != null, x.ReceivedAtUtc != null)).ToList();
        return result with { PreviousPayments = history?.Count > 0 ? history : null, Transfers = result.Transfers.Select(x =>
        {
            if (!result.IsFinal || (x.FromUserId != Actor && x.ToUserId != Actor)) return x;
            var row = data.Confirmations?.SingleOrDefault(c => c.SettlementVersion == result.Version && c.FromUserId == x.FromUserId && c.ToUserId == x.ToUserId);
            return x with { Acknowledgement = new(row?.SentAtUtc != null, row?.ReceivedAtUtc != null, x.FromUserId == Actor, x.ToUserId == Actor) };
        }).ToList() };
    }
}
