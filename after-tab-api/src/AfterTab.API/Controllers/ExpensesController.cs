using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Interfaces.Expenses;
using AfterTab.API.Contracts;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AfterTab.API.Controllers;
[ApiController, Authorize, Route("api/events/{eventId:guid}/expenses")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExpensesController(IExpenseService expenses, PublicIds ids) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid eventId, CancellationToken ct) => Ok(await expenses.ListAsync(await ids.ResolveAsync<Event>(eventId, ct), ct));
    [HttpPost]
    public async Task<IActionResult> Create(Guid eventId, PublicCreateExpense input, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await expenses.CreateAsync(await ids.ResolveAsync<Event>(eventId, ct),
            new CreateExpenseDto(input.Name, input.AmountMinor, input.SplitMethod, await ids.SharesAsync(input.Shares, ct), input.Balance,
                await ids.ParticipantAsync(input.PaidByParticipantId, ct), await ids.ParticipantAsync(input.BalanceParticipantId, ct)), ct));
    [HttpPut("{expenseId:guid}/split")]
    public async Task<IActionResult> UpdateSplit(Guid eventId, Guid expenseId, PublicUpdateExpenseSplit input, CancellationToken ct)
        => Ok(await expenses.UpdateSplitAsync(await ids.ResolveAsync<Event>(eventId, ct), await ids.ResolveAsync<Expense>(expenseId, ct),
            new UpdateExpenseSplitDto(input.SplitMethod, await ids.SharesAsync(input.Shares, ct), input.Balance, input.RowVersion,
                await ids.ParticipantAsync(input.BalanceParticipantId, ct), input.Name, input.AmountMinor), ct));
    [HttpPut("{expenseId:guid}/details")]
    public async Task<IActionResult> UpdateDetails(Guid eventId, Guid expenseId, PublicUpdateExpenseDetails input, CancellationToken ct)
        => Ok(await expenses.UpdateDetailsAsync(await ids.ResolveAsync<Event>(eventId, ct), await ids.ResolveAsync<Expense>(expenseId, ct),
            new UpdateExpenseDetailsDto(input.Name, input.AmountMinor, input.RowVersion), ct));
    [HttpPut("{expenseId:guid}/balance")]
    public async Task<IActionResult> UpdateBalance(Guid eventId, Guid expenseId, PublicUpdateExpenseBalance input, CancellationToken ct)
        => Ok(await expenses.UpdateBalanceAsync(await ids.ResolveAsync<Event>(eventId, ct), await ids.ResolveAsync<Expense>(expenseId, ct),
            new UpdateExpenseBalanceDto(await ids.ParticipantAsync(input.ParticipantId, ct), input.RowVersion), ct));
    [HttpPut("{expenseId:guid}/payer")]
    public async Task<IActionResult> UpdatePayer(Guid eventId, Guid expenseId, PublicUpdateExpensePayer input, CancellationToken ct)
        => Ok(await expenses.UpdatePayerAsync(await ids.ResolveAsync<Event>(eventId, ct), await ids.ResolveAsync<Expense>(expenseId, ct),
            new UpdateExpensePayerDto(await ids.ResolveAsync<EventParticipant>(input.PaidByParticipantId, ct), input.RowVersion), ct));
}
