using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AfterTab.DAL.EF.Configurations;
public sealed class EventInviteConfiguration : IEntityTypeConfiguration<EventInvite>
{
    public void Configure(EntityTypeBuilder<EventInvite> b)
    {
        b.ConfigureAudit("EventInvites");
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
    }
}
