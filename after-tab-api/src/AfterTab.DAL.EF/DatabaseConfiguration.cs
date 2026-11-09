using Microsoft.EntityFrameworkCore;

namespace AfterTab.DAL.EF;

public enum DatabaseProvider { SqlServer, PostgreSql }

public static class DatabaseConfiguration
{
    public const string PostgreSqlMigrationsAssembly = "AfterTab.DAL.Migrations.PostgreSql";

    public static DatabaseProvider ParseProvider(string? value)
        => value is null ? DatabaseProvider.SqlServer : value.Trim().ToUpperInvariant() switch
        {
            "SQLSERVER" => DatabaseProvider.SqlServer,
            "POSTGRESQL" => DatabaseProvider.PostgreSql,
            _ => throw new InvalidOperationException("Database:Provider must be SqlServer or PostgreSql.")
        };

    public static DbContextOptionsBuilder UseAfterTabDatabase(this DbContextOptionsBuilder options,
        string connectionString, DatabaseProvider provider)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing ConnectionStrings:AfterTab.");
        return provider switch
        {
            DatabaseProvider.SqlServer => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()),
            DatabaseProvider.PostgreSql => options.UseNpgsql(connectionString, pg => pg
                .MigrationsAssembly(PostgreSqlMigrationsAssembly)
                .EnableRetryOnFailure()),
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };
    }
}
