using AfterTab.Common.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace AfterTab.DAL.EF;
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__AfterTab")
            ?? @"Server=(localdb)\MSSQLLocalDB;Database=AfterTab;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection).Options;
        return new AppDbContext(options, new SystemActor(), TimeProvider.System);
    }
    private sealed class SystemActor : ICurrentUser { public Guid? UserId => null; }
}
