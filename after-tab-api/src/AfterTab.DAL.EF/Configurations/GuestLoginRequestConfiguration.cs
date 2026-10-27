using AfterTab.DAL.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterTab.DAL.EF.Configurations;

public sealed class GuestLoginRequestConfiguration : IEntityTypeConfiguration<GuestLoginRequest>
{
    public void Configure(EntityTypeBuilder<GuestLoginRequest> b)
    {
        b.ConfigureAudit("GuestLoginRequests");
        b.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        b.Property(x => x.CodeHash).HasMaxLength(64);
        b.Property(x => x.Challenge).HasMaxLength(64).IsRequired();
        b.Property(x => x.State).HasMaxLength(64).IsRequired();
        b.Property(x => x.ReturnOrigin).HasMaxLength(300).IsRequired();
        b.Property(x => x.Action).HasMaxLength(10).IsRequired();
        b.Property(x => x.ProposedDisplayName).HasMaxLength(100);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasIndex(x => x.RequestHash).IsUnique();
        b.HasIndex(x => x.CodeHash).IsUnique().HasFilter("[CodeHash] IS NOT NULL");
        b.HasIndex(x => x.ExpiresAtUtc);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.LegacyUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Session>().WithMany().HasForeignKey(x => x.LegacySessionId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<GuestDevice>().WithMany().HasForeignKey(x => x.GuestDeviceId).OnDelete(DeleteBehavior.NoAction);
    }
}
