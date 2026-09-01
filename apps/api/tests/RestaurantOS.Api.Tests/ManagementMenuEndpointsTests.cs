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
using RestaurantOS.Application;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class ManagementMenuEndpointsTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task DraftMenuPublishesForCustomersAndStaysTenantIsolated()
    {
        using var factory = new MenuApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includePublish: true, includeEdit: true);
        var access = await LoginAsync(client);

        var created = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/menus",
            new ManagementCreateMenuRequest("Yaz Menüsü")));
        var menu = await created.Content.ReadFromJsonAsync<ManagementMenuSummaryResponse>();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("draft", menu?.Lifecycle);

        var emptyPublish = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu!.Id}/publish"));
        Assert.Equal(HttpStatusCode.Conflict, emptyPublish.StatusCode);

        var categoryResponse = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu.Id}/categories",
            new ManagementCreateCategoryRequest("Izgaralar", 1)));
        var category = await categoryResponse.Content.ReadFromJsonAsync<ManagementMenuCategoryResponse>();
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);

        var itemResponse = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu.Id}/items",
            new ManagementCreateMenuItemRequest(category!.Id, "Levrek", "Izgara", 42_000, true, 1)));
        Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);

        var foreignMenu = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            $"/api/v1/management/menus/{SeedIds.MenuB}"));
        Assert.Equal(HttpStatusCode.NotFound, foreignMenu.StatusCode);

        var published = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu.Id}/publish"));
        var publishedMenu = await published.Content.ReadFromJsonAsync<ManagementMenuSummaryResponse>();
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal("published", publishedMenu?.Lifecycle);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            db.DiningTables.Add(new DiningTable(SeedIds.TableA, SeedIds.TenantA, SeedIds.BranchA, "Masa 1"));
            db.TableQrCodes.Add(new TableQrCode(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                OpaqueToken.Hash("valid-opaque-qr-token-menu-publish-0001"),
                SeedIds.CreatedAtUtc));
            await db.SaveChangesAsync();
        }

        var resolve = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = "valid-opaque-qr-token-menu-publish-0001", locale = "tr" });
        var session = await resolve.Content.ReadFromJsonAsync<PublishedCustomerSession>();
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        Assert.Contains(session!.Products, product => product.Name == "Levrek");
        Assert.DoesNotContain(session.Products, product => product.Name == "Secret");

        var archived = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu.Id}/archive"));
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        var afterArchive = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = "valid-opaque-qr-token-menu-publish-0001", locale = "tr" });
        Assert.Equal(HttpStatusCode.Conflict, afterArchive.StatusCode);

        var editArchived = await client.SendAsync(Authorized(
            access,
            HttpMethod.Patch,
            $"/api/v1/management/menus/{menu.Id}",
            new ManagementRenameMenuRequest("Olmaz")));
        Assert.Equal(HttpStatusCode.Conflict, editArchived.StatusCode);
    }

    [Fact]
    public async Task PublishAndEditRequireDistinctPermissions()
    {
        using var factory = new MenuApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includePublish: false, includeEdit: true);
        var access = await LoginAsync(client);

        var created = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/menus",
            new ManagementCreateMenuRequest("Taslak")));
        var menu = await created.Content.ReadFromJsonAsync<ManagementMenuSummaryResponse>();
        var denied = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu!.Id}/publish"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@example.test", Password, SeedIds.TenantA, SeedIds.BranchA));
        var access = await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(access);
        return access.AccessToken;
    }

    private static HttpRequestMessage Authorized(string accessToken, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task SeedAsync(MenuApiFactory factory, bool includePublish, bool includeEdit)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var role = new ManagementRole(SeedIds.Role, "RestaurantManager");
        var user = new ManagementUser(SeedIds.User, "owner@example.test", "OWNER@EXAMPLE.TEST", "pending", SeedIds.CreatedAtUtc);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ManagementUser>>();
        user.UpdatePasswordHash(hasher.HashPassword(user, Password));
        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            new Tenant(SeedIds.TenantB, "Tenant B"),
            new Restaurant(SeedIds.RestaurantB, SeedIds.TenantB, "Restaurant B"),
            new Branch(SeedIds.BranchB, SeedIds.TenantB, SeedIds.RestaurantB, "Branch B"),
            new PublishedMenu(SeedIds.MenuB, SeedIds.TenantB, SeedIds.BranchB, "Secret", SeedIds.CreatedAtUtc),
            new MenuCategory(SeedIds.CategoryB, SeedIds.TenantB, SeedIds.BranchB, SeedIds.MenuB, "Hidden", 1),
            new MenuItem(
                SeedIds.ProductB,
                SeedIds.TenantB,
                SeedIds.BranchB,
                SeedIds.MenuB,
                SeedIds.CategoryB,
                "Secret",
                "Must not leak",
                Money.Try(1_000),
                true,
                1),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuView),
            user,
            new ManagementMembership(Guid.NewGuid(), user.Id, SeedIds.TenantA, SeedIds.BranchA, role.Id));
        if (includeEdit)
        {
            db.ManagementRolePermissions.Add(new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuEdit));
        }

        if (includePublish)
        {
            db.ManagementRolePermissions.Add(new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuPublish));
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateMenuItemWithEmptyCatalogListsSucceeds()
    {
        using var factory = new MenuApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includePublish: false, includeEdit: true);
        var access = await LoginAsync(client);

        var menuResponse = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/menus",
            new ManagementCreateMenuRequest("Katalog Test")));
        var menu = await menuResponse.Content.ReadFromJsonAsync<ManagementMenuSummaryResponse>();

        var categoryResponse = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu!.Id}/categories",
            new ManagementCreateCategoryRequest("Ana", 1)));
        var category = await categoryResponse.Content.ReadFromJsonAsync<ManagementMenuCategoryResponse>();

        var itemResponse = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu.Id}/items",
            new ManagementCreateMenuItemRequest(
                category!.Id,
                "Köfte",
                "Izgara",
                25_000,
                true,
                1,
                Catalog: new MenuItemCatalogData
                {
                    DietaryTags = [],
                    AllergenKeys = [],
                    MayContainAllergenKeys = [],
                    Ingredients = [],
                    ModifierGroups = [],
                    Portions = [],
                })));

        Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);
    }

    public sealed class MenuApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"menu-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ManagementAuth:SigningKey", "test-only-signing-key-32-bytes-minimum-value");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<RestaurantOsDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<RestaurantOsDbContext>>();
                services.RemoveAll<RestaurantOsDbContext>();
                services.AddDbContext<RestaurantOsDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            });
        }
    }

    private sealed record PublishedCustomerSession(IReadOnlyList<PublishedProduct> Products);
    private sealed record PublishedProduct(string Name);

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("51000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("51000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("51000000-0000-0000-0000-000000000003");
        public static readonly Guid TableA = Guid.Parse("51000000-0000-0000-0000-000000000004");
        public static readonly Guid TenantB = Guid.Parse("52000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantB = Guid.Parse("52000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchB = Guid.Parse("52000000-0000-0000-0000-000000000003");
        public static readonly Guid MenuB = Guid.Parse("52000000-0000-0000-0000-000000000005");
        public static readonly Guid CategoryB = Guid.Parse("52000000-0000-0000-0000-000000000006");
        public static readonly Guid ProductB = Guid.Parse("52000000-0000-0000-0000-000000000007");
        public static readonly Guid User = Guid.Parse("53000000-0000-0000-0000-000000000001");
        public static readonly Guid Role = Guid.Parse("53000000-0000-0000-0000-000000000002");
        public static readonly DateTimeOffset CreatedAtUtc =
            DateTimeOffset.Parse("2026-08-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }
}
