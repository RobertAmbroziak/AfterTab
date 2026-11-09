using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Interfaces.Users;
using Microsoft.EntityFrameworkCore;
namespace AfterTab.DAL.EF.Repositories;
public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        => db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> HasIdentityAsync(long id, CancellationToken cancellationToken = default)
        => db.UserIdentities.AnyAsync(x => x.UserId == id, cancellationToken);
    public async Task UpdateDisplayNameAsync(long id, string name, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleAsync(x => x.Id == id, cancellationToken);
        user.DisplayName = name;
        user.DisplayNameConfirmed = true;
        await db.SaveChangesAsync(cancellationToken);
    }
}
