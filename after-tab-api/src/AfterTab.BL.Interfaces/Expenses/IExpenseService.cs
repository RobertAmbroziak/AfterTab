using AfterTab.BL.DTO.Expenses;
namespace AfterTab.BL.Interfaces.Expenses;
public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseDto>> ListAsync(long eventId, CancellationToken ct);
    Task<ExpenseDto> CreateAsync(long eventId, CreateExpenseDto input, CancellationToken ct);
    Task<ExpenseDto> UpdateDetailsAsync(long eventId, long expenseId, UpdateExpenseDetailsDto input, CancellationToken ct);
    Task<ExpenseDto> UpdateSplitAsync(long eventId, long expenseId, UpdateExpenseSplitDto input, CancellationToken ct);
    Task<ExpenseDto> UpdateBalanceAsync(long eventId, long expenseId, UpdateExpenseBalanceDto input, CancellationToken ct);
    Task<ExpenseDto> UpdatePayerAsync(long eventId, long expenseId, UpdateExpensePayerDto input, CancellationToken ct);
}
