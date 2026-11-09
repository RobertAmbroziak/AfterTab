using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AfterTab.API.Contracts;
using AfterTab.API.Controllers;
using AfterTab.API.Mcp;
using AfterTab.API.Security;
using AfterTab.BL.Services;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using AfterTab.DAL.Entities.Users;
using AfterTab.Mcp;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class McpHttpTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public required WebApplication App { get; init; }
        public required SqliteConnection Connection { get; init; }
        public required HttpClient Client { get; init; }
        public string Session { get; private set; } = "";
        public string? Integration { get; set; }
        public static async Task<Fixture> Create(bool enabled = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders().AddConsole().SetMinimumLevel(LogLevel.Warning);
            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("guest", limiter =>
            { limiter.PermitLimit = 100; limiter.Window = TimeSpan.FromMinutes(1); }));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            { ["Mcp:Enabled"] = enabled.ToString(), ["Authentication:FrontendOrigin"] = "https://aftertab.test" });
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
            builder.Services.AddAfterTabData("unused");
            builder.Services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            builder.Services.RemoveAll<DbContextOptions<AppDbContext>>();
            builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection)
                .ReplaceService<IModelCustomizer, ExpenseTests.SqliteModel>());
            builder.Services.AddAfterTabBusiness();
            builder.Services.AddScoped<PublicIds>();
            builder.Services.AddScoped<PublicIdResultFilter>();
            builder.Services.AddControllers(options => options.Filters.AddService<PublicIdResultFilter>())
                .AddApplicationPart(typeof(EventsController).Assembly);
            var auth = builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
            builder.Services.AddAfterTabMcpHost(builder.Configuration, auth);
            builder.Services.AddAuthorization();
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<AfterTab.API.Errors.AppExceptionHandler>();
            var app = builder.Build();
            app.UseExceptionHandler();
            if (enabled) app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapAfterTabMcp(builder.Configuration);
            using (var scope = app.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
            await app.StartAsync();
            return new Fixture { App = app, Connection = connection, Client = app.GetTestClient() };
        }
        public async Task Login(string name = "Owner")
        {
            Client.DefaultRequestHeaders.Authorization = null;
            var guest = await Api(HttpMethod.Post, "/api/auth/guest");
            Session = guest!["token"]!.GetValue<string>();
            Client.DefaultRequestHeaders.Authorization = new("Bearer", Session);
            await Api(HttpMethod.Put, "/api/profile/display-name", new { displayName = name });
        }
        public async Task<JsonNode> Issue(bool write = true)
        {
            var result = await Api(HttpMethod.Post, "/api/profile/mcp-tokens", new { name = "Local Codex", canWrite = write, expiresInDays = 30 });
            Integration = result!["token"]!.GetValue<string>();
            return result;
        }
        public async Task<JsonNode?> Api(HttpMethod method, string path, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await Client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode, $"{method} {path}: {response.StatusCode} {text}");
            return text.Length == 0 ? null : JsonNode.Parse(text);
        }
        public async Task<JsonNode> Rpc(string method, object args, bool authenticated = true, string? bearer = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp");
            // Override the ordinary PWA session header on this request only.
            request.Headers.Authorization = authenticated ? new("Bearer", Integration) : new("Bearer", bearer ?? "invalid");
            request.Headers.Accept.Add(new("application/json"));
            request.Headers.Accept.Add(new("text/event-stream"));
            request.Headers.Add("MCP-Protocol-Version", "2025-06-18");
            request.Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = args });
            using var response = await Client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode, $"{method}: {response.StatusCode} {text}");
            var json = text.StartsWith("{") ? text : text.Split('\n').Last(x => x.StartsWith("data: "))[6..];
            var result = JsonNode.Parse(json)!;
            Assert.IsNull(result["error"], result.ToJsonString());
            return result["result"]!;
        }
        public async Task<JsonNode> Tool(string name, object arguments, bool error = false, bool authenticated = true, string? bearer = null)
        {
            var result = await Rpc("tools/call", new { name, arguments }, authenticated, bearer);
            Assert.AreEqual(error, result["isError"]?.GetValue<bool>() ?? false, result.ToJsonString());
            return error ? result : JsonNode.Parse(result["content"]![0]!["text"]!.GetValue<string>())!;
        }
        public async ValueTask DisposeAsync() { Client.Dispose(); await App.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    [TestMethod]
    public async Task Protocol_Lists_Tools_And_Writes_Expense_Once_With_Public_Ids()
    {
        await using var f = await Fixture.Create();
        await f.Login();
        await f.Issue();
        var initialized = await f.Rpc("initialize", new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "AfterTab tests", version = "1" } });
        Assert.AreEqual("AfterTab", initialized["serverInfo"]!["name"]!.GetValue<string>());
        StringAssert.Contains(initialized["instructions"]!.GetValue<string>(), "isOwner=true");
        StringAssert.Contains(initialized["instructions"]!.GetValue<string>(), "Expenses can include real and virtual event participants.");
        var tools = (await f.Rpc("tools/list", new { }))["tools"]!.AsArray();
        Assert.AreEqual(18, tools.Count);
        Assert.IsNotNull(tools.Single(x => x!["name"]!.GetValue<string>() == "get_current_user")!["_meta"]?["securitySchemes"]);
        Assert.IsNotNull(tools.Single(x => x!["name"]!.GetValue<string>() == "create_event")!["_meta"]?["securitySchemes"]);
        Assert.AreEqual(0, tools.Count(x => x!["annotations"]?["destructiveHint"]?.GetValue<bool>() == true));
        Assert.IsTrue(tools.Single(x => x!["name"]!.GetValue<string>() == "get_event")!["annotations"]!["readOnlyHint"]!.GetValue<bool>());
        var me = await f.Tool("get_current_user", new { });
        var currentUserId = Guid.Parse(me["id"]!.GetValue<string>());
        Assert.AreNotEqual(Guid.Empty, currentUserId);
        Assert.AreEqual(string.Empty, (await f.Tool("get_user_instructions", new { }))["instructions"]!.GetValue<string>());

        var createId = Guid.NewGuid();
        var created = await f.Tool("create_event", new { operationId = createId, name = "Dinner" });
        Assert.IsFalse(created.ToJsonString().Contains("inviteToken", StringComparison.OrdinalIgnoreCase));
        var retry = await f.Tool("create_event", new { operationId = createId, name = "Dinner" });
        Assert.AreEqual(created.ToJsonString(), retry.ToJsonString());
        await f.Tool("create_event", new { operationId = createId, name = "Different" }, error: true);
        var eventId = created["event"]!["id"]!.GetValue<string>();
        var owner = created["participants"]![0]!["participantId"]!.GetValue<string>();
        Assert.IsTrue(created["participants"]![0]!["isOwner"]!.GetValue<bool>());
        Assert.AreEqual(currentUserId, Guid.Parse(created["participants"]![0]!["userId"]!.GetValue<string>()));
        var members = await f.Tool("add_virtual_participant", new { operationId = Guid.NewGuid(), eventId, displayName = "Alex" });
        var friend = members["participants"]!.AsArray().Single(x => x!["userId"] is null)!["participantId"]!.GetValue<string>();
        var expense = new { name = "Pizza", amountMinor = 10000, splitMethod = "Percentage", balanceParticipantId = friend,
            shares = new[] { new { participantId = owner, percentageBps = 3300 }, new { participantId = friend, percentageBps = 3300 } } };
        var preview = await f.Tool("preview_expense", new { eventId, expense });
        Assert.AreEqual(0L, preview["othersAmountMinor"]!.GetValue<long>());
        Assert.AreEqual(3400L, preview["balanceAmountMinor"]!.GetValue<long>());
        var badParticipantId = await f.Tool("preview_expense", new
        {
            eventId,
            expense = new { name = "Invalid ID", amountMinor = 100, splitMethod = "Amount", shares = new[] { new { participantId = Guid.NewGuid(), amountMinor = 100 } } }
        }, error: true);
        StringAssert.Contains(badParticipantId["content"]![0]!["text"]!.GetValue<string>(), "Odczytaj get_event");
        var duplicateParticipantId = await f.Tool("preview_expense", new
        {
            eventId,
            expense = new { name = "Duplicate ID", amountMinor = 100, splitMethod = "Amount", shares = new[]
            {
                new { participantId = owner, amountMinor = 50 }, new { participantId = owner, amountMinor = 50 }
            } }
        }, error: true);
        StringAssert.Contains(duplicateParticipantId["content"]![0]!["text"]!.GetValue<string>(), "więcej niż raz");
        var operationId = Guid.NewGuid();
        var saved = await f.Tool("create_expense", new { operationId, eventId, expense });
        retry = await f.Tool("create_expense", new { operationId, eventId, expense });
        Assert.AreEqual(saved.ToJsonString(), retry.ToJsonString());
        Assert.AreEqual(1, (await f.Tool("list_expenses", new { eventId })).AsArray().Count);
        var expenseId = saved["id"]!.GetValue<string>();
        await f.Tool("set_expense_balance", new { operationId = Guid.NewGuid(), eventId, expenseId, participantId = owner, rowVersion = "AAAA" }, error: true);
        var settlement = await f.Tool("close_event", new { operationId = Guid.NewGuid(), eventId });
        Assert.IsTrue(settlement["isFinal"]!.GetValue<bool>());
        await f.Tool("create_expense", new { operationId = Guid.NewGuid(), eventId, expense }, error: true);
        await f.Tool("reopen_event", new { operationId = Guid.NewGuid(), eventId });
        using var scope = f.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(1, await db.Events.CountAsync());
        Assert.AreEqual(1, await db.Expenses.CountAsync());
        var hash = (await db.McpAccessTokens.SingleAsync()).TokenHash;
        Assert.AreEqual(64, hash.Length);
        Assert.AreNotEqual(f.Integration, hash);
    }

    [TestMethod]
    public async Task Personal_Mcp_Instructions_Are_Stored_Per_Account_And_Read_By_Its_Token()
    {
        await using var f = await Fixture.Create();
        await f.Login("Stefan");
        await f.Issue();
        const string instructions = "When I say me, I mean my AfterTab account.";
        var saved = await f.Api(HttpMethod.Put, "/api/profile/mcp-instructions", new { instructions });
        Assert.AreEqual(instructions, saved!["instructions"]!.GetValue<string>());
        var loaded = await f.Api(HttpMethod.Get, "/api/profile/mcp-instructions");
        Assert.AreEqual(instructions, loaded!["instructions"]!.GetValue<string>());
        Assert.AreEqual(instructions, (await f.Tool("get_user_instructions", new { }))["instructions"]!.GetValue<string>());

        await f.Login("Olek");
        await f.Issue();
        Assert.AreEqual(string.Empty, (await f.Tool("get_user_instructions", new { }))["instructions"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task ReadOnly_Revocation_Expiry_And_Separate_Schemes_Are_Enforced()
    {
        await using var f = await Fixture.Create();
        await f.Login();
        var issued = await f.Issue(write: false);
        await f.Tool("list_events", new { });
        await f.Tool("create_event", new { operationId = Guid.NewGuid(), name = "Blocked" }, error: true);
        var list = await f.Api(HttpMethod.Get, "/api/profile/mcp-tokens");
        Assert.IsFalse(list!.ToJsonString().Contains("atm_"));
        Assert.IsFalse(list.ToJsonString().Contains("tokenHash"));
        using (var request = new HttpRequestMessage(HttpMethod.Get, "/api/events"))
        {
            request.Headers.Authorization = new("Bearer", f.Integration);
            using var response = await f.Client.SendAsync(request);
            Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        var sessionBlocked = await f.Tool("get_current_user", new { }, error: true, authenticated: false, bearer: f.Session);
        StringAssert.Contains(sessionBlocked["_meta"]!["mcp/www_authenticate"]!.ToJsonString(), "invalid_token");
        await f.Api(HttpMethod.Delete, "/api/profile/mcp-tokens/" + issued["details"]!["id"]!.GetValue<string>());
        await AssertOAuthChallenge(f);
        await f.Issue();
        using (var scope = f.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var token = await db.McpAccessTokens.SingleAsync(x => x.RevokedAtUtc == null);
            token.ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }
        await AssertOAuthChallenge(f);
    }

    [TestMethod]
    public async Task Stranger_Cannot_Read_Preview_Write_Or_Revoke_Another_Users_Token()
    {
        await using var f = await Fixture.Create();
        await f.Login();
        var issued = await f.Issue();
        var created = await f.Tool("create_event", new { operationId = Guid.NewGuid(), name = "Private" });
        var eventId = created["event"]!["id"]!.GetValue<string>();
        await f.Login("Stranger");
        await f.Issue();
        foreach (var tool in new[] { "get_event", "list_expenses", "get_settlement" })
            await f.Tool(tool, new { eventId }, error: true);
        await f.Tool("add_virtual_participant", new { operationId = Guid.NewGuid(), eventId, displayName = "Intruder" }, error: true);
        await f.Tool("preview_expense", new { eventId, expense = new { name = "Intruder", amountMinor = 100, splitMethod = "Amount", shares = new object[0] } }, error: true);
        using var response = await f.Client.DeleteAsync("/api/profile/mcp-tokens/" + issued["details"]!["id"]!.GetValue<string>());
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Incomplete_Operation_Is_Not_Run_Again_After_Unknown_Outcome()
    {
        await using var f = await Fixture.Create();
        await f.Login();
        await f.Issue();
        var operationId = Guid.NewGuid();
        // A rejected write still reserves its ID; an uncertain retry cannot create a second write.
        await f.Tool("create_event", new { operationId, name = "" }, error: true);
        await f.Tool("create_event", new { operationId, name = "" }, error: true);
        using var scope = f.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.AreEqual(1, await db.McpOperations.CountAsync());
        Assert.AreEqual(0, await db.Events.CountAsync());
        Assert.IsFalse((await db.McpOperations.SingleAsync()).Completed);
    }

    [TestMethod]
    public async Task OAuth_Uses_Dynamic_Client_Registration_PKCE_Consent_And_Rotating_Refresh_Tokens()
    {
        await using var f = await Fixture.Create();
        var resourceMetadata = await f.Client.GetFromJsonAsync<JsonNode>("/.well-known/oauth-protected-resource");
        Assert.AreEqual("http://localhost/mcp", resourceMetadata!["resource"]!.GetValue<string>());
        CollectionAssert.Contains(resourceMetadata["authorization_servers"]!.AsArray()
            .Select(x => x!.GetValue<string>()).ToArray(), "http://localhost");
        var authorizationMetadata = await f.Client.GetFromJsonAsync<JsonNode>("/.well-known/oauth-authorization-server");
        CollectionAssert.Contains(authorizationMetadata!["code_challenge_methods_supported"]!.AsArray()
            .Select(x => x!.GetValue<string>()).ToArray(), "S256");
        CollectionAssert.Contains(authorizationMetadata["token_endpoint_auth_methods_supported"]!.AsArray()
            .Select(x => x!.GetValue<string>()).ToArray(), "none");
        using var registrationResponse = await f.Client.PostAsJsonAsync("/oauth/register", new
        {
            client_name = "ChatGPT test",
            redirect_uris = new[] { "https://chatgpt.com/connector/oauth/test" },
            token_endpoint_auth_method = "none",
            grant_types = new[] { "authorization_code", "refresh_token" },
            response_types = new[] { "code" }
        });
        Assert.AreEqual(HttpStatusCode.Created, registrationResponse.StatusCode,
            await registrationResponse.Content.ReadAsStringAsync());
        var client = JsonNode.Parse(await registrationResponse.Content.ReadAsStringAsync())!;
        var clientId = client["client_id"]!.GetValue<string>();
        Assert.AreEqual("none", client["token_endpoint_auth_method"]!.GetValue<string>());

        var verifier = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var challenge = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        var state = Guid.NewGuid().ToString("N");
        var authorizeUrl = "/oauth/authorize?" + string.Join("&", new Dictionary<string, string>
        {
            ["response_type"] = "code", ["client_id"] = clientId,
            ["redirect_uri"] = "https://chatgpt.com/connector/oauth/test", ["state"] = state,
            ["code_challenge"] = challenge, ["code_challenge_method"] = "S256",
            ["scope"] = "aftertab.read offline_access", ["resource"] = "http://localhost/mcp"
        }.Select(x => Uri.EscapeDataString(x.Key) + "=" + Uri.EscapeDataString(x.Value)));
        using var authorizeResponse = await f.Client.GetAsync(authorizeUrl);
        Assert.AreEqual(HttpStatusCode.Redirect, authorizeResponse.StatusCode);
        var consentHash = authorizeResponse.Headers.Location!.Fragment;
        Assert.IsTrue(consentHash.StartsWith("#mcp-oauth=", StringComparison.Ordinal));
        var requestId = Guid.Parse(consentHash["#mcp-oauth=".Length..]);
        var info = await f.Client.GetFromJsonAsync<JsonNode>($"/api/mcp/oauth/requests/{requestId}");
        Assert.AreEqual("ChatGPT test", info!["clientName"]!.GetValue<string>());

        await f.Login("Stefan");
        var approved = await f.Api(HttpMethod.Post, "/api/mcp/oauth/consent", new { requestId, approve = true });
        var callback = new Uri(approved!["redirectUrl"]!.GetValue<string>());
        var code = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(callback.Query)["code"].ToString();
        var returnedState = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(callback.Query)["state"].ToString();
        Assert.AreEqual(state, returnedState);

        using var exchangeResponse = await f.Client.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["client_id"] = clientId,
            ["code"] = code, ["redirect_uri"] = "https://chatgpt.com/connector/oauth/test",
            ["code_verifier"] = verifier, ["resource"] = "http://localhost/mcp"
        }));
        Assert.AreEqual(HttpStatusCode.OK, exchangeResponse.StatusCode, await exchangeResponse.Content.ReadAsStringAsync());
        var issued = JsonNode.Parse(await exchangeResponse.Content.ReadAsStringAsync())!;
        var accessToken = issued["access_token"]!.GetValue<string>();
        var refreshToken = issued["refresh_token"]!.GetValue<string>();
        f.Integration = accessToken;
        Assert.AreEqual("Stefan", (await f.Tool("get_current_user", new { }))!["displayName"]!.GetValue<string>());
        await f.Tool("create_event", new { operationId = Guid.NewGuid(), name = "Read-only blocked" }, error: true);

        using var refreshResponse = await f.Client.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = clientId,
            ["refresh_token"] = refreshToken, ["resource"] = "http://localhost/mcp"
        }));
        Assert.AreEqual(HttpStatusCode.OK, refreshResponse.StatusCode, await refreshResponse.Content.ReadAsStringAsync());
        var refreshed = JsonNode.Parse(await refreshResponse.Content.ReadAsStringAsync())!;
        Assert.AreNotEqual(refreshToken, refreshed["refresh_token"]!.GetValue<string>());
        var connected = await f.Client.GetFromJsonAsync<JsonNode>("/api/mcp/oauth/clients");
        Assert.AreEqual(1, connected!.AsArray().Count);
        using var replay = await f.Client.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token", ["client_id"] = clientId,
            ["refresh_token"] = refreshToken, ["resource"] = "http://localhost/mcp"
        }));
        Assert.AreEqual(HttpStatusCode.BadRequest, replay.StatusCode);
        connected = await f.Client.GetFromJsonAsync<JsonNode>("/api/mcp/oauth/clients");
        Assert.AreEqual(0, connected!.AsArray().Count, "Reusing a rotated refresh token must revoke the token family.");
        using var disconnect = await f.Client.DeleteAsync("/api/mcp/oauth/clients/" + Uri.EscapeDataString(clientId));
        Assert.AreEqual(HttpStatusCode.NoContent, disconnect.StatusCode);
        f.Integration = refreshed["access_token"]!.GetValue<string>();
        await AssertOAuthChallenge(f);
    }

    [TestMethod]
    public async Task Disabled_Integration_Exposes_No_Mcp_Endpoint_Or_Token_Creation()
    {
        await using var f = await Fixture.Create(enabled: false);
        await f.Login();
        using var mcp = await f.Client.PostAsJsonAsync("/mcp", new { });
        using var tokens = await f.Client.PostAsJsonAsync("/api/profile/mcp-tokens", new { name = "Disabled" });
        Assert.AreEqual(HttpStatusCode.NotFound, mcp.StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, tokens.StatusCode);
    }

    private static async Task AssertOAuthChallenge(Fixture f)
    {
        var result = await f.Tool("get_current_user", new { }, error: true, authenticated: false, bearer: f.Integration);
        var challenge = result["_meta"]!["mcp/www_authenticate"]!.ToJsonString();
        StringAssert.Contains(challenge, "invalid_token");
        StringAssert.Contains(challenge, "oauth-protected-resource");
    }
}
