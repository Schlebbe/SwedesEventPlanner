using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SwedesEventPlanner.Application.Clock;

namespace SwedesEventPlanner.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
