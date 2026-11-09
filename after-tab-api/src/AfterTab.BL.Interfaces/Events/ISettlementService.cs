using AfterTab.BL.DTO.Events;
namespace AfterTab.BL.Interfaces.Events;
public interface ISettlementService
{
    Task<SettlementDto> GetAsync(long eventId, CancellationToken ct);
    Task<SettlementDto> SetClosedAsync(long eventId, bool closed, CancellationToken ct);
    Task<SettlementDto> MarkAsync(long eventId, MarkTransferDto input, CancellationToken ct);
}
