namespace AfterTab.BL.DTO.Users;
public sealed record UserDto(long Id, string DisplayName, bool IsGuest, bool RequiresDisplayName = false);
public sealed record UpdateDisplayNameDto(string DisplayName);
