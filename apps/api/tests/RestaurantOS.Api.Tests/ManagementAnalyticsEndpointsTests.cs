using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class ManagementAnalyticsEndpointsTests
{
    private const string Password = "Local-Dev-Pass!42";

    [Fact]
    public async Task SummaryRequiresAnalyticsPermission()
    {
        await using var factory = CreateFactory(includeAnalytics: false);
        using var client = factory.CreateClient();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync("/api/v1/management/analytics/summary");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SummaryReturnsCompletedOrderMetrics()
    {
        await using var factory = CreateFactory(includeAnalytics: true);
        using var client = factory.CreateClient();
        await SeedCompletedOrderAsync(factory);
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync("/api/v1/management/analytics/summary");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<SummaryDto>();
        Assert.NotNull(json);
        Assert.Equal(1, json!.CompletedOrderCount);
        Assert.Equal(2_500, json.GrossSalesMinor);
    }

    [Fact]
    public async Task CheckoutConvertsToPaidPro()
    {
        await using var factory = CreateFactory(includeAnalytics: true);
        using var client = factory.CreateClient();
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.PostAsJsonAsync(
            "/api/v1/management/subscription/checkout",
            new { planCode = "Pro" });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<UsageDto>();
        Assert.NotNull(json);
        Assert.Equal("Pro", json!.PlanCode);
        Assert.False(json.IsTrial);
    }

    [Fact]
    public async Task SummaryHidesFinancialMetricsForStaffRole()
    {
        await using var factory = CreateFactory(includeAnalytics: true, includeFinancials: false);
        using var client = factory.CreateClient();
        await SeedCompletedOrderAsync(factory);
        var token = await LoginAsync(client);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        var response = await client.GetAsync("/api/v1/management/analytics/summary");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<SummaryDto>();
        Assert.NotNull(json);
        Assert.False(json!.CanViewFinancials);
        Assert.Null(json.EstimatedCostMinor);
        Assert.Null(json.GrossProfitMinor);
        Assert.Equal(2_500, json.GrossSalesMinor);
    }

    private static WebApplicationFactory<Program> CreateFactory(bool includeAnalytics, bool includeFinancials = true)
    {
        var databaseName = Guid.NewGuid().ToString("N");
        return new AnalyticsApiFactory(databaseName, includeAnalytics, includeFinancials);
    }

    private static async Task SeedAsync(RestaurantOsDbContext db, bool includeAnalytics, bool includeFinancials = true)
    {
        await db.Database.EnsureCreatedAsync();
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var branchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var restaurantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var role = new ManagementRole(Guid.NewGuid(), "Owner");
        var user = new ManagementUser(
            Guid.NewGuid(),
            "owner@example.test",
            "OWNER@EXAMPLE.TEST",
            "pending",
            DateTimeOffset.UtcNow);
        user.UpdatePasswordHash(
            new PasswordHasher<ManagementUser>().HashPassword(user, Password));
        db.AddRange(
            new Tenant(tenantId, "Tenant"),
            new Restaurant(restaurantId, tenantId, "Restaurant"),
            new Branch(branchId, tenantId, restaurantId, "Branch"),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.SubscriptionManage),
            user,
            new ManagementMembership(Guid.NewGuid(), user.Id, tenantId, branchId, role.Id),
            TenantSubscription.CreateProTrial(tenantId, DateTimeOffset.UtcNow));
        if (includeAnalytics)
        {
            db.ManagementRolePermissions.Add(
                new ManagementRolePermissionGrant(role.Id, ManagementPermissions.AnalyticsView));
        }

        if (includeFinancials)
        {
            db.ManagementRolePermissions.Add(
                new ManagementRolePermissionGrant(role.Id, ManagementPermissions.AnalyticsFinancialView));
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedCompletedOrderAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var branchId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var now = DateTimeOffset.UtcNow;
        var order = new CustomerOrder(
            Guid.NewGuid(),
            tenantId,
            branchId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            "idem-1",
            new string('a', 64),
            "#A-1",
            new Money(2_500, "TRY"),
            now.AddMinutes(-30),
            now.AddMinutes(-10));
        order.ChangeStatus(OrderStatus.Accepted, now.AddMinutes(-25));
        order.ChangeStatus(OrderStatus.Preparing, now.AddMinutes(-20));
        order.ChangeStatus(OrderStatus.Ready, now.AddMinutes(-12));
        order.ChangeStatus(OrderStatus.Completed, now.AddMinutes(-5));
        db.CustomerOrders.Add(order);
        await db.SaveChangesAsync();
    }

    private static async Task<ManagementAccessTokenResponse> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest(
                "owner@example.test",
                Password,
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Guid.Parse("33333333-3333-3333-3333-333333333333")));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>())!;
    }

    private sealed record SummaryDto(
        long GrossSalesMinor,
        long? EstimatedCostMinor,
        long? GrossProfitMinor,
        long CancelledSalesMinor,
        int CompletedOrderCount,
        int CancelledOrderCount,
        int OpenServiceRequestCount,
        long AverageTicketMinor,
        string Currency,
        bool CanViewFinancials);

    private sealed record UsageDto(string PlanCode, bool IsTrial);

    private sealed class AnalyticsApiFactory(string databaseName, bool includeAnalytics, bool includeFinancials)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(
                "ManagementAuth:SigningKey",
                "test-only-signing-key-32-bytes-minimum-value");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<RestaurantOsDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<RestaurantOsDbContext>>();
                services.RemoveAll<RestaurantOsDbContext>();
                services.AddDbContext<RestaurantOsDbContext>(
                    options => options.UseInMemoryDatabase(databaseName));
            });
            builder.ConfigureServices(services =>
            {
                using var scope = services.BuildServiceProvider().CreateScope();
                SeedAsync(
                    scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>(),
                    includeAnalytics,
                    includeFinancials).GetAwaiter().GetResult();
            });
        }
    }
}
