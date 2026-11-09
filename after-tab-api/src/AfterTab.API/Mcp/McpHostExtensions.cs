using AfterTab.Mcp;
using Microsoft.AspNetCore.Authentication;
using System.Threading.RateLimiting;

namespace AfterTab.API.Mcp;

public static class McpHostExtensions
{
    public static void AddAfterTabMcpHost(this IServiceCollection services, IConfiguration configuration,
        AuthenticationBuilder authentication)
    {
        services.AddAfterTabMcp(configuration);
        // Management controllers are present even when disabled, returning 404 without touching data.
        services.AddScoped<McpTokenService>();
        services.AddScoped<McpOAuthService>();
        services.AddRateLimiter(options => options.AddPolicy("mcp-oauth-register", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 20, Window = TimeSpan.FromHours(1), QueueLimit = 0 })));
        if (!configuration.GetValue<bool>("Mcp:Enabled")) return;
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("mcp", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        services.AddScoped<IAfterTabMcpApplication, AfterTabMcpApplication>();
        services.AddScoped<IMcpWriteStore, McpWriteStore>();
        authentication.AddScheme<AuthenticationSchemeOptions, McpAuthenticationHandler>(McpOptions.AuthenticationScheme, _ => { });
        services.AddAuthorization(options => options.AddPolicy(McpOptions.ReadPolicy, policy =>
            policy.AddAuthenticationSchemes(McpOptions.AuthenticationScheme).RequireAuthenticatedUser()
                .RequireClaim(McpOptions.ScopeClaim, McpOptions.ReadScope)));
    }
}
