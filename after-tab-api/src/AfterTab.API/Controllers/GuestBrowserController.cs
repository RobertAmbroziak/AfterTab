using AfterTab.API.Security;
using AfterTab.BL.DTO.Users;
using AfterTab.BL.Interfaces.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Cryptography;

namespace AfterTab.API.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth/guest")]
[EnableRateLimiting("guest-browser")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GuestBrowserController(IGuestBrowserService service, IConfiguration configuration) : ControllerBase
{
    private const string CookieName = "__Host-aftertab_guest";

    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare(GuestBrowserPrepareDto input, CancellationToken ct)
    {
        var allowed = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (!allowed.Contains(input.ReturnOrigin, StringComparer.Ordinal)
            || Request.Headers.Origin.ToString() != input.ReturnOrigin) return BadRequest();
        var request = await service.PrepareAsync(input, SessionAuthenticationHandler.ReadToken(Request), ct);
        return request is null ? BadRequest() : Ok(new { request });
    }

    [HttpGet("continue")]
    public IActionResult Continue()
    {
        Response.Headers["Referrer-Policy"] = "same-origin";
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        // Chromium also checks the form's final redirect against form-action.
        var redirectOrigins = string.Join(" ", origins);
        Response.Headers["Content-Security-Policy"] = $"default-src 'none'; script-src 'nonce-{nonce}'; form-action 'self' {redirectOrigins}; base-uri 'none'; frame-ancestors 'none'";
        // The short-lived proof travels in a fragment (not request logs), then in a same-origin POST.
        // Never mint a remembered-device cookie based on a GET request handle alone.
        return Content("""
            <!doctype html><html lang="pl"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>AfterTab</title><p>Przywracanie gościa…</p><noscript>Włącz JavaScript i wróć do AfterTab.</noscript>
            <script nonce="NONCE_VALUE">
            const request = new URLSearchParams(location.search).get('request');
            const verifier = new URLSearchParams(location.hash.slice(1)).get('verifier');
            history.replaceState(null, '', location.pathname);
            if (!request || !verifier) { document.querySelector('p').textContent = 'Wróć do AfterTab i spróbuj ponownie.'; }
            else {
              const form = document.createElement('form'); form.method = 'POST'; form.action = '/api/auth/guest/authorize';
              for (const [name, value] of Object.entries({ request, verifier })) {
                const input = document.createElement('input'); input.type = 'hidden'; input.name = name; input.value = value; form.append(input);
              }
              document.body.append(form); form.submit();
            }
            </script></html>
            """.Replace("NONCE_VALUE", nonce), "text/html; charset=utf-8");
    }

    [HttpPost("authorize")]
    public async Task<IActionResult> AuthorizeBrowser([FromForm] string request, [FromForm] string verifier, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (Request.Headers.Origin.ToString() != $"{Request.Scheme}://{Request.Host}") return BadRequest();
        var result = await service.ContinueAsync(request, verifier, Request.Cookies[CookieName], ct);
        if (result is null) return BadRequest("Nieprawidłowa lub wygasła próba logowania. Wróć do AfterTab i spróbuj ponownie.");
        if (result.DeviceToken is not null)
            Response.Cookies.Append(CookieName, result.DeviceToken, new CookieOptions
            {
                HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/",
                Expires = result.DeviceExpiresAtUtc, IsEssential = true
            });
        // Only a short-lived, one-use, verifier-bound code goes into the fragment. Never a session or device token.
        return Redirect(result.ReturnOrigin + "/#aftertab-code=" + Uri.EscapeDataString(result.Code)
            + "&state=" + Uri.EscapeDataString(result.State));
    }

    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange(GuestBrowserExchangeDto input, CancellationToken ct)
    {
        var result = await service.ExchangeAsync(input, ct);
        return result is null ? BadRequest() : Ok(result);
    }
}
