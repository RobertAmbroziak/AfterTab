using AfterTab.DAL.Entities.Users;
namespace AfterTab.DAL.Interfaces.Users;
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasIdentityAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateDisplayNameAsync(Guid id, string name, CancellationToken cancellationToken = default);
}
