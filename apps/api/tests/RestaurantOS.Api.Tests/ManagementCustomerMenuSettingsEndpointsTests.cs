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

public sealed class ManagementCustomerMenuSettingsEndpointsTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task GetSettingsRequiresAuthentication()
    {
        using var factory = new CustomerMenuSettingsApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/management/customer-menu/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAndUpdateSettings()
    {
        using var factory = new CustomerMenuSettingsApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginAsync(client);

        var getResponse = await client.SendAsync(Authorized(access, HttpMethod.Get, "/api/v1/management/customer-menu/settings"));
        var initial = await getResponse.Content.ReadFromJsonAsync<ManagementCustomerMenuSettingsResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(initial);

        var updateResponse = await client.SendAsync(Authorized(
            access,
            HttpMethod.Put,
            "/api/v1/management/customer-menu/settings",
            new ManagementUpdateCustomerMenuSettingsRequest(
                ShowDietaryFilters: true,
                DietaryFilterOptions: ["vegan", "glutenFree"],
                ShowAllergenExclusions: true,
                AllergenExclusionOptions: ["nuts"],
                AllergenDisclaimer: "Lütfen alerjen bilgilerini kontrol edin.",
                AllergenMatrixUrl: "https://example.test/allergens")));
        var updated = await updateResponse.Content.ReadFromJsonAsync<ManagementCustomerMenuSettingsResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.True(updated.ShowDietaryFilters);
        Assert.Equal(["vegan", "glutenFree"], updated.DietaryFilterOptions);
        Assert.Equal("Lütfen alerjen bilgilerini kontrol edin.", updated.AllergenDisclaimer);
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

    private static async Task SeedAsync(CustomerMenuSettingsApiFactory factory)
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

    public sealed class CustomerMenuSettingsApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"customer-menu-settings-tests-{Guid.NewGuid()}";

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
        public static readonly Guid TenantA = Guid.Parse("56000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("56000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("56000000-0000-0000-0000-000000000003");
        public static readonly Guid User = Guid.Parse("56000000-0000-0000-0000-000000000004");
        public static readonly Guid Role = Guid.Parse("56000000-0000-0000-0000-000000000005");
        public static readonly DateTimeOffset CreatedAtUtc =
            DateTimeOffset.Parse("2026-08-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }
}
