using System.Net;
using System.Net.Http.Json;
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

public sealed class ManagementNotificationsEndpointsTests : IAsyncLifetime, IDisposable
{
    private const string Password = "strong-password-0001";
    private readonly WebApplicationFactory<Program> _factory = new NotificationApiFactory();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task DispatchSendsEmailAndReturnsCounts()
    {
        var access = await LoginAsync();
        var notificationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa9");
        using var request = Authorized(
            HttpMethod.Post,
            $"/api/v1/management/notifications/{notificationId}/dispatch",
            access.AccessToken);
        var response = await _client.SendAsync(request);
        var payload = await response.Content.ReadFromJsonAsync<ManagementNotificationDispatchResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal(1, payload.EmailSentCount);
        Assert.Equal(1, payload.PushSentCount);
        Assert.Contains("owner@example.test", payload.RecipientEmails);
    }

    private async Task<ManagementAccessTokenResponse> LoginAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@example.test", Password, SeedIds.TenantA, SeedIds.BranchA));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>())!;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task SeedAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var role = new ManagementRole(SeedIds.Role, "RestaurantOwner");
        var user = new ManagementUser(
            SeedIds.User,
            "owner@example.test",
            "OWNER@EXAMPLE.TEST",
            "pending",
            now);
        user.UpdatePasswordHash(
            scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<ManagementUser>>()
                .HashPassword(user, Password));

        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            role,
            user,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.SubscriptionManage),
            new ManagementMembership(Guid.NewGuid(), user.Id, SeedIds.TenantA, SeedIds.BranchA, role.Id),
            new TenantSubscription(SeedIds.TenantA, SubscriptionPlanCodes.Free, now),
            new TenantNotification(
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa9"),
                SeedIds.TenantA,
                SubscriptionAudiences.Free,
                "Test bildirimi",
                "Free plan mesajı",
                now.AddDays(-1),
                now.AddMonths(1),
                null,
                true));
        await db.SaveChangesAsync();
    }

    private sealed class NotificationApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"notification-tests-{Guid.NewGuid()}";

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
        public static readonly Guid User = Guid.Parse("10000000-0000-0000-0000-000000000010");
        public static readonly Guid Role = Guid.Parse("10000000-0000-0000-0000-000000000011");
    }
}
