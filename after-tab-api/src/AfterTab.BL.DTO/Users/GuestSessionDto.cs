namespace AfterTab.BL.DTO.Users;

public sealed record SessionProfileDto(long SessionId, UserDto User, DateTimeOffset ExpiresAtUtc);
public sealed record GuestSessionDto(string Token, UserDto User, DateTimeOffset ExpiresAtUtc);
