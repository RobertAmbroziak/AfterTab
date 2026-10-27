using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Interfaces.Users;
using Microsoft.EntityFrameworkCore;
namespace AfterTab.DAL.EF.Repositories;
public sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
    public Task<bool> HasIdentityAsync(Guid id, CancellationToken cancellationToken = default)
        => db.UserIdentities.AnyAsync(x => x.UserId == id, cancellationToken);
    public async Task UpdateDisplayNameAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.SingleAsync(x => x.Id == id, cancellationToken);
        user.DisplayName = name;
        user.DisplayNameConfirmed = true;
        await db.SaveChangesAsync(cancellationToken);
    }
}
