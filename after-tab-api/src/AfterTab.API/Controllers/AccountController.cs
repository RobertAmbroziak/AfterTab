using System.Security.Claims;
using System.Security.Cryptography;
using AfterTab.API.Security;
using AfterTab.BL.Interfaces.Users;
using AfterTab.DAL.EF;
using AfterTab.DAL.Entities.Enums;
using AfterTab.DAL.Entities.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AfterTab.API.Controllers;

[ApiController, AllowAnonymous, Route("api/auth/account")]
[EnableRateLimiting("account-browser")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(AccountService accounts, AppDbContext db, ISessionService sessions,
    IConfiguration config, IAuthenticationSchemeProvider schemes) : ControllerBase
{
    public const string CookieName = "__Host-aftertab_account";
    private const string BrowserCookie = "__Host-aftertab_account_flow";
    public sealed record Registration(string Email, string Password, string DisplayName);
    public sealed record EmailRequest(string Email);
    public sealed record CompleteRequest(string Token, string? Password);
    public sealed record PrepareRequest(string Action, string Challenge, string State, string ReturnOrigin, string? Email, string? Password);
    public sealed record ExchangeRequest(string Code, string Verifier);

    private bool AllowedOrigin(string origin) => (config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []).Contains(origin, StringComparer.Ordinal);
    private bool BrowserOrigin() => AllowedOrigin(Request.Headers.Origin.ToString());
    [HttpGet("providers")]
    public async Task<IActionResult> Providers() => Ok(new { google = await schemes.GetSchemeAsync("Google") is not null,
        facebook = await schemes.GetSchemeAsync("Facebook") is not null });
    [HttpPost("register")]
    [EnableRateLimiting("account-mail")]
    public async Task<IActionResult> Register(Registration input, CancellationToken ct)
    {
        if (!BrowserOrigin()) return BadRequest();
        await accounts.RegisterAsync(input.Email, input.Password, input.DisplayName, SessionAuthenticationHandler.ReadToken(Request), ct);
        return Ok(new { message = "Jeśli rejestracja jest możliwa, wysłaliśmy wiadomość. Sprawdź pocztę albo skorzystaj z odzyskiwania hasła." });
    }
    [HttpPost("forgot-password")]
    [EnableRateLimiting("account-mail")]
    public Task<IActionResult> Forgot(EmailRequest input, CancellationToken ct) => Send(input, true, ct);
    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("account-mail")]
    public Task<IActionResult> Resend(EmailRequest input, CancellationToken ct) => Send(input, false, ct);
    private async Task<IActionResult> Send(EmailRequest input, bool reset, CancellationToken ct)
    {
        if (!BrowserOrigin()) return BadRequest();
        await accounts.SendAsync(input.Email, reset, ct);
        return Ok(new { message = "Jeśli adres pasuje do konta, wysłaliśmy wiadomość. Sprawdź skrzynkę i spam." });
    }
    [HttpPost("complete-email")]
    [EnableRateLimiting("account-credentials")]
    public async Task<IActionResult> Complete(CompleteRequest input, CancellationToken ct)
    {
        if (!BrowserOrigin()) return BadRequest();
        await accounts.CompleteEmailAsync(input.Token, input.Password, ct);
        return Ok(new { message = input.Password is null ? "E-mail potwierdzony. Możesz się zalogować." : "Hasło zmienione. Zaloguj się ponownie." });
    }
    [HttpPost("prepare")]
    [EnableRateLimiting("account-credentials")]
    public async Task<IActionResult> Prepare(PrepareRequest input, CancellationToken ct)
    {
        if (!AllowedOrigin(input.ReturnOrigin) || Request.Headers.Origin.ToString() != input.ReturnOrigin
            || !AccountService.IsSecret(input.Challenge) || !AccountService.IsSecret(input.State)
            || input.Action is not ("login" or "Google" or "Facebook" or "restore" or "logout")) return BadRequest();
        Guid? id = null;
        if (input.Action == "login") id = await accounts.LoginAsync(input.Email ?? "", input.Password ?? "", ct);
        else if (input.Action is "Google" or "Facebook")
        {
            if (await schemes.GetSchemeAsync(input.Action) is null) return BadRequest("Dostawca logowania nie jest skonfigurowany.");
            var profile = await sessions.ValidateAsync(SessionAuthenticationHandler.ReadToken(Request), ct);
            if (profile?.User.IsGuest == true) id = profile.User.Id;
        }
        else if (input.Action == "logout")
        {
            var token = SessionAuthenticationHandler.ReadToken(Request);
            var profile = await sessions.ValidateAsync(token, ct);
            id = profile?.User.IsGuest == false ? profile.User.Id : null;
            if (token is not null) await sessions.RevokeAsync(token, ct);
        }
        var request = accounts.AddAction(id, "flow-" + input.Action, TimeSpan.FromMinutes(5), input.Challenge, input.State, input.ReturnOrigin);
        await db.SaveChangesAsync(ct);
        return Ok(new { request });
    }
    [HttpGet("continue")]
    public IActionResult Continue()
        => ContinuePage(false);

    [HttpGet("remember")]
    public IActionResult Remember() => ContinuePage(true);

    private IActionResult ContinuePage(bool remembering)
    {
        if (!remembering && !AccountService.IsSecret(Request.Cookies[BrowserCookie]))
            Response.Cookies.Append(BrowserCookie, AccountService.Secret(), CookieOptions(DateTimeOffset.UtcNow.AddMinutes(10)));
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        Response.Headers["Referrer-Policy"] = "same-origin";
        var origins = string.Join(" ", config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []);
        Response.Headers["Content-Security-Policy"] = $"default-src 'none'; script-src 'nonce-{nonce}'; form-action 'self' {origins} https://accounts.google.com https://www.facebook.com; base-uri 'none'; frame-ancestors 'none'";
        return Content("""
            <!doctype html><html lang="pl"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <title>AfterTab — logowanie</title><p>Łączenie z kontem…</p><noscript>Włącz JavaScript i wróć do AfterTab.</noscript>
            <script nonce="NONCE_VALUE">
            const request = new URLSearchParams(location.search).get('request');
            const verifier = new URLSearchParams(location.hash.slice(1)).get('verifier');
            const action = new URLSearchParams(location.search).get('action');
            history.replaceState(null, '', location.pathname);
            const remembering = REMEMBER_VALUE;
            if (!request || !verifier || (!remembering && !['login','Google','Facebook','restore','logout'].includes(action))) document.querySelector('p').textContent='Wróć do AfterTab i ponów logowanie.';
            else {
              const f=document.createElement('form'); f.method='POST'; f.action='/api/auth/account/'+(remembering ? 'remember-authorize' : 'authorize');
              for(const [name,value] of Object.entries({request,verifier,...(remembering ? {} : {action})})) {const i=document.createElement('input');i.type='hidden';i.name=name;i.value=value;f.append(i);}
              document.body.append(f);f.submit();
            }
            </script></html>
            """.Replace("NONCE_VALUE", nonce).Replace("REMEMBER_VALUE", remembering ? "true" : "false"), "text/html; charset=utf-8");
    }
    [HttpPost("authorize")]
    public async Task<IActionResult> AuthorizeBrowser([FromForm] string request, [FromForm] string verifier, [FromForm] string action, CancellationToken ct)
    {
        if (Request.Headers.Origin.ToString() != $"{Request.Scheme}://{Request.Host}"
            || action is not ("login" or "Google" or "Facebook" or "restore" or "logout")) return BadRequest();
        var flow = await accounts.FindAsync(request, "flow-" + action, ct);
        var browser = Request.Cookies[BrowserCookie];
        if (flow is null || !AccountService.Matches(flow.Challenge, verifier) || !AccountService.IsSecret(browser)) return BadRequest("Logowanie wygasło. Wróć do aplikacji.");
        flow.BrowserHash = AccountService.Hash(browser!);
        accounts.Consume(flow);
        if (action is "Google" or "Facebook")
        {
            // Protected OAuth state + framework correlation cookie protect the provider callback.
            var callback = accounts.AddAction(flow.UserId, "oauth-" + action, TimeSpan.FromMinutes(5), flow.Challenge, flow.State, flow.ReturnOrigin);
            db.AccountActions.Local.Single(x => x.TokenHash == AccountService.Hash(callback)).BrowserHash = flow.BrowserHash;
            await accounts.SaveOnceAsync(ct);
            var properties = new AuthenticationProperties { RedirectUri = "/api/auth/account/external-complete" };
            properties.Items["flow"] = callback; properties.Items["provider"] = action;
            return Challenge(properties, action);
        }
        Guid? userId = flow.UserId;
        if (action is "restore" or "logout")
        {
            var cookie = Request.Cookies[CookieName];
            var profile = await sessions.ValidateAsync(cookie, ct);
            userId = profile?.User.IsGuest == false ? profile.User.Id : null;
            if (action == "logout")
            {
                if (cookie is not null && profile?.User.Id == flow.UserId)
                {
                    await sessions.RevokeAsync(cookie, ct);
                    Response.Cookies.Delete(CookieName, CookieOptions());
                }
                userId = null;
            }
        }
        return await FinishAsync(flow, userId, action == "login", ct);
    }
    [HttpGet("external-complete")]
    public async Task<IActionResult> ExternalComplete(CancellationToken ct)
    {
        var external = await HttpContext.AuthenticateAsync("External");
        await HttpContext.SignOutAsync("External");
        if (!external.Succeeded || external.Properties is null) return BadRequest("Logowanie nie powiodło się. Wróć do AfterTab.");
        external.Properties.Items.TryGetValue("flow", out var secret);
        external.Properties.Items.TryGetValue("provider", out var provider);
        if (provider is not ("Google" or "Facebook")) return BadRequest();
        var flow = await accounts.FindAsync(secret, "oauth-" + provider, ct);
        if (flow is null) return BadRequest();
        var subject = external.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (subject is null) return BadRequest();
        var userId = await accounts.ExternalAsync(Enum.Parse<IdentityProvider>(provider), subject,
            external.Principal.FindFirstValue(ClaimTypes.Name), external.Principal.FindFirstValue(ClaimTypes.Email), flow.UserId, ct);
        accounts.Consume(flow);
        return await FinishAsync(flow, userId, true, ct);
    }
    private async Task<IActionResult> FinishAsync(AccountAction flow, Guid? userId, bool remember, CancellationToken ct)
    {
        var code = accounts.AddAction(userId, remember ? "exchange-remember" : "exchange", TimeSpan.FromMinutes(1), flow.Challenge, flow.State, flow.ReturnOrigin);
        db.AccountActions.Local.Single(x => x.TokenHash == AccountService.Hash(code)).BrowserHash = flow.BrowserHash;
        await accounts.SaveOnceAsync(ct);
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return Redirect(flow.ReturnOrigin + "/#account-code=" + code + "&state=" + flow.State);
    }
    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange(ExchangeRequest input, CancellationToken ct)
    {
        var flow = await accounts.FindAsync(input.Code, "exchange", ct) ?? await accounts.FindAsync(input.Code, "exchange-remember", ct);
        if (flow is null || !AccountService.Matches(flow.Challenge, input.Verifier)
            || Request.Headers.Origin.ToString() != flow.ReturnOrigin) return BadRequest();
        accounts.Consume(flow);
        var session = flow.UserId.HasValue ? await accounts.IssueSessionAsync(flow.UserId.Value, ct) : null;
        string? rememberRequest = null;
        if (session is not null && flow.Purpose == "exchange-remember")
        {
            // Only the SPA that checked its own state can obtain this handle. It is also
            // bound to the first-party browser cookie; forwarding it to another browser fails.
            rememberRequest = accounts.AddAction(flow.UserId, "remember", TimeSpan.FromMinutes(1), flow.Challenge, flow.State, flow.ReturnOrigin);
            db.AccountActions.Local.Single(x => x.TokenHash == AccountService.Hash(rememberRequest)).BrowserHash = flow.BrowserHash;
        }
        await accounts.SaveOnceAsync(ct);
        return Ok(new { session, remembered = session is not null, rememberRequest });
    }
    [HttpPost("remember-authorize")]
    public async Task<IActionResult> RememberAuthorize([FromForm] string request, [FromForm] string verifier, CancellationToken ct)
    {
        if (Request.Headers.Origin.ToString() != $"{Request.Scheme}://{Request.Host}") return BadRequest();
        var flow = await accounts.FindAsync(request, "remember", ct);
        var browser = Request.Cookies[BrowserCookie];
        if (flow?.UserId is not Guid userId || !AccountService.Matches(flow.Challenge, verifier)
            || !AccountService.Matches(flow.BrowserHash, browser)) return BadRequest("Logowanie pochodzi z innej przeglądarki lub wygasło. Wróć do AfterTab.");
        accounts.Consume(flow);
        var session = await accounts.IssueSessionAsync(userId, ct, persistentCookie: true);
        await accounts.SaveOnceAsync(ct);
        Response.Cookies.Append(CookieName, session.Token, CookieOptions(session.ExpiresAtUtc));
        Response.Headers["Referrer-Policy"] = "no-referrer";
        return Redirect(flow.ReturnOrigin + "/#account-ready=" + flow.State);
    }
    private static CookieOptions CookieOptions(DateTimeOffset? expires = null) => new()
    { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax, Path = "/", Expires = expires, IsEssential = true };
}
