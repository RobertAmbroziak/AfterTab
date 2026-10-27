using AfterTab.Common.Security;
using AfterTab.BL.DTO.Users;
using AfterTab.BL.Interfaces.Users;
using AfterTab.DAL.Interfaces.Users;
using AfterTab.DAL.Entities.Enums;
using AfterTab.Common.Errors;
using AfterTab.Common.Validation;
namespace AfterTab.BL.Services.Users;
public sealed class UserService(IUserRepository repository, ICurrentUser currentUser) : IUserService
{
    public async Task<UserDto?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser.UserId is not Guid id) return null;
        var user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null || user.Status != UserStatus.Active) return null;
        var hasIdentity = await repository.HasIdentityAsync(id, cancellationToken);
        return new UserDto(user.Id, user.DisplayName, !hasIdentity, !user.DisplayNameConfirmed);
    }
    public async Task<UserDto> UpdateDisplayNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var profile = await GetCurrentAsync(cancellationToken)
            ?? throw new AppException(ErrorKind.Forbidden, "Zaloguj się ponownie.");
        await repository.UpdateDisplayNameAsync(profile.Id, DisplayNames.Normalize(name), cancellationToken);
        return (await GetCurrentAsync(cancellationToken))!;
    }
}
