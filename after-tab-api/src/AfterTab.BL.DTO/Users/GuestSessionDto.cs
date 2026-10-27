namespace AfterTab.BL.DTO.Users;

public sealed record SessionProfileDto(Guid SessionId, UserDto User, DateTimeOffset ExpiresAtUtc);
public sealed record GuestSessionDto(string Token, UserDto User, DateTimeOffset ExpiresAtUtc);
