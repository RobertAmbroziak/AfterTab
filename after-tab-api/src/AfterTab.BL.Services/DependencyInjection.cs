using AfterTab.BL.Interfaces.Users;
using AfterTab.BL.Interfaces.Expenses;
using AfterTab.BL.Services.Users;
using AfterTab.BL.Services.Expenses;
using Microsoft.Extensions.DependencyInjection;
namespace AfterTab.BL.Services;
public static class DependencyInjection
{
    public static IServiceCollection AddAfterTabBusiness(this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ISessionService, SessionService>();
        services.AddScoped<IGuestBrowserService, GuestBrowserService>();
        services.AddScoped<AfterTab.BL.Interfaces.Events.IEventService, AfterTab.BL.Services.Events.EventService>();
        services.AddSingleton<ISplitCalculator, SplitCalculator>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<AfterTab.BL.Interfaces.Events.ISettlementService, AfterTab.BL.Services.Events.SettlementService>();
        return services;
    }
}
