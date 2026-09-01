using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;
using static RestaurantOS.Api.Tests.ManagementTableQrEndpointsTests;

namespace RestaurantOS.Api.Tests;

public sealed class TableOccupancyIsolationTests
{
    private const string Password = "A-strong-test-password!42";
    private static readonly DateTimeOffset SeedNow = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.Parse("61000000-0000-0000-0000-000000000001");
    private static readonly Guid RestaurantId = Guid.Parse("61000000-0000-0000-0000-000000000002");
    private static readonly Guid BranchId = Guid.Parse("61000000-0000-0000-0000-000000000003");
    private static readonly Guid RoleId = Guid.Parse("61000000-0000-0000-0000-000000000004");
    private static readonly Guid UserId = Guid.Parse("61000000-0000-0000-0000-000000000005");

    [Fact]
    public async Task OrderOnOneTableDoesNotAffectOtherTables()
    {
        using var factory = new ManagementTableApiFactory();
        using var client = factory.CreateClient();
        await SeedTablesAsync(factory);
        var access = await LoginAsync(client);

        var masa1 = await CreateTableAsync(client, access, "Masa1");
        var a1 = await CreateTableAsync(client, access, "A1");
        await CreateTableAsync(client, access, "Masa3");
        var qr = await GenerateQrAsync(client, access, masa1.Id);
        await EnsurePublishedMenuAsync(factory);
        await SeedMenuItemAsync(factory);

        var resolve = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = qr.Token, locale = "tr" });
        var session = await resolve.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        Assert.NotNull(session);
        Assert.Equal("Masa1", session.TableLabel);

        var orderRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customer/orders");
        orderRequest.Headers.Add("Idempotency-Key", "occupancy-isolation-order-1");
        orderRequest.Content = JsonContent.Create(new
        {
            sessionToken = session.SessionToken,
            lines = new[] { new { productId = MenuProductId.ToString(), quantity = 1, note = (string?)null, modifierOptionIds = Array.Empty<string>() } },
        });
        Assert.Equal(HttpStatusCode.Created, (await client.SendAsync(orderRequest)).StatusCode);

        var tables = await ListTablesAsync(client, access);
        Assert.Equal("has_pending_order", tables.Single(table => table.Id == masa1.Id).OperationalStatus);
        Assert.Equal("available", tables.Single(table => table.Id == a1.Id).OperationalStatus);
        Assert.Equal("available", tables.Single(table => table.Label == "Masa3").OperationalStatus);
    }

    private static readonly Guid MenuProductId = Guid.Parse("71000000-0000-0000-0000-000000000001");

    private static async Task SeedMenuItemAsync(ManagementTableApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        var menu = await db.Menus.SingleAsync(menu => menu.TenantId == TenantId && menu.PublishedAtUtc != null);
        if (await db.MenuItems.AnyAsync(item => item.Id == MenuProductId))
        {
            return;
        }

        var categoryId = Guid.Parse("71000000-0000-0000-0000-000000000002");
        db.MenuCategories.Add(new MenuCategory(categoryId, TenantId, BranchId, menu.Id, "Ana", 1));
        db.MenuItems.Add(new MenuItem(
            MenuProductId,
            TenantId,
            BranchId,
            menu.Id,
            categoryId,
            "Test ürün",
            "Test",
            Money.Try(10_000),
            true,
            1));
        await db.SaveChangesAsync();
    }

    private sealed record CustomerSessionResponse(
        string SessionToken,
        string RestaurantName,
        string BranchName,
        string TableLabel,
        string Locale);

    [Fact]
    public async Task ResolvingOneTableQrMarksOnlyThatTableOccupied()
    {
        using var factory = new ManagementTableApiFactory();
        using var client = factory.CreateClient();
        await SeedTablesAsync(factory);
        var access = await LoginAsync(client);

        var masa1 = await CreateTableAsync(client, access, "Masa1");
        await CreateTableAsync(client, access, "A1");
        var qr = await GenerateQrAsync(client, access, masa1.Id);
        await EnsurePublishedMenuAsync(factory);

        var resolve = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = qr.Token, locale = "tr" });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);

        var tables = await ListTablesAsync(client, access);
        var masa1Status = tables.Single(table => table.Label == "Masa1").OperationalStatus;
        var a1Status = tables.Single(table => table.Label == "A1").OperationalStatus;

        Assert.Equal("occupied", masa1Status);
        Assert.Equal("available", a1Status);
    }

    [Fact]
    public async Task RescanningSameTableSupersedesPreviousSession()
    {
        using var factory = new ManagementTableApiFactory();
        using var client = factory.CreateClient();
        await SeedTablesAsync(factory);
        var access = await LoginAsync(client);

        var masa1 = await CreateTableAsync(client, access, "Masa1");
        var qr = await GenerateQrAsync(client, access, masa1.Id);
        await EnsurePublishedMenuAsync(factory);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = qr.Token, locale = "tr" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = qr.Token, locale = "tr" })).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        var activeSessions = await db.CustomerSessions
            .Where(session => session.TableId == masa1.Id && session.ExpiresAtUtc > DateTimeOffset.UtcNow)
            .CountAsync();
        Assert.Equal(1, activeSessions);
    }

    [Fact]
    public async Task ReleaseTableClearsOperationalOccupancy()
    {
        using var factory = new ManagementTableApiFactory();
        using var client = factory.CreateClient();
        await SeedTablesAsync(factory);
        var access = await LoginAsync(client);

        var a1 = await CreateTableAsync(client, access, "A1");
        var qr = await GenerateQrAsync(client, access, a1.Id);
        await EnsurePublishedMenuAsync(factory);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = qr.Token, locale = "tr" })).StatusCode);

        var release = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{a1.Id}/release"));
        Assert.Equal(HttpStatusCode.OK, release.StatusCode);

        var tables = await ListTablesAsync(client, access);
        Assert.Equal("available", tables.Single(table => table.Id == a1.Id).OperationalStatus);
    }

    private static async Task SeedTablesAsync(ManagementTableApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = SeedNow;
        var role = new ManagementRole(RoleId, "RestaurantManager");
        var user = new ManagementUser(
            UserId,
            "owner@example.test",
            "OWNER@EXAMPLE.TEST",
            "pending",
            now);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ManagementUser>>();
        user.UpdatePasswordHash(hasher.HashPassword(user, Password));
        db.AddRange(
            new Tenant(TenantId, "Tenant A"),
            new Restaurant(RestaurantId, TenantId, "Restaurant A"),
            new Branch(BranchId, TenantId, RestaurantId, "Branch A"),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.TableView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.TableEdit),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderModify),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuPublish),
            user,
            new ManagementMembership(
                Guid.NewGuid(),
                user.Id,
                TenantId,
                BranchId,
                role.Id));
        await db.SaveChangesAsync();
    }

    private static async Task EnsurePublishedMenuAsync(ManagementTableApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        if (await db.Menus.AnyAsync(menu => menu.TenantId == TenantId && menu.PublishedAtUtc != null))
        {
            return;
        }

        db.Menus.Add(new PublishedMenu(Guid.NewGuid(), TenantId, BranchId, "Live", SeedNow));
        await db.SaveChangesAsync();
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@example.test", Password, TenantId, BranchId));
        var access = await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(access);
        return access.AccessToken;
    }

    private static async Task<ManagementTableResponse> CreateTableAsync(
        HttpClient client,
        string access,
        string label)
    {
        var response = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/tables",
            new ManagementCreateTableRequest(label)));
        var table = await response.Content.ReadFromJsonAsync<ManagementTableResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(table);
        return table;
    }

    private static async Task<ManagementGeneratedQrResponse> GenerateQrAsync(
        HttpClient client,
        string access,
        Guid tableId)
    {
        var response = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{tableId}/qr-codes"));
        var qr = await response.Content.ReadFromJsonAsync<ManagementGeneratedQrResponse>();
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(qr);
        return qr;
    }

    private static async Task<IReadOnlyList<ManagementTableResponse>> ListTablesAsync(
        HttpClient client,
        string access)
    {
        var response = await client.SendAsync(Authorized(access, HttpMethod.Get, "/api/v1/management/tables"));
        var tables = await response.Content.ReadFromJsonAsync<ManagementTableResponse[]>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(tables);
        return tables;
    }

    private static HttpRequestMessage Authorized(
        string accessToken,
        HttpMethod method,
        string path,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }
}
