using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AfterTab.API.Contracts;
using AfterTab.API.Controllers;
using AfterTab.API.Security;
using AfterTab.BL.Services;
using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class PublicIdHttpTests
{
    [TestMethod]
    public async Task Guid_Contracts_RoundTrip_Through_Authentication_Events_Expenses_And_Settlements()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder();
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
        builder.Services.AddAuthentication(SessionAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, SessionAuthenticationHandler>(SessionAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<AfterTab.API.Errors.AppExceptionHandler>();
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreatedAsync();
        await app.StartAsync();
        using var client = app.GetTestClient();

        async Task<JsonNode> Send(HttpMethod method, string path, object? body = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            using var response = await client.SendAsync(request);
            var text = await response.Content.ReadAsStringAsync();
            Assert.IsTrue(response.IsSuccessStatusCode, $"{method} {path}: {response.StatusCode} {text}");
            return JsonNode.Parse(text)!;
        }
        static Guid Id(JsonNode? node) => Guid.Parse(node!.GetValue<string>());

        var guest = await Send(HttpMethod.Post, "/api/auth/guest");
        var userId = Id(guest["user"]!["id"]);
        Assert.AreNotEqual(Guid.Empty, userId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guest["token"]!.GetValue<string>());
        var profile = await Send(HttpMethod.Put, "/api/profile/display-name", new { displayName = "Owner" });
        Assert.AreEqual(userId, Id(profile["id"])); // Typed ActionResult<UserDto> still serializes the public contract.
        var me = await Send(HttpMethod.Get, "/api/auth/me");
        Assert.AreNotEqual(Guid.Empty, Id(me["sessionId"]));
        Assert.AreEqual(userId, Id(me["user"]!["id"]));

        using var createResponse = await client.PostAsJsonAsync("/api/events", new { name = "Dinner" });
        Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
        var created = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!;
        var eventId = Id(created["details"]!["event"]!["id"]);
        StringAssert.Contains(createResponse.Headers.Location!.ToString(), eventId.ToString());
        var ownerId = Id(created["details"]!["participants"]![0]!["participantId"]);
        Assert.AreEqual(userId, Id(created["details"]!["participants"]![0]!["userId"]));
        var path = $"/api/events/{eventId}";
        await Send(HttpMethod.Get, path);
        var page = await Send(HttpMethod.Get, "/api/events?page=1");
        Assert.AreEqual(eventId, Id(page["items"]![0]!["id"]));
        var invite = await Send(HttpMethod.Post, "/api/invites/preview", new { token = created["inviteToken"]!.GetValue<string>() });
        Assert.AreEqual(eventId, Id(invite["eventId"]));

        var details = await Send(HttpMethod.Post, path + "/participants/virtual", new { displayName = "Friend" });
        var friendId = Id(details["participants"]!.AsArray().Single(x => x!["userId"] is null)!["participantId"]);
        var expense = await Send(HttpMethod.Post, path + "/expenses", new
        {
            name = "Pizza", amountMinor = 1200, splitMethod = "Amount", paidByParticipantId = ownerId,
            shares = new[] { new { participantId = ownerId, amountMinor = 200 }, new { participantId = friendId, amountMinor = 800 } }
        });
        var expenseId = Id(expense["id"]);
        Assert.AreEqual(userId, Id(expense["authorUserId"]));
        Assert.AreEqual(ownerId, Id(expense["paidByParticipantId"]));
        expense = await Send(HttpMethod.Put, path + $"/expenses/{expenseId}/balance",
            new { participantId = friendId, rowVersion = expense["rowVersion"]!.GetValue<string>() });
        Assert.AreEqual(friendId, Id(expense["balanceParticipantId"]));
        expense = await Send(HttpMethod.Put, path + $"/expenses/{expenseId}/split", new
        {
            splitMethod = "Amount", balanceParticipantId = friendId, rowVersion = expense["rowVersion"]!.GetValue<string>(),
            shares = new[] { new { participantId = ownerId, amountMinor = 200 }, new { participantId = friendId, amountMinor = 800 } }
        });
        expense = await Send(HttpMethod.Put, path + $"/expenses/{expenseId}/payer",
            new { paidByParticipantId = ownerId, rowVersion = expense["rowVersion"]!.GetValue<string>() });
        Assert.AreEqual(expenseId, Id(expense["id"]));
        Assert.AreEqual(ownerId, Id(expense["paidByParticipantId"]));
        var list = await Send(HttpMethod.Get, path + "/expenses");
        Assert.AreEqual(expenseId, Id(list[0]!["id"]));
        var settlement = await Send(HttpMethod.Get, path + "/settlement");
        Assert.AreEqual(friendId, Id(settlement["transfers"]![0]!["fromParticipantId"]));
        Assert.AreEqual(ownerId, Id(settlement["transfers"]![0]!["toParticipantId"]));
        settlement = await Send(HttpMethod.Post, path + "/close");
        settlement = await Send(HttpMethod.Put, path + "/settlement/confirmation", new
        {
            version = settlement["version"]!.GetValue<int>(), fromParticipantId = friendId, toParticipantId = ownerId,
            amountMinor = settlement["transfers"]![0]!["amountMinor"]!.GetValue<long>(), side = "Received", value = true
        });
        Assert.IsTrue(settlement["transfers"]![0]!["acknowledgement"]!["received"]!.GetValue<bool>());
        await Send(HttpMethod.Post, path + "/reopen");
        await Send(HttpMethod.Put, path + $"/participants/{friendId}", new { displayName = "Renamed friend" });

        // GUID resolution must not bypass the existing membership check.
        client.DefaultRequestHeaders.Authorization = null;
        var stranger = await Send(HttpMethod.Post, "/api/auth/guest");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", stranger["token"]!.GetValue<string>());
        using var forbidden = await client.GetAsync(path);
        Assert.AreEqual(HttpStatusCode.NotFound, forbidden.StatusCode);
        using var numeric = await client.GetAsync("/api/events/1");
        Assert.AreEqual(HttpStatusCode.NotFound, numeric.StatusCode);
        await Send(HttpMethod.Put, "/api/profile/display-name", new { displayName = "Joined friend" });
        var joined = await Send(HttpMethod.Post, "/api/invites/join", new { token = created["inviteToken"]!.GetValue<string>() });
        var strangerId = Id(stranger["user"]!["id"]);
        var targetId = Id(joined["participants"]!.AsArray().Single(x => x!["userId"] is not null && Id(x["userId"]) == strangerId)!["participantId"]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guest["token"]!.GetValue<string>());
        var linked = await Send(HttpMethod.Post, path + $"/participants/{friendId}/link", new { targetParticipantId = targetId });
        var linkedMember = linked["participants"]!.AsArray().Single(x => Id(x!["participantId"]) == friendId)!;
        Assert.AreEqual(strangerId, Id(linkedMember["userId"]));

        using var check = app.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.Events.SingleAsync(x => x.PublicId == eventId);
        Assert.IsTrue(entity.Id > 0);
        Assert.AreEqual(entity.Id, (await db.Expenses.SingleAsync()).EventId);
        Assert.AreEqual(expenseId, (await db.Expenses.SingleAsync()).PublicId);
    }
}
