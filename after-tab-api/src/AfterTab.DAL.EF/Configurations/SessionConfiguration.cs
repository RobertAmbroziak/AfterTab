using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AfterTab.DAL.EF.Configurations;
public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> b)
    {
        b.ConfigureAudit("Sessions");
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.DeviceLabel).HasMaxLength(200);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<GuestDevice>().WithMany().HasForeignKey(x => x.GuestDeviceId).OnDelete(DeleteBehavior.NoAction);
    }
}
