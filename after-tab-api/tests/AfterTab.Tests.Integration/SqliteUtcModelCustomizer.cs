using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AfterTab.Tests.Integration;

// SQLite does not support sorting DateTimeOffset; production SQL Server does.
// UTC ticks preserve SQL ordering semantics in relational integration tests.
public sealed class SqliteUtcModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);
        var converter = new ValueConverter<DateTimeOffset, long>(x => x.UtcTicks, x => new DateTimeOffset(x, TimeSpan.Zero));
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetProperties()))
            if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?)) property.SetValueConverter(converter);
    }
}
