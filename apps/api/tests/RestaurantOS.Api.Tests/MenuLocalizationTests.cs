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

public sealed class MenuLocalizationTests
{
    private const string Password = "A-strong-test-password!42";
    private const string QrToken = "valid-opaque-qr-token-localized-menu-01";

    [Fact]
    public async Task EnglishTranslationIsServedToCustomersWithTurkishFallback()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginAsync(client);

        var created = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/menus",
            new ManagementCreateMenuRequest("Akşam")));
        var menu = await created.Content.ReadFromJsonAsync<ManagementMenuSummaryResponse>();
        var category = await (await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu!.Id}/categories",
            new ManagementCreateCategoryRequest("Izgaralar", 1))))
            .Content.ReadFromJsonAsync<ManagementMenuCategoryResponse>();
        var item = await (await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu.Id}/items",
            new ManagementCreateMenuItemRequest(category!.Id, "Levrek", "Izgara levrek", 42_000, true, 1))))
            .Content.ReadFromJsonAsync<ManagementMenuItemResponse>();

        var unsupported = await client.SendAsync(Authorized(
            access,
            HttpMethod.Put,
            $"/api/v1/management/menu-items/{item!.Id}/translations/de",
            new ManagementUpsertTranslationRequest("Seebarsch", "Gegrillt")));
        Assert.Equal(HttpStatusCode.BadRequest, unsupported.StatusCode);

        var translated = await client.SendAsync(Authorized(
            access,
            HttpMethod.Put,
            $"/api/v1/management/menu-items/{item.Id}/translations/en",
            new ManagementUpsertTranslationRequest("Sea bass", "Grilled sea bass")));
        var categoryTranslated = await client.SendAsync(Authorized(
            access,
            HttpMethod.Put,
            $"/api/v1/management/menu-categories/{category.Id}/translations/en",
            new ManagementUpsertTranslationRequest("Grill", null)));
        Assert.Equal(HttpStatusCode.OK, translated.StatusCode);
        Assert.Equal(HttpStatusCode.OK, categoryTranslated.StatusCode);

        await client.SendAsync(Authorized(access, HttpMethod.Post, $"/api/v1/management/menus/{menu.Id}/publish"));

        var english = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = QrToken, locale = "en" });
        var englishSession = await english.Content.ReadFromJsonAsync<LocalizedSession>();
        Assert.Equal(HttpStatusCode.OK, english.StatusCode);
        Assert.Equal("en", englishSession?.Locale);
        Assert.Contains(englishSession!.Products, product => product.Name == "Sea bass");
        Assert.Contains(englishSession.Categories, cat => cat.Name == "Grill");

        var turkish = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = QrToken, locale = "tr" });
        var turkishSession = await turkish.Content.ReadFromJsonAsync<LocalizedSession>();
        Assert.Contains(turkishSession!.Products, product => product.Name == "Levrek");
    }

    private static async Task<string> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@example.test", Password, SeedIds.TenantA, SeedIds.BranchA));
        var access = await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return access!.AccessToken;
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

    private static async Task SeedAsync(Factory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var role = new ManagementRole(SeedIds.Role, "RestaurantManager");
        var user = new ManagementUser(SeedIds.User, "owner@example.test", "OWNER@EXAMPLE.TEST", "pending", SeedIds.Now);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ManagementUser>>();
        user.UpdatePasswordHash(hasher.HashPassword(user, Password));
        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            new DiningTable(SeedIds.TableA, SeedIds.TenantA, SeedIds.BranchA, "Masa 1"),
            new TableQrCode(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                OpaqueToken.Hash(QrToken),
                SeedIds.Now),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuEdit),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuPublish),
            user,
            new ManagementMembership(Guid.NewGuid(), user.Id, SeedIds.TenantA, SeedIds.BranchA, role.Id),
            new TenantSubscription(SeedIds.TenantA, SubscriptionPlanCodes.Pro, SeedIds.Now));
        await db.SaveChangesAsync();
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"locale-tests-{Guid.NewGuid()}";

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

    private sealed record LocalizedSession(
        string Locale,
        IReadOnlyList<Named> Categories,
        IReadOnlyList<Named> Products);

    private sealed record Named(string Name);

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("61000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("61000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("61000000-0000-0000-0000-000000000003");
        public static readonly Guid TableA = Guid.Parse("61000000-0000-0000-0000-000000000004");
        public static readonly Guid User = Guid.Parse("63000000-0000-0000-0000-000000000001");
        public static readonly Guid Role = Guid.Parse("63000000-0000-0000-0000-000000000002");
        public static readonly DateTimeOffset Now =
            DateTimeOffset.Parse("2026-08-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }
}
