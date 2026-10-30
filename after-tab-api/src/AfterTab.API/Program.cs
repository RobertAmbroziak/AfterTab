using AfterTab.API.Security;
using AfterTab.BL.Services;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AfterTab.API.Errors.AppExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<IAccountMail, AccountMail>();
var authentication = builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { })
    .AddCookie("External", options =>
    {
        options.Cookie.Name = "__Host-aftertab_external";
        options.Cookie.HttpOnly = true; options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
    });
if (!string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientId"]) && !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientSecret"]))
    authentication.AddGoogle("Google", options =>
    {
        options.SignInScheme = "External";
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.SaveTokens = false;
        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.Redirect(builder.Configuration["Authentication:FrontendOrigin"] + "/#account-error=provider");
            return Task.CompletedTask;
        };
    });
if (!string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Facebook:AppId"]) && !string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Facebook:AppSecret"]))
    authentication.AddFacebook("Facebook", options =>
    {
        options.SignInScheme = "External";
        options.AppId = builder.Configuration["Authentication:Facebook:AppId"]!;
        options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]!;
        options.SaveTokens = false;
        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            context.Response.Redirect(builder.Configuration["Authentication:FrontendOrigin"] + "/#account-error=provider");
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("account-browser", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("account-credentials", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("account-mail", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("invites", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("guest-browser", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 40,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
    options.AddPolicy("guest", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddAfterTabBusiness();
builder.Services.AddAfterTabData(builder.Configuration.GetConnectionString("AfterTab")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:AfterTab"));
var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
{
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
var app = builder.Build();
app.UseExceptionHandler();
app.UseHttpsRedirection();
// Public ownership-verification files must be available without an API session.
app.UseStaticFiles();
app.UseCors("Frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
public partial class Program { }
