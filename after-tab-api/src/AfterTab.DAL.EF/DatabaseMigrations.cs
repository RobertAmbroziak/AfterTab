using Microsoft.EntityFrameworkCore;

namespace AfterTab.DAL.EF;

public static class DatabaseMigrations
{
    public static async Task MigrateFreshSchemaAsync(this AppDbContext db, CancellationToken ct = default)
    {
        var known = db.Database.GetMigrations().ToHashSet(StringComparer.Ordinal);
        var applied = await db.Database.GetAppliedMigrationsAsync(ct);
        if (applied.Any(x => !known.Contains(x)))
            throw new InvalidOperationException(
                "This release requires a new empty database (numeric keys). The existing migration history is incompatible. " +
                "Stop the application and explicitly recreate the database before applying migrations. No data has been removed.");
        await db.Database.MigrateAsync(ct);
    }
}
