using System.Net;
using System.Net.Http.Headers;
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

public sealed class ManagementDashboardEndpointsTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task GetTodayDashboardReturnsOperationalMetrics()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var login = await LoginAsync(client);
        var access = await login.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.NotNull(access);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/management/dashboard/today");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
        var response = await client.SendAsync(request);
        var dashboard = await response.Content.ReadFromJsonAsync<ManagementTodayDashboardResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(dashboard);
        Assert.Equal(2, dashboard.TodaysOrderCount);
        Assert.Equal(1_500, dashboard.TodaysRevenueMinor);
        Assert.Equal(1, dashboard.OpenTablesCount);
        Assert.Equal(1, dashboard.PendingOrdersCount);
        Assert.Equal(1, dashboard.CompletedOrdersTodayCount);
    }

    [Fact]
    public async Task ActiveOrdersIncludeTableLabelAndAmount()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var login = await LoginAsync(client);
        var access = await login.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.NotNull(access);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/management/orders/active");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
        var response = await client.SendAsync(request);
        var orders = await response.Content.ReadFromJsonAsync<ManagementOrderResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(orders);
        var active = Assert.Single(orders);
        Assert.Equal("Masa 1", active.TableLabel);
        Assert.Equal(SeedIds.TableA, active.TableId);
        Assert.Equal(1_000, active.AmountMinor);
    }

    [Fact]
    public async Task ListTablesIncludesOperationalStatus()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var login = await LoginAsync(client);
        var access = await login.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.NotNull(access);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/management/tables");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
        var response = await client.SendAsync(request);
        var tables = await response.Content.ReadFromJsonAsync<ManagementTableResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(tables);
        var table = Assert.Single(tables);
        Assert.Equal("has_pending_order", table.OperationalStatus);
    }

    private static async Task<HttpResponseMessage> LoginAsync(HttpClient client) =>
        await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new
            {
                email = "owner@example.test",
                password = Password,
                tenantId = SeedIds.TenantA,
                branchId = SeedIds.BranchA,
            });

    private static async Task SeedAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = SeedIds.CreatedAtUtc;
        var role = new ManagementRole(SeedIds.Role, "RestaurantManager");
        var user = new ManagementUser(
            SeedIds.User,
            "owner@example.test",
            "OWNER@EXAMPLE.TEST",
            "pending",
            now);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ManagementUser>>();
        user.UpdatePasswordHash(hasher.HashPassword(user, Password));

        var activeOrder = CreateOrder(
            SeedIds.OrderActive,
            SeedIds.TenantA,
            SeedIds.BranchA,
            SeedIds.TableA,
            now,
            OrderStatus.Preparing,
            1_000);
        var completedToday = CreateOrder(
            SeedIds.OrderCompleted,
            SeedIds.TenantA,
            SeedIds.BranchA,
            SeedIds.TableA,
            now,
            OrderStatus.Completed,
            1_500);

        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.TableView),
            user,
            new ManagementMembership(Guid.NewGuid(), user.Id, SeedIds.TenantA, SeedIds.BranchA, role.Id),
            new DiningTable(SeedIds.TableA, SeedIds.TenantA, SeedIds.BranchA, "Masa 1"),
            activeOrder,
            completedToday);
        await db.SaveChangesAsync();
    }

    private static CustomerOrder CreateOrder(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        DateTimeOffset now,
        OrderStatus status,
        long totalMinor)
    {
        var order = new CustomerOrder(
            id,
            tenantId,
            branchId,
            tableId,
            Guid.NewGuid(),
            $"key-{id:N}",
            new string('A', 64),
            $"#{id.ToString("N")[..6]}",
            Money.Try(totalMinor),
            now,
            now.AddMinutes(18));
        if (status != OrderStatus.Submitted)
        {
            order.ChangeStatus(status, now.AddMinutes(5));
        }

        return order;
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new DashboardApiFactory();

    private sealed class DashboardApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"dashboard-tests-{Guid.NewGuid()}";

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
                    options => options.UseInMemoryDatabase(_databaseName));
            });
        }
    }

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("41000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("41000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("41000000-0000-0000-0000-000000000003");
        public static readonly Guid User = Guid.Parse("41000000-0000-0000-0000-000000000004");
        public static readonly Guid Role = Guid.Parse("41000000-0000-0000-0000-000000000005");
        public static readonly Guid TableA = Guid.Parse("41000000-0000-0000-0000-000000000010");
        public static readonly Guid OrderActive = Guid.Parse("41000000-0000-0000-0000-000000000011");
        public static readonly Guid OrderCompleted = Guid.Parse("41000000-0000-0000-0000-000000000012");
        public static readonly DateTimeOffset CreatedAtUtc =
            new(2026, 8, 31, 10, 0, 0, TimeSpan.Zero);
    }
}
