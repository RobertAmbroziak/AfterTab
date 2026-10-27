using AfterTab.BL.DTO.Users;

namespace AfterTab.BL.Interfaces.Users;

public interface ISessionService
{
    Task<GuestSessionDto> CreateGuestAsync(string? existingToken, CancellationToken cancellationToken);
    Task<SessionProfileDto?> ValidateAsync(string? token, CancellationToken cancellationToken);
    Task RevokeAsync(string token, CancellationToken cancellationToken);
}
