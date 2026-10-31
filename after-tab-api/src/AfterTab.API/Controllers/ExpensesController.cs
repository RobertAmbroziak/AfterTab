using AfterTab.BL.DTO.Expenses;
using AfterTab.BL.Interfaces.Expenses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AfterTab.API.Controllers;
[ApiController, Authorize, Route("api/events/{eventId:guid}/expenses")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ExpensesController(IExpenseService expenses) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid eventId, CancellationToken ct) => Ok(await expenses.ListAsync(eventId, ct));
    [HttpPost]
    public async Task<IActionResult> Create(Guid eventId, CreateExpenseDto input, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await expenses.CreateAsync(eventId, input, ct));
    [HttpPut("{expenseId:guid}/split")]
    public async Task<IActionResult> UpdateSplit(Guid eventId, Guid expenseId, UpdateExpenseSplitDto input, CancellationToken ct)
        => Ok(await expenses.UpdateSplitAsync(eventId, expenseId, input, ct));
    [HttpPut("{expenseId:guid}/balance")]
    public async Task<IActionResult> UpdateBalance(Guid eventId, Guid expenseId, UpdateExpenseBalanceDto input, CancellationToken ct)
        => Ok(await expenses.UpdateBalanceAsync(eventId, expenseId, input, ct));
    [HttpPut("{expenseId:guid}/payer")]
    public async Task<IActionResult> UpdatePayer(Guid eventId, Guid expenseId, UpdateExpensePayerDto input, CancellationToken ct)
        => Ok(await expenses.UpdatePayerAsync(eventId, expenseId, input, ct));
}
