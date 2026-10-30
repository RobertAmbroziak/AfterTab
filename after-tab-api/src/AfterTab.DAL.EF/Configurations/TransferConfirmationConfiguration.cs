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
        b.HasIndex(x => new { x.EventId, x.SettlementVersion, x.FromParticipantId, x.ToParticipantId }).IsUnique();
        b.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EventParticipant>().WithMany().HasForeignKey(x => new { x.FromParticipantId, x.EventId }).HasPrincipalKey(x => new { x.Id, x.EventId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EventParticipant>().WithMany().HasForeignKey(x => new { x.ToParticipantId, x.EventId }).HasPrincipalKey(x => new { x.Id, x.EventId }).OnDelete(DeleteBehavior.NoAction);
        b.ToTable("TransferConfirmations", t => t.HasCheckConstraint("CK_TransferConfirmations_Valid", "[AmountMinor] > 0 AND [SettlementVersion] > 0 AND [FromParticipantId] <> [ToParticipantId]"));
    }
}
