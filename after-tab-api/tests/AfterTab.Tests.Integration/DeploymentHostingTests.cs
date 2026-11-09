using System.Net;
using System.Net.Http.Json;
using AfterTab.API.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class DeploymentHostingTests
{
    private sealed record Probe(string Scheme, string ClientIp);

    [TestMethod]
    [DataRow(true, "172.30.37.10", "https", "203.0.113.7")]
    [DataRow(true, "198.51.100.1", "http", "198.51.100.1")]
    [DataRow(false, "172.30.37.10", "http", "172.30.37.10")]
    public async Task Forwarded_Headers_Require_OptIn_And_A_Trusted_Proxy(bool enabled, string peer, string expectedScheme, string expectedIp)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ReverseProxy:Enabled"] = enabled.ToString(),
            ["ReverseProxy:KnownProxies:0"] = "172.30.37.10"
        });
        builder.Services.AddDeploymentHosting(builder.Configuration);
        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
            return next(context);
        });
        app.UseDeploymentHosting();
        app.MapGet("/probe", (HttpContext context) => new Probe(context.Request.Scheme, context.Connection.RemoteIpAddress!.ToString()));
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/probe");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");
        request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Probe>();
        Assert.IsNotNull(result);
        Assert.AreEqual(expectedScheme, result.Scheme);
        Assert.AreEqual(expectedIp, result.ClientIp);
    }

    [TestMethod]
    public void Enabling_Proxy_Without_A_Trust_List_Fails_Instead_Of_Trusting_Everyone()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ReverseProxy:Enabled"] = "true" }).Build();
        Assert.ThrowsExactly<InvalidOperationException>(() => new ServiceCollection().AddDeploymentHosting(configuration));
    }

    [TestMethod]
    public void Persistent_DataProtection_Keys_Survive_Recreating_The_Application()
    {
        var directory = Path.Combine(Path.GetTempPath(), "aftertab-keys-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["DataProtection:KeysPath"] = directory }).Build();
            string protectedValue;
            using (var first = new ServiceCollection().AddLogging().AddDeploymentHosting(configuration).BuildServiceProvider())
            {
                protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-cookie").Protect("test-value");
            }
            using (var second = new ServiceCollection().AddLogging().AddDeploymentHosting(configuration).BuildServiceProvider())
            {
                Assert.AreEqual("test-value", second.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("test-cookie").Unprotect(protectedValue));
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
