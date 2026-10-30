using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AfterTab.DAL.EF.Configurations;
public sealed class EventParticipantConfiguration : IEntityTypeConfiguration<EventParticipant>
{
    public void Configure(EntityTypeBuilder<EventParticipant> b)
    {
        b.ConfigureAudit("EventParticipants");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.EventId, x.UserId }).IsUnique().HasFilter("[UserId] IS NOT NULL");
        b.HasOne<EventParticipant>().WithMany().HasForeignKey(x => new { x.MergedIntoParticipantId, x.EventId })
            .HasPrincipalKey(x => new { x.Id, x.EventId }).OnDelete(DeleteBehavior.NoAction);
        b.HasAlternateKey(x => new { x.Id, x.EventId });
        b.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
