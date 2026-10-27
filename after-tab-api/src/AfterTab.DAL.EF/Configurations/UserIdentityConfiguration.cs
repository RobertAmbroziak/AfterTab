using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AfterTab.DAL.EF.Configurations;
public sealed class UserIdentityConfiguration : IEntityTypeConfiguration<UserIdentity>
{
    public void Configure(EntityTypeBuilder<UserIdentity> b)
    {
        b.ConfigureAudit("UserIdentities");
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ProviderSubject).HasMaxLength(255);
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.NormalizedEmail).HasMaxLength(320);
        b.Property(x => x.PasswordHash).HasMaxLength(1024);
        b.HasIndex(x => new { x.Provider, x.ProviderSubject }).IsUnique()
            .HasFilter("[ProviderSubject] IS NOT NULL");
        b.HasIndex(x => x.NormalizedEmail).IsUnique()
            .HasFilter("[Provider] = 'Local' AND [NormalizedEmail] IS NOT NULL");
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        b.ToTable("UserIdentities", t => t.HasCheckConstraint("CK_UserIdentities_Login",
            "([Provider] = 'Local' AND [NormalizedEmail] IS NOT NULL) OR ([Provider] IN ('Google', 'Facebook') AND [ProviderSubject] IS NOT NULL AND [PasswordHash] IS NULL)"));
    }
}
