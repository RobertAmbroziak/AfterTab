using AfterTab.DAL.Entities.Users;
namespace AfterTab.DAL.Interfaces.Users;
public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<bool> HasIdentityAsync(long id, CancellationToken cancellationToken = default);
    Task UpdateDisplayNameAsync(long id, string name, CancellationToken cancellationToken = default);
}
