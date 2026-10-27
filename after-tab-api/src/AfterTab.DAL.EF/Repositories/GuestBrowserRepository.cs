using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Interfaces.Users;
using Microsoft.EntityFrameworkCore;

namespace AfterTab.DAL.EF.Repositories;

public sealed class GuestBrowserRepository(AppDbContext db) : IGuestBrowserRepository
{
    public void AddRequest(GuestLoginRequest request) => db.GuestLoginRequests.Add(request);
    public void AddUser(User user) => db.Users.Add(user);
    public void AddDevice(GuestDevice device) => db.GuestDevices.Add(device);
    public void AddSession(Session session) => db.Sessions.Add(session);
    public Task<GuestLoginRequest?> FindRequestAsync(string hash, CancellationToken ct)
        => db.GuestLoginRequests.SingleOrDefaultAsync(x => x.RequestHash == hash, ct);
    public Task<GuestLoginRequest?> FindCodeAsync(string hash, CancellationToken ct)
        => db.GuestLoginRequests.SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
    public Task<GuestDevice?> FindDeviceAsync(string hash, CancellationToken ct)
        => db.GuestDevices.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
    public Task<GuestDevice?> FindDeviceAsync(Guid id, CancellationToken ct)
        => db.GuestDevices.SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task RevokeDeviceSessionsAsync(Guid deviceId, DateTimeOffset now, CancellationToken ct)
    {
        var sessions = await db.Sessions.Where(x => x.GuestDeviceId == deviceId && x.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var session in sessions) session.RevokedAtUtc = now;
    }
    public async Task<bool> SaveAsync(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { db.ChangeTracker.Clear(); return false; }
    }
}
