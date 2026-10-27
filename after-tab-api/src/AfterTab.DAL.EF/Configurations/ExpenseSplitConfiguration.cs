using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AfterTab.DAL.EF.Configurations;
public sealed class ExpenseSplitConfiguration : IEntityTypeConfiguration<ExpenseSplit>
{
    public void Configure(EntityTypeBuilder<ExpenseSplit> b)
    {
        b.ConfigureAudit("ExpenseSplits");
        b.HasIndex(x => new { x.ExpenseId, x.ParticipantId }).IsUnique();
        b.HasOne<Expense>().WithMany(x => x.Splits)
            .HasForeignKey(x => new { x.ExpenseId, x.EventId })
            .HasPrincipalKey(x => new { x.Id, x.EventId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EventParticipant>().WithMany()
            .HasForeignKey(x => new { x.ParticipantId, x.EventId })
            .HasPrincipalKey(x => new { x.Id, x.EventId }).OnDelete(DeleteBehavior.NoAction);
        b.ToTable("ExpenseSplits", t =>
        {
            t.HasCheckConstraint("CK_ExpenseSplits_Amount", "[AmountMinor] >= 0");
            t.HasCheckConstraint("CK_ExpenseSplits_Percentage", "[PercentageBps] IS NULL OR ([PercentageBps] > 0 AND [PercentageBps] <= 10000)");
        });
    }
}
