using AfterTab.DAL.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AfterTab.DAL.EF.Configurations;
public sealed class AccountActionConfiguration : IEntityTypeConfiguration<AccountAction>
{
    public void Configure(EntityTypeBuilder<AccountAction> b)
    {
        b.ConfigureAudit("AccountActions");
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.Property(x => x.Purpose).HasMaxLength(30).IsRequired();
        b.Property(x => x.Challenge).HasMaxLength(64);
        b.Property(x => x.State).HasMaxLength(64);
        b.Property(x => x.ReturnOrigin).HasMaxLength(300);
        b.Property(x => x.BrowserHash).HasMaxLength(64);
        b.Property(x => x.Version).IsConcurrencyToken();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
