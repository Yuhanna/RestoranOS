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

public sealed class ManagementRolePermissionMatrixTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task SyncBuiltInRolesStripsObsoleteStaffGrants()
    {
        using var factory = CreateFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();

        var role = new ManagementRole(Guid.NewGuid(), "BranchStaff");
        db.ManagementRoles.Add(role);
        db.ManagementRolePermissions.AddRange(
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.TableEdit),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.AnalyticsView));
        await db.SaveChangesAsync();

        await ManagementRolePermissionSync.SyncBuiltInRolesAsync(db, CancellationToken.None);
        await db.SaveChangesAsync();

        var grants = await db.ManagementRolePermissions
            .Where(x => x.RoleId == role.Id)
            .Select(x => x.Permission)
            .OrderBy(x => x)
            .ToListAsync();

        Assert.Equal(
            ManagementAuthServicePermissions.BranchStaff.OrderBy(x => x),
            grants);
        Assert.DoesNotContain(ManagementPermissions.TableEdit, grants);
        Assert.DoesNotContain(ManagementPermissions.AnalyticsView, grants);
    }

    [Fact]
    public async Task StaffCannotAccessAnalyticsTablesAdminOrFinancialDashboard()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedStaffAsync(factory);

        var access = await LoginForAccessAsync(client, "staff@example.test");

        using var analytics = Authorized(access, HttpMethod.Get, "/api/v1/management/analytics/summary?fromUtc=2020-01-01T00:00:00Z&toUtc=2030-01-01T00:00:00Z");
        using var tablesCreate = Authorized(access, HttpMethod.Post, "/api/v1/management/tables");
        tablesCreate.Content = JsonContent.Create(new { label = "Yeni" });
        using var branches = Authorized(access, HttpMethod.Get, "/api/v1/management/branches");
        using var dashboard = Authorized(access, HttpMethod.Get, "/api/v1/management/dashboard/today");
        using var workspace = Authorized(access, HttpMethod.Get, "/api/v1/management/workspace");
        using var orders = Authorized(access, HttpMethod.Get, "/api/v1/management/orders/active");

        var analyticsResponse = await client.SendAsync(analytics);
        var tablesResponse = await client.SendAsync(tablesCreate);
        var branchesResponse = await client.SendAsync(branches);
        var dashboardResponse = await client.SendAsync(dashboard);
        var workspaceResponse = await client.SendAsync(workspace);
        var ordersResponse = await client.SendAsync(orders);

        Assert.Equal(HttpStatusCode.Forbidden, analyticsResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tablesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, branchesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, dashboardResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ordersResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);

        var today = await dashboardResponse.Content.ReadFromJsonAsync<ManagementTodayDashboardResponse>();
        Assert.NotNull(today);
        Assert.False(today.CanViewFinancials);
        Assert.Equal(0, today.TodaysRevenueMinor);

        var scope = await workspaceResponse.Content.ReadFromJsonAsync<ManagementWorkspaceResponse>();
        Assert.NotNull(scope?.Permissions);
        Assert.Contains(ManagementPermissions.OrderView, scope.Permissions);
        Assert.Contains(ManagementPermissions.OrderModify, scope.Permissions);
        Assert.DoesNotContain(ManagementPermissions.TableEdit, scope.Permissions);
        Assert.DoesNotContain(ManagementPermissions.AnalyticsView, scope.Permissions);
        Assert.DoesNotContain(ManagementPermissions.BranchManage, scope.Permissions);
    }

    [Fact]
    public async Task StaffCanReleaseOccupiedTableWithOrderModify()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedStaffAsync(factory, withOccupiedTable: true);

        var access = await LoginForAccessAsync(client, "staff@example.test");
        using var release = Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{SeedIds.TableA}/release");
        var response = await client.SendAsync(release);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task SeedStaffAsync(
        WebApplicationFactory<Program> factory,
        bool withOccupiedTable = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;

        await ManagementRolePermissionSync.SyncBuiltInRolesAsync(db, CancellationToken.None);
        await db.SaveChangesAsync();
        var staffRole = await db.ManagementRoles.SingleAsync(x => x.Name == "BranchStaff");

        var user = new ManagementUser(
            SeedIds.User,
            "staff@example.test",
            "STAFF@EXAMPLE.TEST",
            "pending",
            now);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ManagementUser>>();
        user.UpdatePasswordHash(hasher.HashPassword(user, Password));

        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            user,
            new ManagementMembership(Guid.NewGuid(), user.Id, SeedIds.TenantA, SeedIds.BranchA, staffRole.Id),
            new DiningTable(SeedIds.TableA, SeedIds.TenantA, SeedIds.BranchA, "Masa 1"));

        if (withOccupiedTable)
        {
            var order = new CustomerOrder(
                SeedIds.OrderA,
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                Guid.NewGuid(),
                $"key-{SeedIds.OrderA:N}",
                new string('A', 64),
                "#staff1",
                Money.Try(1_000),
                now,
                now.AddMinutes(18));
            order.ChangeStatus(OrderStatus.Preparing, now);
            db.CustomerOrders.Add(order);
        }
        else
        {
            var completed = new CustomerOrder(
                SeedIds.OrderA,
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                Guid.NewGuid(),
                $"key-{SeedIds.OrderA:N}",
                new string('A', 64),
                "#staff1",
                Money.Try(2_500),
                now,
                now.AddMinutes(18));
            completed.ChangeStatus(OrderStatus.Completed, now);
            db.CustomerOrders.Add(completed);
        }

        await db.SaveChangesAsync();
    }

    private static async Task<string> LoginForAccessAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new
            {
                email,
                password = Password,
                tenantId = SeedIds.TenantA,
                branchId = SeedIds.BranchA,
            });
        var access = await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(access);
        return access.AccessToken;
    }

    private static HttpRequestMessage Authorized(string accessToken, HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static WebApplicationFactory<Program> CreateFactory() => new RoleMatrixApiFactory();

    private sealed class RoleMatrixApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"role-matrix-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
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
        public static readonly Guid User = Guid.Parse("43000000-0000-0000-0000-000000000001");
        public static readonly Guid TableA = Guid.Parse("44000000-0000-0000-0000-000000000010");
        public static readonly Guid OrderA = Guid.Parse("44000000-0000-0000-0000-000000000001");
    }
}
