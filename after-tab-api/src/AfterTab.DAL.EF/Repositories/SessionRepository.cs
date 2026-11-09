using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Interfaces.Users;
using Microsoft.EntityFrameworkCore;

namespace AfterTab.DAL.EF.Repositories;

public sealed class SessionRepository(AppDbContext db) : ISessionRepository
{
    public async Task CreateGuestAsync(User user, Session session, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        session.UserId = user.Id;
        db.Sessions.Add(session);
        // A single SaveChanges transaction commits both records or neither.
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<Session?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
        => db.Sessions.SingleOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);
    public Task<Session?> FindByIdAsync(long id, CancellationToken cancellationToken)
        => db.Sessions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task RevokeAsync(Session session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        session.RevokedAtUtc = now;
        await db.SaveChangesAsync(cancellationToken);
    }
}
