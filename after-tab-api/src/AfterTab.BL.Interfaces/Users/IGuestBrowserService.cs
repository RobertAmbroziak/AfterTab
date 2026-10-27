using AfterTab.BL.DTO.Users;

namespace AfterTab.BL.Interfaces.Users;

public interface IGuestBrowserService
{
    Task<string?> PrepareAsync(GuestBrowserPrepareDto input, string? sessionToken, CancellationToken ct);
    Task<GuestBrowserRedirectDto?> ContinueAsync(string request, string verifier, string? deviceToken, CancellationToken ct);
    Task<GuestBrowserResultDto?> ExchangeAsync(GuestBrowserExchangeDto input, CancellationToken ct);
}
