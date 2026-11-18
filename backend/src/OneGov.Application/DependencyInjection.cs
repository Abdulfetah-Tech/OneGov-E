using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using OneGov.Application.Registry;

namespace OneGov.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<ListServicesQueryValidator>();
        services.AddScoped<ServiceDirectoryService>();
        return services;
    }
}
