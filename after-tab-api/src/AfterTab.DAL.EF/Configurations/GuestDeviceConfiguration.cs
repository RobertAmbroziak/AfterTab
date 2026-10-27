using AfterTab.DAL.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterTab.DAL.EF.Configurations;

public sealed class GuestDeviceConfiguration : IEntityTypeConfiguration<GuestDevice>
{
    public void Configure(EntityTypeBuilder<GuestDevice> b)
    {
        b.ConfigureAudit("GuestDevices");
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
