using AfterTab.BL.DTO.Events;
namespace AfterTab.BL.Interfaces.Events;
public interface ISettlementService
{
    Task<SettlementDto> GetAsync(Guid eventId, CancellationToken ct);
    Task<SettlementDto> SetClosedAsync(Guid eventId, bool closed, CancellationToken ct);
    Task<SettlementDto> MarkAsync(Guid eventId, MarkTransferDto input, CancellationToken ct);
}
