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

public sealed class ManagementMediaEndpointsTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task ListStockPhotosRequiresAuthentication()
    {
        using var factory = new MediaApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/management/media/stock");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListStockPhotosReturnsLibraryForAuthorizedUser()
    {
        using var factory = new MediaApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginAsync(client);

        var response = await client.SendAsync(Authorized(access, HttpMethod.Get, "/api/v1/management/media/stock?query=levrek"));
        var body = await response.Content.ReadFromJsonAsync<ManagementStockPhotoLibraryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.NotEmpty(body.Categories);
        Assert.Contains(body.Photos, photo => photo.Id == "izgara-levrek");
    }

    [Fact]
    public async Task ListStockPhotosReturnsEmptyForUnknownCategory()
    {
        using var factory = new MediaApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginAsync(client);

        var response = await client.SendAsync(Authorized(access, HttpMethod.Get, "/api/v1/management/media/stock?category=unknown"));
        var body = await response.Content.ReadFromJsonAsync<ManagementStockPhotoLibraryResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Empty(body.Photos);
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

    private static HttpRequestMessage Authorized(string accessToken, HttpMethod method, string path) =>
        new(method, path) { Headers = { Authorization = new AuthenticationHeaderValue("Bearer", accessToken) } };

    private static async Task SeedAsync(MediaApiFactory factory)
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
            user,
            new ManagementMembership(Guid.NewGuid(), user.Id, SeedIds.TenantA, SeedIds.BranchA, role.Id));
        await db.SaveChangesAsync();
    }

    public sealed class MediaApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"media-tests-{Guid.NewGuid()}";

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
        public static readonly Guid TenantA = Guid.Parse("54000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("54000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("54000000-0000-0000-0000-000000000003");
        public static readonly Guid User = Guid.Parse("54000000-0000-0000-0000-000000000004");
        public static readonly Guid Role = Guid.Parse("54000000-0000-0000-0000-000000000005");
        public static readonly DateTimeOffset CreatedAtUtc =
            DateTimeOffset.Parse("2026-08-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }
}
