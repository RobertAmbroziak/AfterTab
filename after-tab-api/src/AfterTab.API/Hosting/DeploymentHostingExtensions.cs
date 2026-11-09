using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace AfterTab.API.Hosting;

public static class DeploymentHostingExtensions
{
    public static IServiceCollection AddDeploymentHosting(this IServiceCollection services, IConfiguration configuration)
    {
        var keysPath = configuration["DataProtection:KeysPath"];
        if (!string.IsNullOrWhiteSpace(keysPath))
            services.AddDataProtection().SetApplicationName("AfterTab")
                .PersistKeysToFileSystem(new DirectoryInfo(keysPath));

        if (configuration.GetValue<bool>("ReverseProxy:Enabled"))
        {
            var configuredProxies = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
            if (configuredProxies.Length == 0)
                throw new InvalidOperationException("ReverseProxy:KnownProxies is required when ReverseProxy:Enabled is true.");
            var proxies = configuredProxies.Select(IPAddress.Parse).ToArray();
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = 1;
                options.RequireHeaderSymmetry = true;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
                foreach (var proxy in proxies) options.KnownProxies.Add(proxy);
            });
        }
        return services;
    }

    public static WebApplication UseDeploymentHosting(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>("ReverseProxy:Enabled")) app.UseForwardedHeaders();
        return app;
    }
}
