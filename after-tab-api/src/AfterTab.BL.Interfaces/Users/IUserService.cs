using AfterTab.BL.DTO.Users;
namespace AfterTab.BL.Interfaces.Users;
public interface IUserService
{
    Task<UserDto?> GetCurrentAsync(CancellationToken cancellationToken = default);
    Task<UserDto> UpdateDisplayNameAsync(string name, CancellationToken cancellationToken = default);
}
