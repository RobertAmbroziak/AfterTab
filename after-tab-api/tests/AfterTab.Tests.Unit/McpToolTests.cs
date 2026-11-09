using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Nodes;
using AfterTab.Mcp;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;

namespace AfterTab.Tests.Unit;

[TestClass]
public sealed class McpToolTests
{
    public class ApplicationProxy : DispatchProxy
    {
        public List<string> Calls { get; } = [];
        protected override object Invoke(MethodInfo? method, object?[]? args)
        {
            Calls.Add(method!.Name);
            return method.ReturnType == typeof(Task) ? Task.CompletedTask
                : Task.FromResult<JsonNode?>(JsonNode.Parse("{\"ok\":true}"));
        }
    }
    private sealed class Writes : IMcpWriteStore
    {
        public int Calls;
        public Task<JsonNode?> ExecuteAsync(Guid id, string tool, string fingerprint, Func<Task<JsonNode?>> operation, CancellationToken ct)
        { Calls++; return operation(); }
    }
    private static (AfterTabTools Tools, ApplicationProxy App, Writes Writes) Fixture(bool authenticated, bool write)
    {
        var app = DispatchProxy.Create<IAfterTabMcpApplication, ApplicationProxy>();
        var identity = new ClaimsIdentity(authenticated ? McpOptions.AuthenticationScheme : null);
        identity.AddClaim(new(McpOptions.ScopeClaim, McpOptions.ReadScope));
        if (write) identity.AddClaim(new(McpOptions.ScopeClaim, McpOptions.WriteScope));
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new(identity) } };
        var writes = new Writes();
        return (new(app, writes, http, NullLogger<AfterTabTools>.Instance,
            Options.Create(new McpOptions { PublicOrigin = "https://aftertab.test" })), (ApplicationProxy)(object)app, writes);
    }
    [TestMethod]
    public async Task ReadOnly_Token_Cannot_Reach_Write_Or_Application()
    {
        var f = Fixture(true, false);
        var rejected = await f.Tools.CreateEvent(Guid.NewGuid(), "Dinner");
        Assert.IsTrue(rejected.IsError);
        StringAssert.Contains(rejected.Meta!["mcp/www_authenticate"]!.ToJsonString(), "insufficient_scope");
        Assert.AreEqual(0, f.Writes.Calls);
        Assert.AreEqual(0, f.App.Calls.Count);
        await f.Tools.ListEvents();
        CollectionAssert.AreEqual(new[] { "ListEventsAsync" }, f.App.Calls);
    }
    [TestMethod]
    public async Task Unauthenticated_Principal_And_Empty_Operation_Cannot_Reach_Data()
    {
        var f = Fixture(false, true);
        var rejected = await f.Tools.ListEvents();
        Assert.IsTrue(rejected.IsError);
        StringAssert.Contains(rejected.Meta!["mcp/www_authenticate"]!.ToJsonString(), "invalid_token");
        Assert.AreEqual(0, f.App.Calls.Count);
        f = Fixture(true, true);
        await Assert.ThrowsExactlyAsync<McpException>(() => f.Tools.CreateEvent(Guid.Empty, "Dinner"));
        Assert.AreEqual(0, f.Writes.Calls);
    }
    [TestMethod]
    public async Task Event_Write_Checks_Current_Access_Before_Idempotency_Store()
    {
        var f = Fixture(true, true);
        await f.Tools.AddVirtualParticipant(Guid.NewGuid(), Guid.NewGuid(), "Alex", default);
        CollectionAssert.AreEqual(new[] { "EnsureEventAccessAsync", "AddParticipantAsync" }, f.App.Calls);
        Assert.AreEqual(1, f.Writes.Calls);
    }
}
