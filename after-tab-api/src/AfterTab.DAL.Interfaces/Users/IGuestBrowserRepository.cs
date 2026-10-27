using AfterTab.DAL.Entities.Users;

namespace AfterTab.DAL.Interfaces.Users;

public interface IGuestBrowserRepository
{
    void AddRequest(GuestLoginRequest request);
    void AddUser(User user);
    void AddDevice(GuestDevice device);
    void AddSession(Session session);
    Task<GuestLoginRequest?> FindRequestAsync(string hash, CancellationToken ct);
    Task<GuestLoginRequest?> FindCodeAsync(string hash, CancellationToken ct);
    Task<GuestDevice?> FindDeviceAsync(string hash, CancellationToken ct);
    Task<GuestDevice?> FindDeviceAsync(Guid id, CancellationToken ct);
    Task RevokeDeviceSessionsAsync(Guid deviceId, DateTimeOffset now, CancellationToken ct);
    Task<bool> SaveAsync(CancellationToken ct);
}
