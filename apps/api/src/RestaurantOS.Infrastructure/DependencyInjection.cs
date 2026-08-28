using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RestaurantOS.Application;

namespace RestaurantOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("RestaurantOs")
            ?? throw new InvalidOperationException(
                "The ConnectionStrings:RestaurantOs setting is required.");

        services.AddDbContext<RestaurantOsDbContext>(
            options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));
        services.AddScoped<ICustomerExperienceService, CustomerExperienceService>();
        services.AddScoped<IManagementOrderService, ManagementOrderService>();
        services.AddScoped<IManagementTableService, ManagementTableService>();
        services.AddScoped<IManagementMenuService, ManagementMenuService>();
        services.AddScoped<IFeatureEntitlementService, FeatureEntitlementService>();
        services.AddScoped<IManagementServiceRequestService, ManagementServiceRequestService>();

        return services;
    }
}
