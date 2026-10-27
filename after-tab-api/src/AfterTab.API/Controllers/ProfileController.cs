using AfterTab.BL.DTO.Users;
using AfterTab.BL.Interfaces.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AfterTab.API.Controllers;

[ApiController, Authorize, Route("api/profile")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProfileController(IUserService users) : ControllerBase
{
    [HttpPut("display-name")]
    public async Task<ActionResult<UserDto>> Update(UpdateDisplayNameDto input, CancellationToken ct)
        => Ok(await users.UpdateDisplayNameAsync(input.DisplayName, ct));
}
