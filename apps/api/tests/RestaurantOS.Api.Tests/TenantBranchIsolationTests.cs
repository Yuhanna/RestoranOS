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

public sealed class TenantBranchIsolationTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task ListsAndWorkspaceStayInsideJwtTenantAndBranch()
    {
        using var factory = new IsolationApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var accessA = await LoginAsync(client, "owner-a@example.test", SeedIds.TenantA, SeedIds.BranchA1);
        var tablesA = await client.SendAsync(Authorized(accessA, HttpMethod.Get, "/api/v1/management/tables"));
        var menusA = await client.SendAsync(Authorized(accessA, HttpMethod.Get, "/api/v1/management/menus"));
        var workspaceA = await client.SendAsync(Authorized(accessA, HttpMethod.Get, "/api/v1/management/workspace"));
        var tablePayload = await tablesA.Content.ReadFromJsonAsync<List<ManagementTableResponse>>();
        var menuPayload = await menusA.Content.ReadFromJsonAsync<List<ManagementMenuSummaryResponse>>();
        var workspace = await workspaceA.Content.ReadFromJsonAsync<ManagementWorkspaceResponse>();

        Assert.Equal(HttpStatusCode.OK, tablesA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, menusA.StatusCode);
        Assert.Equal(HttpStatusCode.OK, workspaceA.StatusCode);
        Assert.NotNull(tablePayload);
        Assert.NotNull(menuPayload);
        Assert.NotNull(workspace);
        Assert.Single(tablePayload);
        Assert.Equal("Masa A1", tablePayload[0].Label);
        Assert.Single(menuPayload);
        Assert.Equal("Menü A1", menuPayload[0].Name);
        Assert.Equal(SeedIds.RestaurantA, workspace.RestaurantId);
        Assert.Equal("Restaurant A", workspace.RestaurantName);
        Assert.Equal(SeedIds.BranchA1, workspace.BranchId);
        Assert.Equal("Şube A1", workspace.BranchName);
    }

    [Fact]
    public async Task SiblingBranchAndForeignTenantDataAreNotVisible()
    {
        using var factory = new IsolationApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var accessA1 = await LoginAsync(client, "owner-a@example.test", SeedIds.TenantA, SeedIds.BranchA1);

        // Same tenant, same restaurant, sibling branch — must not leak.
        var siblingMenu = await client.SendAsync(Authorized(
            accessA1,
            HttpMethod.Get,
            $"/api/v1/management/menus/{SeedIds.MenuA2}"));
        var siblingTable = await client.SendAsync(Authorized(
            accessA1,
            HttpMethod.Get,
            $"/api/v1/management/tables/{SeedIds.TableA2}/qr-codes"));

        // Other tenant — must not leak.
        var foreignMenu = await client.SendAsync(Authorized(
            accessA1,
            HttpMethod.Get,
            $"/api/v1/management/menus/{SeedIds.MenuB}"));
        var foreignTable = await client.SendAsync(Authorized(
            accessA1,
            HttpMethod.Get,
            $"/api/v1/management/tables/{SeedIds.TableB}/qr-codes"));

        Assert.Equal(HttpStatusCode.NotFound, siblingMenu.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, siblingTable.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignMenu.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignTable.StatusCode);

        var list = await client.SendAsync(Authorized(accessA1, HttpMethod.Get, "/api/v1/management/menus"));
        var menus = await list.Content.ReadFromJsonAsync<List<ManagementMenuSummaryResponse>>();
        Assert.DoesNotContain(menus!, menu => menu.Id == SeedIds.MenuA2 || menu.Id == SeedIds.MenuB);
        Assert.Contains(menus!, menu => menu.Id == SeedIds.MenuA1);
    }

    [Fact]
    public async Task MembershipOnOneBranchCannotLoginToSiblingBranchWithoutMembership()
    {
        using var factory = new IsolationApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var denied = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest(
                "owner-a@example.test",
                Password,
                SeedIds.TenantA,
                SeedIds.BranchA2));

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
    }

    private static async Task<string> LoginAsync(
        HttpClient client,
        string email,
        Guid tenantId,
        Guid branchId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest(email, Password, tenantId, branchId));
        var access = await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(access);
        return access.AccessToken;
    }

    private static HttpRequestMessage Authorized(string accessToken, HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static async Task SeedAsync(IsolationApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ManagementUser>>();
        var role = new ManagementRole(SeedIds.Role, "RestaurantOwner");
        var userA = new ManagementUser(
            SeedIds.UserA,
            "owner-a@example.test",
            "OWNER-A@EXAMPLE.TEST",
            "pending",
            SeedIds.CreatedAtUtc);
        userA.UpdatePasswordHash(hasher.HashPassword(userA, Password));

        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA1, SeedIds.TenantA, SeedIds.RestaurantA, "Şube A1"),
            new Branch(SeedIds.BranchA2, SeedIds.TenantA, SeedIds.RestaurantA, "Şube A2"),
            new Tenant(SeedIds.TenantB, "Tenant B"),
            new Restaurant(SeedIds.RestaurantB, SeedIds.TenantB, "Restaurant B"),
            new Branch(SeedIds.BranchB, SeedIds.TenantB, SeedIds.RestaurantB, "Şube B"),
            new DiningTable(SeedIds.TableA1, SeedIds.TenantA, SeedIds.BranchA1, "Masa A1"),
            new DiningTable(SeedIds.TableA2, SeedIds.TenantA, SeedIds.BranchA2, "Masa A2"),
            new DiningTable(SeedIds.TableB, SeedIds.TenantB, SeedIds.BranchB, "Masa B"),
            new PublishedMenu(SeedIds.MenuA1, SeedIds.TenantA, SeedIds.BranchA1, "Menü A1"),
            new PublishedMenu(SeedIds.MenuA2, SeedIds.TenantA, SeedIds.BranchA2, "Menü A2"),
            new PublishedMenu(SeedIds.MenuB, SeedIds.TenantB, SeedIds.BranchB, "Menü B"),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.TableView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuView),
            userA,
            // Membership only on Branch A1 — not on sibling A2 or Tenant B.
            new ManagementMembership(
                Guid.NewGuid(),
                userA.Id,
                SeedIds.TenantA,
                SeedIds.BranchA1,
                role.Id));

        await db.SaveChangesAsync();
    }

    public sealed class IsolationApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"isolation-{Guid.NewGuid():N}";

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
                services.AddDbContext<RestaurantOsDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("61000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("61000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA1 = Guid.Parse("61000000-0000-0000-0000-000000000003");
        public static readonly Guid BranchA2 = Guid.Parse("61000000-0000-0000-0000-000000000004");
        public static readonly Guid TableA1 = Guid.Parse("61000000-0000-0000-0000-000000000005");
        public static readonly Guid TableA2 = Guid.Parse("61000000-0000-0000-0000-000000000006");
        public static readonly Guid MenuA1 = Guid.Parse("61000000-0000-0000-0000-000000000007");
        public static readonly Guid MenuA2 = Guid.Parse("61000000-0000-0000-0000-000000000008");
        public static readonly Guid TenantB = Guid.Parse("62000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantB = Guid.Parse("62000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchB = Guid.Parse("62000000-0000-0000-0000-000000000003");
        public static readonly Guid TableB = Guid.Parse("62000000-0000-0000-0000-000000000004");
        public static readonly Guid MenuB = Guid.Parse("62000000-0000-0000-0000-000000000005");
        public static readonly Guid UserA = Guid.Parse("63000000-0000-0000-0000-000000000001");
        public static readonly Guid Role = Guid.Parse("63000000-0000-0000-0000-000000000002");
        public static readonly DateTimeOffset CreatedAtUtc =
            DateTimeOffset.Parse("2026-08-26T09:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }
}
