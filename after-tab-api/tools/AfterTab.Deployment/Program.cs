using AfterTab.Common.Security;
using AfterTab.DAL.EF;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Deliberately scoped to the AfterTab Azure database. Never uses BudgetWalletDb.
var token = Environment.GetEnvironmentVariable("AFTERTAB_SQL_ACCESS_TOKEN")
    ?? throw new InvalidOperationException("Missing deployment SQL access token.");
await using var connection = new SqlConnection(
    "Server=tcp:ambrodevserver.database.windows.net,1433;Database=AfterTabDb;Encrypt=True;TrustServerCertificate=False;Connect Timeout=90;")
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
    await db.Database.MigrateAsync();
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

var applied = await db.Database.GetAppliedMigrationsAsync();
Console.WriteLine("Applied migrations: " + string.Join(", ", applied));
Console.WriteLine($"Users: {await db.Users.CountAsync()}, Sessions: {await db.Sessions.CountAsync()}");
Console.WriteLine($"Revoked sessions: {await db.Sessions.CountAsync(x => x.RevokedAtUtc != null)}");
Console.WriteLine($"Events: {await db.Events.CountAsync()}, Expenses: {await db.Expenses.CountAsync()}");

internal sealed class DeploymentActor : ICurrentUser
{
    public Guid? UserId => null;
}
