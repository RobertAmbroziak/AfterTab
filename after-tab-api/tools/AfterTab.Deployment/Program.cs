using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder().AddUserSecrets<DeploymentActor>()
    .AddEnvironmentVariables().Build();
var provider = DatabaseConfiguration.ParseProvider(configuration["Database:Provider"]);
var standardConnection = configuration.GetConnectionString("AfterTab");
if (standardConnection is not null || provider == DatabaseProvider.PostgreSql || args.Contains("--migrate"))
{
    if (args.Contains("--bootstrap"))
        throw new InvalidOperationException("--bootstrap is only for the separate Azure SQL / Entra deployment mode. Use --migrate for provider-configured deployments.");
    var configuredOptions = new DbContextOptionsBuilder<AppDbContext>();
    configuredOptions.UseAfterTabDatabase(standardConnection
        ?? throw new InvalidOperationException("Missing ConnectionStrings:AfterTab."), provider);
    await using var configuredDb = new AppDbContext(configuredOptions.Options, new DeploymentActor(), TimeProvider.System);
    if (args.Contains("--migrate"))
    {
        await configuredDb.MigrateFreshSchemaAsync();
        Console.WriteLine($"Applied {provider} migrations.");
    }
    await Report(configuredDb);
    return;
}

// Legacy Azure / Entra mode remains restricted to AfterTabDb and token authentication.
var connectionString = Environment.GetEnvironmentVariable("AFTERTAB_SQL_CONNECTION_STRING")
    ?? configuration["Deployment:SqlConnectionString"];
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Missing deployment connection string. Set AFTERTAB_SQL_CONNECTION_STRING or the Deployment:SqlConnectionString user secret.");
var connectionSettings = new SqlConnectionStringBuilder(connectionString);
if (connectionSettings.InitialCatalog != "AfterTabDb")
    throw new InvalidOperationException("Deployment requires the AfterTabDb database.");
if (!string.IsNullOrEmpty(connectionSettings.UserID) || !string.IsNullOrEmpty(connectionSettings.Password)
    || connectionSettings.IntegratedSecurity || connectionSettings.Authentication != SqlAuthenticationMethod.NotSpecified)
    throw new InvalidOperationException("Deployment requires access-token authentication without connection-string credentials.");
connectionSettings.Encrypt = SqlConnectionEncryptOption.Mandatory;
connectionSettings.TrustServerCertificate = false;
var token = Environment.GetEnvironmentVariable("AFTERTAB_SQL_ACCESS_TOKEN")
    ?? throw new InvalidOperationException("Missing deployment SQL access token.");
await using var connection = new SqlConnection(connectionSettings.ConnectionString)
{
    AccessToken = token
};
for (var attempt = 0; ; attempt++)
{
    try { await connection.OpenAsync(); break; }
    catch (SqlException exception) when (attempt < 5 && exception.Number is 40613 or 40197 or 40501 or 10928 or 10929)
    {
        Console.WriteLine("Azure SQL is resuming or temporarily busy; retrying the deployment connection.");
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}
Console.WriteLine("Connected to Azure SQL AfterTabDb with Microsoft Entra authentication.");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connection, sql => sql.EnableRetryOnFailure()).Options;
await using var db = new AppDbContext(options, new DeploymentActor(), TimeProvider.System);
if (args.Contains("--bootstrap"))
{
    var principalId = Guid.Parse(Environment.GetEnvironmentVariable("AFTERTAB_API_PRINCIPAL_ID")
        ?? throw new InvalidOperationException("Missing API managed identity principal ID."));
    await db.MigrateFreshSchemaAsync();
    Console.WriteLine("Applied EF migrations to AfterTabDb.");
    await using var command = connection.CreateCommand();
    command.CommandText = $"""
        IF DB_NAME() <> N'AfterTabDb' THROW 50000, 'Unexpected database', 1;
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'aftertab-api-ambro')
            CREATE USER [aftertab-api-ambro] FROM EXTERNAL PROVIDER WITH OBJECT_ID = '{principalId:D}';
        GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [aftertab-api-ambro];
        """;
    await command.ExecuteNonQueryAsync();
    Console.WriteLine("Granted API managed identity data access in AfterTabDb (no schema migration permissions).");
}

await Report(db);

static async Task Report(AppDbContext context)
{
    var applied = await context.Database.GetAppliedMigrationsAsync();
    Console.WriteLine("Applied migrations: " + string.Join(", ", applied));
    Console.WriteLine($"Users: {await context.Users.CountAsync()}, Sessions: {await context.Sessions.CountAsync()}");
    Console.WriteLine($"Revoked sessions: {await context.Sessions.CountAsync(x => x.RevokedAtUtc != null)}");
    Console.WriteLine($"Events: {await context.Events.CountAsync()}, Expenses: {await context.Expenses.CountAsync()}");
}

internal sealed class DeploymentActor : ICurrentUser
{
    public long? UserId => null;
}
