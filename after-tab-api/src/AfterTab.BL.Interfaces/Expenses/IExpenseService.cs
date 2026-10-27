using AfterTab.BL.DTO.Expenses;
namespace AfterTab.BL.Interfaces.Expenses;
public interface IExpenseService
{
    Task<IReadOnlyList<ExpenseDto>> ListAsync(Guid eventId, CancellationToken ct);
    Task<ExpenseDto> CreateAsync(Guid eventId, CreateExpenseDto input, CancellationToken ct);
    Task<ExpenseDto> UpdateSplitAsync(Guid eventId, Guid expenseId, UpdateExpenseSplitDto input, CancellationToken ct);
    Task<ExpenseDto> UpdatePayerAsync(Guid eventId, Guid expenseId, UpdateExpensePayerDto input, CancellationToken ct);
}
