using System.Text.Json.Nodes;
using AfterTab.API.Contracts;
using AfterTab.BL.DTO.Events;
using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Interfaces.Events;
using AfterTab.BL.Interfaces.Expenses;
using AfterTab.BL.Interfaces.Users;
using AfterTab.BL.Services.Expenses;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using AfterTab.Mcp;
using ModelContextProtocol;

namespace AfterTab.API.Mcp;

public sealed class AfterTabMcpApplication(PublicIds ids, IUserService users, IEventService events,
    IExpenseService expenses, ISettlementService settlements, McpTokenService tokens) : IAfterTabMcpApplication
{
    public async Task EnsureEventAccessAsync(Guid eventId, CancellationToken ct)
        => _ = await events.GetAsync(await EventId(eventId, ct), ct);
    public async Task<JsonNode?> GetProfileAsync(CancellationToken ct)
        => await ids.ResponseAsync(await users.GetCurrentAsync(ct), ct);
    public async Task<JsonNode?> GetUserInstructionsAsync(CancellationToken ct)
    {
        var result = await tokens.GetInstructionsAsync(ct);
        return new JsonObject { ["instructions"] = result.Instructions };
    }
    public async Task<JsonNode?> ListEventsAsync(int page, CancellationToken ct)
        => await ids.ResponseAsync(await events.ListAsync(page, ct), ct);
    public async Task<JsonNode?> GetEventAsync(Guid eventId, CancellationToken ct)
        => await ids.ResponseAsync(await events.GetAsync(await EventId(eventId, ct), ct), ct);
    public async Task<JsonNode?> CreateEventAsync(string name, string? description, CancellationToken ct)
        => await ids.ResponseAsync((await events.CreateAsync(new CreateEventDto(name, description), ct)).Details, ct);
    public async Task<JsonNode?> AddParticipantAsync(Guid eventId, string displayName, CancellationToken ct)
        => await ids.ResponseAsync(await events.AddVirtualAsync(await EventId(eventId, ct), new(displayName), ct), ct);
    public async Task<JsonNode?> RenameParticipantAsync(Guid eventId, Guid participantId, string displayName, CancellationToken ct)
        => await ids.ResponseAsync(await events.RenameVirtualAsync(await EventId(eventId, ct),
            await ids.ResolveAsync<EventParticipant>(participantId, ct), new(displayName), ct), ct);
    public async Task<JsonNode?> RemoveParticipantAsync(Guid eventId, Guid participantId, CancellationToken ct)
        => await ids.ResponseAsync(await events.RemoveVirtualAsync(await EventId(eventId, ct),
            await ids.ResolveAsync<EventParticipant>(participantId, ct), ct), ct);
    public async Task<JsonNode?> ListExpensesAsync(Guid eventId, CancellationToken ct)
        => await ids.ResponseAsync(await expenses.ListAsync(await EventId(eventId, ct), ct), ct);

    public async Task<JsonNode?> PreviewExpenseAsync(Guid eventId, ExpenseInput input, CancellationToken ct)
    {
        var details = await events.GetAsync(await EventId(eventId, ct), ct);
        var dto = await Expense(input, ct);
        var members = details.Participants.Select(x => x.ParticipantId).ToHashSet();
        if (dto.Shares.Any(x => !members.Contains(x.ParticipantId))
            || (dto.PaidByParticipantId.HasValue && !members.Contains(dto.PaidByParticipantId.Value)))
            throw new McpException("Każde participantId w podziale i płatniku musi należeć do tego wydarzenia. Odczytaj get_event i użyj dokładnych ID zwróconych dla jego uczestników.");
        return await ids.ResponseAsync(ExpenseAllocation.Calculate(dto), ct);
    }

    public async Task<JsonNode?> CreateExpenseAsync(Guid eventId, ExpenseInput input, CancellationToken ct)
        => await ids.ResponseAsync(await expenses.CreateAsync(await EventId(eventId, ct), await Expense(input, ct), ct), ct);
    public async Task<JsonNode?> UpdateSplitAsync(Guid eventId, Guid expenseId, SplitInput input, CancellationToken ct)
    {
        if (input is null) throw new McpException("Supply a split.");
        return await ids.ResponseAsync(await expenses.UpdateSplitAsync(await EventId(eventId, ct),
            await ids.ResolveAsync<Expense>(expenseId, ct), new UpdateExpenseSplitDto(input.SplitMethod,
                await Shares(input.Shares, ct), input.BalanceParticipantId.HasValue, input.RowVersion,
                await ids.ParticipantAsync(input.BalanceParticipantId, ct)), ct), ct);
    }
    public async Task<JsonNode?> SetBalanceAsync(Guid eventId, Guid expenseId, Guid? participantId, string rowVersion, CancellationToken ct)
        => await ids.ResponseAsync(await expenses.UpdateBalanceAsync(await EventId(eventId, ct),
            await ids.ResolveAsync<Expense>(expenseId, ct), new(await ids.ParticipantAsync(participantId, ct), rowVersion), ct), ct);
    public async Task<JsonNode?> SetPayerAsync(Guid eventId, Guid expenseId, Guid participantId, string rowVersion, CancellationToken ct)
        => await ids.ResponseAsync(await expenses.UpdatePayerAsync(await EventId(eventId, ct),
            await ids.ResolveAsync<Expense>(expenseId, ct), new(await ids.ResolveAsync<EventParticipant>(participantId, ct), rowVersion), ct), ct);
    public async Task<JsonNode?> GetSettlementAsync(Guid eventId, CancellationToken ct)
        => await ids.ResponseAsync(await settlements.GetAsync(await EventId(eventId, ct), ct), ct);
    public async Task<JsonNode?> SetClosedAsync(Guid eventId, bool closed, CancellationToken ct)
        => await ids.ResponseAsync(await settlements.SetClosedAsync(await EventId(eventId, ct), closed, ct), ct);
    public async Task<JsonNode?> MarkTransferAsync(Guid eventId, int version, Guid fromParticipantId, Guid toParticipantId,
        long amountMinor, string side, bool value, CancellationToken ct)
        => await ids.ResponseAsync(await settlements.MarkAsync(await EventId(eventId, ct), new(version,
            await ids.ResolveAsync<EventParticipant>(fromParticipantId, ct), await ids.ResolveAsync<EventParticipant>(toParticipantId, ct),
            amountMinor, side, value), ct), ct);

    private Task<long> EventId(Guid eventId, CancellationToken ct) => ids.ResolveAsync<Event>(eventId, ct);
    private async Task<CreateExpenseDto> Expense(ExpenseInput input, CancellationToken ct)
    {
        if (input is null) throw new McpException("Supply an expense.");
        return new(input.Name, input.AmountMinor, input.SplitMethod, await Shares(input.Shares, ct),
            input.BalanceParticipantId.HasValue, await ids.ParticipantAsync(input.PaidByParticipantId, ct),
            await ids.ParticipantAsync(input.BalanceParticipantId, ct));
    }
    private Task<IReadOnlyList<ExpenseShareDto>> Shares(IReadOnlyList<ExpenseShare> shares, CancellationToken ct)
    {
        if (shares is null || shares.Count > 1000 || shares.Any(x => x is null))
            throw new McpException("Supply at most 1000 valid shares.");
        return ids.SharesAsync(shares.Select(x => new PublicExpenseShare(x.ParticipantId, x.AmountMinor, x.PercentageBps)).ToArray(), ct);
    }
}
