using AfterTab.DAL.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace AfterTab.Tests.Integration;

// SQLite has no sequences. Give its tests the same pre-save key availability as production HiLo.
public class NumericKeyModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder builder, DbContext context)
    {
        base.Customize(builder, context);
        foreach (var entity in builder.Model.GetEntityTypes())
            if (typeof(BaseEntity).IsAssignableFrom(entity.ClrType))
                builder.Entity(entity.ClrType).Property(nameof(BaseEntity.Id)).HasValueGenerator<TestKeyGenerator>();
    }

    private sealed class TestKeyGenerator : ValueGenerator<long>
    {
        private static long next;
        public override bool GeneratesTemporaryValues => false;
        public override long Next(EntityEntry entry) => Interlocked.Increment(ref next);
    }
}
