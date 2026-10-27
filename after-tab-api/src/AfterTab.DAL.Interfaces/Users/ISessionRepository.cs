using AfterTab.DAL.Entities.Users;

namespace AfterTab.DAL.Interfaces.Users;

public interface ISessionRepository
{
    Task CreateGuestAsync(User user, Session session, CancellationToken cancellationToken);
    Task<Session?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);
    Task<Session?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
    Task RevokeAsync(Session session, DateTimeOffset now, CancellationToken cancellationToken);
}
