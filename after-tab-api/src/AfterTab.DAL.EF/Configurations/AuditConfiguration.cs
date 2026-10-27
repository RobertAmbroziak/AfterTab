using AfterTab.DAL.Entities;
using AfterTab.DAL.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AfterTab.DAL.EF.Configurations;
internal static class AuditConfiguration
{
    public static void ConfigureAudit<T>(this EntityTypeBuilder<T> builder, string table)
        where T : BaseEntity
    {
        builder.ToTable(table);
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.ModifiedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
