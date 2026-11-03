using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using AfterTab.API.Errors;
using AfterTab.Common.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AfterTab.Tests.Integration;

[TestClass]
public sealed class ErrorLoggingTests
{
    private sealed class CapturedLogs : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    [TestMethod]
    [DataRow(500)]
    [DataRow(400)]
    [DataRow(403)]
    [DataRow(404)]
    [DataRow(409)]
    public async Task Exception_Pipeline_Logs_Unexpected_Errors_Once_And_Preserves_Business_Responses(int status)
    {
        var directory = Path.Combine(Path.GetTempPath(), "aftertab-logging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Production" });
            builder.WebHost.UseTestServer();
            builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "Logging.appsettings.json"))
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Serilog:WriteTo:1:Args:path"] = Path.Combine(directory, "api-.log")
                });
            var captured = new CapturedLogs();
            using var logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration)
                .WriteTo.Sink(captured).CreateLogger();
            builder.Services.AddSerilog(logger);
            builder.Services.AddProblemDetails();
            builder.Services.AddExceptionHandler<AppExceptionHandler>();
            Exception failure = status switch
            {
                400 => new AppException(ErrorKind.Validation, "Invalid input"),
                403 => new AppException(ErrorKind.Forbidden, "Forbidden"),
                404 => new AppException(ErrorKind.NotFound, "Not found"),
                409 => new AppException(ErrorKind.Conflict, "Conflict"),
                _ => new InvalidOperationException("Internal failure details")
            };
            await using (var app = builder.Build())
            {
                app.UseExceptionHandler();
                app.MapGet("/test/{id}", (HttpContext _) => Task.FromException(failure));
                await app.StartAsync();
                using var client = app.GetTestClient();
                using var response = await client.GetAsync("/test/private-value?token=private-token");
                Assert.AreEqual((HttpStatusCode)status, response.StatusCode);
                Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                var body = await response.Content.ReadAsStringAsync();
                using var json = JsonDocument.Parse(body);
                var traceId = json.RootElement.GetProperty("traceId").GetString();
                Assert.IsFalse(string.IsNullOrEmpty(traceId));
                var errors = captured.Events.Where(e => e.Level >= LogEventLevel.Error).ToArray();
                if (status == 500)
                {
                    Assert.HasCount(1, errors);
                    Assert.AreSame(failure, errors[0].Exception);
                    Assert.IsFalse(string.IsNullOrEmpty(errors[0].Exception!.StackTrace));
                    Assert.AreEqual(traceId, ((ScalarValue)errors[0].Properties["TraceId"]).Value);
                    Assert.AreEqual("/test/{id}", ((ScalarValue)errors[0].Properties["Route"]).Value);
                    Assert.DoesNotContain("Internal failure details", body);
                    Assert.DoesNotContain("InvalidOperationException", body);
                }
                else
                {
                    Assert.HasCount(0, errors);
                    Assert.AreEqual(failure.Message, json.RootElement.GetProperty("title").GetString());
                }
            }
            logger.Dispose(); // Flush the file sink before inspecting its contents.
            var logs = string.Join("\n", Directory.GetFiles(directory, "*.log").Select(File.ReadAllText));
            Assert.DoesNotContain("private-value", logs);
            Assert.DoesNotContain("private-token", logs);
            if (status == 500)
            {
                Assert.Contains("Internal failure details", logs);
                Assert.Contains("InvalidOperationException", logs);
                Assert.Contains("TraceId=", logs);
            }
            else
            {
                Assert.DoesNotContain(failure.Message, logs);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
