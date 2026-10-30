using AfterTab.Common.Security;
using AfterTab.DAL.Entities;
using AfterTab.DAL.Entities.Users;
using AfterTab.DAL.Entities.Events;
using AfterTab.DAL.Entities.Expenses;
using AfterTab.DAL.EF.Configurations;
using AfterTab.DAL.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace AfterTab.DAL.EF;
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AccountAction> AccountActions => Set<AccountAction>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<GuestDevice> GuestDevices => Set<GuestDevice>();
    public DbSet<GuestLoginRequest> GuestLoginRequests => Set<GuestLoginRequest>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseSplit> ExpenseSplits => Set<ExpenseSplit>();
    public DbSet<EventInvite> EventInvites => Set<EventInvite>();
    public DbSet<TransferConfirmation> TransferConfirmations => Set<TransferConfirmation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    private void ApplyAudit()
    {
        ChangeTracker.DetectChanges();
        var now = timeProvider.GetUtcNow();
        var actor = currentUser.UserId;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.CreatedByUserId = actor;
                entry.Entity.ModifiedAtUtc = null;
                entry.Entity.ModifiedByUserId = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(x => x.CreatedAtUtc).IsModified = false;
                entry.Property(x => x.CreatedByUserId).IsModified = false;
                entry.Entity.ModifiedAtUtc = now;
                entry.Entity.ModifiedByUserId = actor;
                entry.Property(x => x.ModifiedAtUtc).IsModified = true;
                entry.Property(x => x.ModifiedByUserId).IsModified = true;
            }
        }
    }
    public override int SaveChanges() => SaveChanges(true);
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAudit();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => SaveChangesAsync(true, cancellationToken);
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAudit();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
