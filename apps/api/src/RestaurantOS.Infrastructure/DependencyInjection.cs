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
        services.AddMemoryCache();
        services.Configure<CustomerOrderRateLimitOptions>(
            configuration.GetSection(CustomerOrderRateLimitOptions.SectionName));
        services.AddSingleton<ICustomerOrderGuard, CustomerOrderGuard>();
        services.AddScoped<ICustomerExperienceService, CustomerExperienceService>();
        services.AddScoped<IManagementOrderService, ManagementOrderService>();
        services.AddScoped<IManagementTableService, ManagementTableService>();
        services.AddScoped<IManagementMenuService, ManagementMenuService>();
        services.AddScoped<IFeatureEntitlementService, FeatureEntitlementService>();
        services.AddScoped<IManagementServiceRequestService, ManagementServiceRequestService>();
        services.AddScoped<IManagementAnalyticsService, ManagementAnalyticsService>();
        services.AddScoped<IManagementDashboardService, ManagementDashboardService>();
        services.AddScoped<IManagementBranchService, ManagementBranchService>();
        services.AddScoped<IPromotionManagementService, PromotionManagementService>();
        services.AddScoped<INotificationManagementService, NotificationManagementService>();
        services.AddScoped<IPlatformSubscriptionOfferService, PlatformSubscriptionOfferService>();
        services.AddScoped<ICustomerMenuSettingsService, CustomerMenuSettingsService>();
        services.AddSingleton<IStockPhotoLibrary, StockPhotoLibrary>();

        return services;
    }

    public static IServiceCollection AddEmailSender(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        var provider = configuration.GetSection(EmailOptions.SectionName)["Provider"] ?? "Logging";
        if (string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        return services;
    }
}
