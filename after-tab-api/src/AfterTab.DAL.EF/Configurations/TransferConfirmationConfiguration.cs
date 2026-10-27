using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AfterTab.DAL.EF.Configurations;
public sealed class TransferConfirmationConfiguration : IEntityTypeConfiguration<TransferConfirmation>
{
    public void Configure(EntityTypeBuilder<TransferConfirmation> b)
    {
        b.ConfigureAudit("TransferConfirmations");
        b.HasIndex(x => new { x.EventId, x.SettlementVersion, x.FromUserId, x.ToUserId }).IsUnique();
        b.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.FromUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ToUserId).OnDelete(DeleteBehavior.NoAction);
        b.ToTable("TransferConfirmations", t => t.HasCheckConstraint("CK_TransferConfirmations_Valid", "[AmountMinor] > 0 AND [SettlementVersion] > 0 AND [FromUserId] <> [ToUserId]"));
    }
}
