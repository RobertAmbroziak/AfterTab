namespace AfterTab.BL.DTO.Users;

public sealed record GuestBrowserPrepareDto(string Challenge, string State, string ReturnOrigin, string Action = "login", string? DisplayName = null);
public sealed record GuestBrowserExchangeDto(string Code, string Verifier);
public sealed record GuestBrowserResultDto(GuestSessionDto? Session, bool Remembered);
public sealed record GuestBrowserRedirectDto(string ReturnOrigin, string State, string Code, string? DeviceToken, DateTimeOffset? DeviceExpiresAtUtc);
