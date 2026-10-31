using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace AfterTab.DAL.EF.Configurations;
public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> b)
    {
        b.ConfigureAudit("Expenses");
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Note).HasMaxLength(2000);
        b.Property(x => x.SplitMethod).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasAlternateKey(x => new { x.Id, x.EventId });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EventParticipant>().WithMany()
            .HasForeignKey(x => new { x.PaidByParticipantId, x.EventId })
            .HasPrincipalKey(x => new { x.Id, x.EventId }).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<EventParticipant>().WithMany().HasForeignKey(x => new { x.BalanceParticipantId, x.EventId })
            .HasPrincipalKey(x => new { x.Id, x.EventId }).OnDelete(DeleteBehavior.NoAction);
        b.ToTable("Expenses", t => t.HasCheckConstraint("CK_Expenses_Balance", "[BalanceAmountMinor] >= 0 AND [BalanceAmountMinor] <= [AmountMinor] AND (([BalanceParticipantId] IS NULL AND [BalanceAmountMinor] = 0) OR ([BalanceParticipantId] IS NOT NULL AND [BalanceAmountMinor] > 0 AND [Balanced] = 1 AND [OthersAmountMinor] = 0))"));
        b.ToTable("Expenses", t => t.HasCheckConstraint("CK_Expenses_Amount", "[AmountMinor] > 0"));
        b.ToTable("Expenses", t => t.HasCheckConstraint("CK_Expenses_Others", "[OthersAmountMinor] >= 0 AND [OthersAmountMinor] <= [AmountMinor]"));
    }
}
