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

public sealed class PlatformTenantBillingTests : IAsyncLifetime, IDisposable
{
    private const string Password = "strong-password-0001";
    private readonly WebApplicationFactory<Program> _factory = new Factory();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task TenantListOmitsBillingEmailAndRestaurantTokenIsDenied()
    {
        var owner = await LoginPlatformAsync();
        var listed = await _client.SendAsync(Authorized(HttpMethod.Get, "/api/v1/platform/tenants", owner.AccessToken));
        listed.EnsureSuccessStatusCode();
        var json = await listed.Content.ReadAsStringAsync();
        Assert.DoesNotContain("owner@example.test", json, StringComparison.OrdinalIgnoreCase);
        var page = await listed.Content.ReadFromJsonAsync<ManagementPlatformTenantListResponse>();
        Assert.Contains(page!.Items, x => x.TenantId == SeedIds.TenantA && x.OpenQuoteCount == 1);

        var restaurant = await LoginOwnerAsync();
        var denied = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/v1/platform/tenants",
            restaurant.AccessToken));
        Assert.True(
            denied.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            denied.StatusCode.ToString());
    }

    [Fact]
    public async Task OwnerCanOverrideTenantLimitsAndSupportCannot()
    {
        var owner = await LoginPlatformAsync();
        var updated = await _client.SendAsync(Authorized(
            HttpMethod.Patch,
            $"/api/v1/platform/tenants/{SeedIds.TenantA}/subscription",
            owner.AccessToken,
            new ManagementUpdatePlatformTenantSubscriptionRequest(
                SubscriptionPlanCodes.Enterprise,
                null,
                0,
                OverrideMaxBranches: 8,
                OverrideMaxOrderHistoryHours: 24 * 120,
                ContractNote: "sözleşme 8 şube")));
        updated.EnsureSuccessStatusCode();
        var detail = await updated.Content.ReadFromJsonAsync<ManagementPlatformTenantDetailResponse>();
        Assert.Equal(SubscriptionPlanCodes.Enterprise, detail!.PlanCode);
        Assert.Equal(8, detail.OverrideMaxBranches);
        Assert.Equal(8, detail.MaxBranches);
        Assert.Equal(24 * 120, detail.MaxOrderHistoryHours);

        await SeedStaffAsync(SeedIds.SupportUser, "support@example.test", PlatformStaffRoles.Support);
        var support = await LoginPlatformAsAsync("support@example.test");
        var blocked = await _client.SendAsync(Authorized(
            HttpMethod.Patch,
            $"/api/v1/platform/tenants/{SeedIds.TenantA}/subscription",
            support.AccessToken,
            new ManagementUpdatePlatformTenantSubscriptionRequest(SubscriptionPlanCodes.Pro)));
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
    }

    [Fact]
    public async Task AcceptQuoteActivatesEnterpriseAndSecondAcceptIsRejected()
    {
        var owner = await LoginPlatformAsync();
        var accepted = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/quotes/{SeedIds.QuoteA}/accept",
            owner.AccessToken,
            new ManagementAcceptPlatformQuoteRequest(
                OverrideMaxBranches: 15,
                ContractNote: "onay")));
        accepted.EnsureSuccessStatusCode();
        var quote = await accepted.Content.ReadFromJsonAsync<ManagementPlatformQuoteDetailResponse>();
        Assert.Equal(EnterpriseQuoteStatuses.Accepted, quote!.Status);

        var tenant = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            $"/api/v1/platform/tenants/{SeedIds.TenantA}",
            owner.AccessToken));
        tenant.EnsureSuccessStatusCode();
        var detail = await tenant.Content.ReadFromJsonAsync<ManagementPlatformTenantDetailResponse>();
        Assert.Equal(SubscriptionPlanCodes.Enterprise, detail!.PlanCode);
        Assert.Equal(15, detail.OverrideMaxBranches);

        var again = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/quotes/{SeedIds.QuoteA}/accept",
            owner.AccessToken,
            new ManagementAcceptPlatformQuoteRequest()));
        Assert.Equal(HttpStatusCode.Forbidden, again.StatusCode);
    }

    [Fact]
    public async Task SupportCanRejectQuoteButCannotAccept()
    {
        await SeedStaffAsync(SeedIds.SupportUser, "support@example.test", PlatformStaffRoles.Support);
        var support = await LoginPlatformAsAsync("support@example.test");
        var accept = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/quotes/{SeedIds.QuoteA}/accept",
            support.AccessToken,
            new ManagementAcceptPlatformQuoteRequest()));
        Assert.Equal(HttpStatusCode.Forbidden, accept.StatusCode);

        var reject = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/quotes/{SeedIds.QuoteA}/reject",
            support.AccessToken,
            new ManagementRejectPlatformQuoteRequest("uygun değil")));
        reject.EnsureSuccessStatusCode();
        var quote = await reject.Content.ReadFromJsonAsync<ManagementPlatformQuoteDetailResponse>();
        Assert.Equal(EnterpriseQuoteStatuses.Rejected, quote!.Status);

        var listed = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/v1/platform/quotes?status=Open",
            support.AccessToken));
        listed.EnsureSuccessStatusCode();
        var json = await listed.Content.ReadAsStringAsync();
        Assert.DoesNotContain("quote@example.test", json, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SeedAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var ownerRole = new ManagementRole(SeedIds.OwnerRole, "RestaurantOwner");
        var owner = new ManagementUser(SeedIds.OwnerUser, "owner@example.test", "OWNER@EXAMPLE.TEST", "pending", now);
        var platform = new ManagementUser(SeedIds.PlatformUser, "platform@example.test", "PLATFORM@EXAMPLE.TEST", "pending", now);
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<ManagementUser>>();
        owner.UpdatePasswordHash(hasher.HashPassword(owner, Password));
        platform.UpdatePasswordHash(hasher.HashPassword(platform, Password));
        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            ownerRole,
            owner,
            platform,
            new ManagementMembership(Guid.NewGuid(), owner.Id, SeedIds.TenantA, SeedIds.BranchA, ownerRole.Id),
            new PlatformStaff(platform.Id, PlatformStaffRoles.Owner, now),
            new TenantSubscription(SeedIds.TenantA, SubscriptionPlanCodes.Free, now),
            new EnterpriseQuoteRequest(
                SeedIds.QuoteA,
                SeedIds.TenantA,
                owner.Id,
                "Ada",
                "quote@example.test",
                "555",
                6,
                "çok şube",
                now));
        await db.SaveChangesAsync();
    }

    private async Task SeedStaffAsync(Guid userId, string email, string roleCode)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        if (await db.ManagementUsers.AnyAsync(x => x.Id == userId))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var user = new ManagementUser(userId, email, email.ToUpperInvariant(), "pending", now);
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<ManagementUser>>();
        user.UpdatePasswordHash(hasher.HashPassword(user, Password));
        db.AddRange(user, new PlatformStaff(user.Id, roleCode, now));
        await db.SaveChangesAsync();
    }

    private async Task<ManagementAccessTokenResponse> LoginOwnerAsync()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@example.test", Password, SeedIds.TenantA, SeedIds.BranchA));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>())!;
    }

    private async Task<ManagementAccessTokenResponse> LoginPlatformAsync() =>
        await LoginPlatformAsAsync("platform@example.test");

    private async Task<ManagementAccessTokenResponse> LoginPlatformAsAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new ManagementPlatformLoginRequest(email, Password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>())!;
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"platform-tenants-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            builder.UseSetting("ManagementAuth:SigningKey", "test-only-signing-key-32-bytes-minimum-value");
            builder.UseSetting("ManagementAuth:PlatformAudience", "restaurant-os-platform");
            builder.UseSetting("BootstrapAdmin:Password", "");
            builder.UseSetting("BootstrapPlatformAdmin:Password", "");
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
        public static readonly Guid TenantA = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("20000000-0000-0000-0000-000000000003");
        public static readonly Guid OwnerUser = Guid.Parse("20000000-0000-0000-0000-000000000010");
        public static readonly Guid PlatformUser = Guid.Parse("20000000-0000-0000-0000-000000000011");
        public static readonly Guid SupportUser = Guid.Parse("20000000-0000-0000-0000-000000000013");
        public static readonly Guid OwnerRole = Guid.Parse("20000000-0000-0000-0000-000000000020");
        public static readonly Guid QuoteA = Guid.Parse("20000000-0000-0000-0000-000000000030");
    }
}
