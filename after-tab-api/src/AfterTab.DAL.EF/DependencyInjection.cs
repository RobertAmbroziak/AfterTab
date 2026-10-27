using AfterTab.DAL.Interfaces;
using AfterTab.DAL.Interfaces.Users;
using AfterTab.DAL.EF.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace AfterTab.DAL.EF;
public static class DependencyInjection
{
    public static IServiceCollection AddAfterTabData(this IServiceCollection services, string connectionString)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.EnableRetryOnFailure()));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IGuestBrowserRepository, GuestBrowserRepository>();
        services.AddScoped<AfterTab.DAL.Interfaces.Events.IEventRepository, EventRepository>();
        services.AddScoped<AfterTab.DAL.Interfaces.Expenses.IExpenseRepository, ExpenseRepository>();
        services.AddScoped<AfterTab.DAL.Interfaces.Events.ISettlementRepository, SettlementRepository>();
        return services;
    }
}
