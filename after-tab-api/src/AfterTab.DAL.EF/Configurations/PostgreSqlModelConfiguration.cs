using System.Text.RegularExpressions;
using AfterTab.DAL.Entities.Expenses;
using Microsoft.EntityFrameworkCore;

namespace AfterTab.DAL.EF.Configurations;

internal static partial class PostgreSqlModelConfiguration
{
    public static void Configure(ModelBuilder builder)
    {
        // The existing SQL Server schema/migration history stays unchanged. Translate only
        // the explicitly authored check/index predicates; ordinary LINQ SQL is provider-generated.
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            foreach (var index in entity.GetIndexes())
                if (index.GetFilter() is { } filter) index.SetFilter(ToPostgreSql(filter));
            foreach (var constraint in entity.GetCheckConstraints().ToArray())
            {
                var name = constraint.ModelName;
                var sql = ToPostgreSql(constraint.Sql);
                entity.RemoveCheckConstraint(name);
                entity.AddCheckConstraint(name, sql);
            }
        }
        builder.Entity<Expense>().Property(x => x.RowVersion)
            .IsConcurrencyToken().ValueGeneratedNever();
    }

    private static string ToPostgreSql(string sql)
        => Identifier().Replace(sql, "\"$1\"")
            .Replace("\"Balanced\" = 1", "\"Balanced\" = TRUE", StringComparison.Ordinal);

    [GeneratedRegex(@"\[([A-Za-z_][A-Za-z0-9_]*)\]")]
    private static partial Regex Identifier();
}
