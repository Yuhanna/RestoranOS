using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class PlatformEndpointsTests : IAsyncLifetime, IDisposable
{
    private const string Password = "strong-password-0001";
    private readonly WebApplicationFactory<Program> _factory = new PlatformApiFactory();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TenantCannotCreateBroadcastNotification()
    {
        var access = await LoginOwnerAsync();
        var response = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/management/notifications",
            access.AccessToken,
            new ManagementCreateNotificationRequest(
                SubscriptionAudiences.Free,
                "Platform denemesi",
                "Mesaj",
                DateTimeOffset.UtcNow,
                null,
                null,
                true,
                BroadcastToAllTenants: true)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PlatformOperatorCanAccessPlatformConsole()
    {
        var access = await LoginPlatformAsync();
        var response = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/v1/platform/access",
            access.AccessToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PlatformOperatorCanCreatePlatformNotification()
    {
        var access = await LoginPlatformAsync();
        var response = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/platform/notifications",
            access.AccessToken,
            new ManagementCreatePlatformNotificationRequest(
                SubscriptionAudiences.NonPro,
                "Pro teklifi",
                "Tüm restoranlara mesaj",
                DateTimeOffset.UtcNow,
                null,
                "/subscription",
                true)));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<ManagementAccessTokenResponse> LoginOwnerAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@example.test", Password, SeedIds.TenantA, SeedIds.BranchA));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>())!;
    }

    private async Task<ManagementAccessTokenResponse> LoginPlatformAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new ManagementPlatformLoginRequest("platform@example.test", Password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>())!;
    }

    private static HttpRequestMessage Authorized(
        HttpMethod method,
        string path,
        string accessToken,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private async Task SeedAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var ownerRole = new ManagementRole(SeedIds.OwnerRole, "RestaurantOwner");
        var platformRole = new ManagementRole(SeedIds.PlatformRole, "PlatformOperator");
        var owner = new ManagementUser(
            SeedIds.OwnerUser,
            "owner@example.test",
            "OWNER@EXAMPLE.TEST",
            "pending",
            now);
        var platform = new ManagementUser(
            SeedIds.PlatformUser,
            "platform@example.test",
            "PLATFORM@EXAMPLE.TEST",
            "pending",
            now);
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<ManagementUser>>();
        owner.UpdatePasswordHash(hasher.HashPassword(owner, Password));
        platform.UpdatePasswordHash(hasher.HashPassword(platform, Password));

        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            ownerRole,
            platformRole,
            owner,
            platform,
            new ManagementRolePermissionGrant(ownerRole.Id, ManagementPermissions.SubscriptionManage),
            new ManagementRolePermissionGrant(platformRole.Id, ManagementPermissions.PlatformManage),
            new ManagementMembership(Guid.NewGuid(), owner.Id, SeedIds.TenantA, SeedIds.BranchA, ownerRole.Id),
            new ManagementMembership(Guid.NewGuid(), platform.Id, SeedIds.TenantA, SeedIds.BranchA, platformRole.Id),
            new TenantSubscription(SeedIds.TenantA, SubscriptionPlanCodes.Free, now));
        await db.SaveChangesAsync();
    }

    private sealed class PlatformApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"platform-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
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
        public static readonly Guid TenantA = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("10000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("10000000-0000-0000-0000-000000000003");
        public static readonly Guid OwnerUser = Guid.Parse("10000000-0000-0000-0000-000000000010");
        public static readonly Guid PlatformUser = Guid.Parse("10000000-0000-0000-0000-000000000011");
        public static readonly Guid OwnerRole = Guid.Parse("10000000-0000-0000-0000-000000000020");
        public static readonly Guid PlatformRole = Guid.Parse("10000000-0000-0000-0000-000000000021");
    }
}
