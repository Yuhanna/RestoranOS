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

public sealed class ManagementPromotionsEndpointsTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task ListMenuPromotionsRequiresAuthentication()
    {
        using var factory = new PromotionsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/management/promotions/menu");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateAndListMenuPromotion()
    {
        using var factory = new PromotionsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginAsync(client);
        var startsAt = DateTimeOffset.UtcNow;

        var createResponse = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/promotions/menu",
            new ManagementCreateMenuPromotionRequest(
                "Hafta sonu %10",
                PromotionScopes.AllMenu,
                DiscountKinds.Percent,
                10,
                startsAt,
                startsAt.AddDays(7),
                null,
                null,
                null,
                null,
                true)));
        var created = await createResponse.Content.ReadFromJsonAsync<ManagementMenuPromotionResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(created);
        Assert.Equal("Hafta sonu %10", created.Name);

        var listResponse = await client.SendAsync(Authorized(access, HttpMethod.Get, "/api/v1/management/promotions/menu"));
        var items = await listResponse.Content.ReadFromJsonAsync<ManagementMenuPromotionResponse[]>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(items);
        Assert.Contains(items, item => item.Id == created.Id);
    }

    [Fact]
    public async Task CreateMenuPromotionRequiresName()
    {
        using var factory = new PromotionsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginAsync(client);

        var response = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/promotions/menu",
            new ManagementCreateMenuPromotionRequest(
                " ",
                PromotionScopes.AllMenu,
                DiscountKinds.Percent,
                10,
                DateTimeOffset.UtcNow,
                null,
                null,
                null,
                null,
                null,
                true)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        var request = new HttpRequestMessage(method, path)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) },
        };
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static async Task SeedAsync(PromotionsApiFactory factory)
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
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.MenuEdit),
            user,
            new ManagementMembership(Guid.NewGuid(), user.Id, SeedIds.TenantA, SeedIds.BranchA, role.Id));
        await db.SaveChangesAsync();
    }

    public sealed class PromotionsApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"promotions-tests-{Guid.NewGuid()}";

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

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("55000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("55000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("55000000-0000-0000-0000-000000000003");
        public static readonly Guid User = Guid.Parse("55000000-0000-0000-0000-000000000004");
        public static readonly Guid Role = Guid.Parse("55000000-0000-0000-0000-000000000005");
        public static readonly DateTimeOffset CreatedAtUtc =
            DateTimeOffset.Parse("2026-08-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }
}
