using AfterTab.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
namespace AfterTab.DAL.EF;
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Support EF CLI from the repository, solution or API directory. Read the API's
        // provider configuration, then let deployment environment/CLI arguments override it.
        var directory = Directory.GetCurrentDirectory();
        var apiDirectory = new[]
        {
            Path.Combine(directory, "src", "AfterTab.API"),
            Path.Combine(directory, "after-tab-api", "src", "AfterTab.API"),
            directory
        }.FirstOrDefault(path => File.Exists(Path.Combine(path, "appsettings.json"))) ?? directory;
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(apiDirectory, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(apiDirectory, $"appsettings.{environment}.json"), optional: true)
            .AddEnvironmentVariables().AddCommandLine(args).Build();
        var provider = DatabaseConfiguration.ParseProvider(configuration["Database:Provider"]);
        var connection = configuration.GetConnectionString("AfterTab")
            ?? (provider == DatabaseProvider.SqlServer
                ? @"Server=(localdb)\MSSQLLocalDB;Database=AfterTab;Trusted_Connection=True;TrustServerCertificate=True"
                : throw new InvalidOperationException("Missing ConnectionStrings:AfterTab for PostgreSql."));
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseAfterTabDatabase(connection, provider);
        return new AppDbContext(options.Options, new SystemActor(), TimeProvider.System);
    }
    private sealed class SystemActor : ICurrentUser { public Guid? UserId => null; }
}
