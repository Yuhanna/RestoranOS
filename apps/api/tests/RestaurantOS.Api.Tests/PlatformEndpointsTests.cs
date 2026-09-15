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

    [Fact]
    public async Task RestaurantTokenCannotAccessPlatformCatalog()
    {
        var access = await LoginOwnerAsync();
        var response = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/v1/platform/catalog",
            access.AccessToken));
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            response.StatusCode.ToString());
    }

    [Fact]
    public async Task PlatformTokenCannotAccessRestaurantWorkspace()
    {
        var access = await LoginPlatformAsync();
        Assert.Equal(AuthRealms.Platform, access.Realm);
        Assert.Equal(Guid.Empty, access.TenantId);
        var response = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/v1/management/workspace",
            access.AccessToken));
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            response.StatusCode.ToString());
    }

    [Fact]
    public async Task PlatformOperatorCanReadAndVersionCatalogPrices()
    {
        var access = await LoginPlatformAsync();
        var catalog = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/v1/platform/catalog",
            access.AccessToken));
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);

        var draft = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/platform/catalog/prices",
            access.AccessToken,
            new ManagementCreatePlanPriceRequest(SubscriptionPlanCodes.Pro, BillingIntervals.Month, 199900)));
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var created = await draft.Content.ReadFromJsonAsync<ManagementPlanPriceResponse>();
        Assert.NotNull(created);
        Assert.Equal(PlanPriceStatuses.Draft, created.Status);

        var firstPublish = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/catalog/prices/{created.Id}/publish",
            access.AccessToken));
        Assert.Equal(HttpStatusCode.OK, firstPublish.StatusCode);

        var secondDraft = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/platform/catalog/prices",
            access.AccessToken,
            new ManagementCreatePlanPriceRequest(SubscriptionPlanCodes.Pro, BillingIntervals.Month, 299900)));
        var second = await secondDraft.Content.ReadFromJsonAsync<ManagementPlanPriceResponse>();
        Assert.NotNull(second);
        var secondPublish = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/catalog/prices/{second.Id}/publish",
            access.AccessToken));
        Assert.Equal(HttpStatusCode.OK, secondPublish.StatusCode);

        var listed = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/v1/platform/catalog",
            access.AccessToken));
        var body = await listed.Content.ReadFromJsonAsync<ManagementCatalogResponse>();
        Assert.NotNull(body);
        var proMonth = body.Products
            .Single(x => x.ProductCode == SubscriptionPlanCodes.Pro)
            .Prices
            .Where(x => x.Interval == BillingIntervals.Month)
            .ToArray();
        Assert.Contains(proMonth, x => x.Status == PlanPriceStatuses.Published && x.AmountMinor == 299900);
        Assert.Contains(proMonth, x => x.Status == PlanPriceStatuses.Archived && x.AmountMinor == 199900);
    }

    [Fact]
    public async Task BillingRoleCannotPublishPrices()
    {
        await SeedBillingStaffAsync();
        var login = await _client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new ManagementPlatformLoginRequest("billing@example.test", Password));
        login.EnsureSuccessStatusCode();
        var access = (await login.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>())!;
        var draft = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/platform/catalog/prices",
            access.AccessToken,
            new ManagementCreatePlanPriceRequest(SubscriptionPlanCodes.Pro, BillingIntervals.Year, 100000)));
        Assert.Equal(HttpStatusCode.Created, draft.StatusCode);
        var created = await draft.Content.ReadFromJsonAsync<ManagementPlanPriceResponse>();
        Assert.NotNull(created);
        var publish = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/catalog/prices/{created.Id}/publish",
            access.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
    }

    [Fact]
    public async Task RestaurantOwnerIsDeniedPlatformLoginWithoutLockingRestaurantAccess()
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var denied = await _client.PostAsJsonAsync(
                "/api/v1/platform/auth/login",
                new ManagementPlatformLoginRequest("owner@example.test", Password));
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        var restaurant = await LoginOwnerAsync();
        Assert.False(string.IsNullOrWhiteSpace(restaurant.AccessToken));
        Assert.Equal(AuthRealms.Management, restaurant.Realm);
    }

    [Fact]
    public async Task PlatformOperatorCannotOpenRestaurantWorkspaceLogin()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("platform@example.test", Password, null, null));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DemotedOwnerCannotPublishWithStaleAccessToken()
    {
        var access = await LoginPlatformAsync();
        var draft = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/platform/catalog/prices",
            access.AccessToken,
            new ManagementCreatePlanPriceRequest(SubscriptionPlanCodes.Pro, BillingIntervals.Month, 111100)));
        draft.EnsureSuccessStatusCode();
        var created = await draft.Content.ReadFromJsonAsync<ManagementPlanPriceResponse>();
        Assert.NotNull(created);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var staff = await db.PlatformStaff.SingleAsync(x => x.UserId == SeedIds.PlatformUser);
            staff.ChangeRole(PlatformStaffRoles.Billing);
            await db.SaveChangesAsync();
        }

        var publish = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/catalog/prices/{created.Id}/publish",
            access.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
    }

    [Fact]
    public async Task ReadOnlyStaffCanReadCatalogButCannotDraftPrices()
    {
        await SeedStaffAsync(SeedIds.ReadOnlyUser, "readonly@example.test", PlatformStaffRoles.ReadOnly);
        var access = await LoginPlatformAsAsync("readonly@example.test");
        var catalog = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/v1/platform/catalog",
            access.AccessToken));
        Assert.Equal(HttpStatusCode.OK, catalog.StatusCode);

        var draft = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/platform/catalog/prices",
            access.AccessToken,
            new ManagementCreatePlanPriceRequest(SubscriptionPlanCodes.Pro, BillingIntervals.Month, 100000)));
        Assert.Equal(HttpStatusCode.Forbidden, draft.StatusCode);
    }

    [Fact]
    public async Task SupportStaffCanWriteCampaignsButNotCatalogPrices()
    {
        await SeedStaffAsync(SeedIds.SupportUser, "support@example.test", PlatformStaffRoles.Support);
        var access = await LoginPlatformAsAsync("support@example.test");
        var draft = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/platform/catalog/prices",
            access.AccessToken,
            new ManagementCreatePlanPriceRequest(SubscriptionPlanCodes.Pro, BillingIntervals.Year, 100000)));
        Assert.Equal(HttpStatusCode.Forbidden, draft.StatusCode);

        var offer = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/platform/subscription-offers",
            access.AccessToken,
            new ManagementCreateSubscriptionOfferRequest(
                SubscriptionAudiences.NonPro,
                SubscriptionPlanCodes.Pro,
                15,
                2,
                "Destek kampanyası",
                "SaaS yükseltme teklifi",
                DateTimeOffset.UtcNow,
                null,
                true)));
        Assert.Equal(HttpStatusCode.Created, offer.StatusCode);
    }

    [Fact]
    public async Task ManagementRefreshCookieCannotMintPlatformSession()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@example.test", Password, SeedIds.TenantA, SeedIds.BranchA));
        login.EnsureSuccessStatusCode();
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        var managementCookie = cookies.First(x =>
            x.StartsWith("restaurantos-refresh=", StringComparison.OrdinalIgnoreCase)
            || x.StartsWith("__Secure-restaurantos-refresh=", StringComparison.OrdinalIgnoreCase));
        var token = managementCookie.Split(';', 2)[0].Split('=', 2)[1];
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/platform/auth/refresh");
        request.Headers.TryAddWithoutValidation("Cookie", $"restaurantos-platform-refresh={token}");
        var refresh = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task PlatformRefreshCookieCannotRotateManagementSession()
    {
        var login = await _client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new ManagementPlatformLoginRequest("platform@example.test", Password));
        login.EnsureSuccessStatusCode();
        Assert.True(login.Headers.TryGetValues("Set-Cookie", out var cookies));
        var platformCookie = cookies.First(x =>
            x.StartsWith("restaurantos-platform-refresh=", StringComparison.OrdinalIgnoreCase)
            || x.StartsWith("__Secure-restaurantos-platform-refresh=", StringComparison.OrdinalIgnoreCase));
        var token = platformCookie.Split(';', 2)[0].Split('=', 2)[1];

        var managementRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/management/auth/refresh");
        managementRefresh.Headers.TryAddWithoutValidation("Cookie", $"restaurantos-refresh={token}");
        var rejected = await _client.SendAsync(managementRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

        var platformRefresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/platform/auth/refresh");
        platformRefresh.Headers.TryAddWithoutValidation("Cookie", $"restaurantos-platform-refresh={token}");
        var stillValid = await _client.SendAsync(platformRefresh);
        Assert.Equal(HttpStatusCode.OK, stillValid.StatusCode);
    }

    [Fact]
    public async Task PlatformTokenCannotListRestaurantMemberships()
    {
        var access = await LoginPlatformAsync();
        var response = await _client.SendAsync(Authorized(
            HttpMethod.Get,
            "/api/v1/management/auth/memberships",
            access.AccessToken));
        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            response.StatusCode.ToString());
    }

    [Fact]
    public async Task BillingRoleCannotArchivePublishedPrices()
    {
        var owner = await LoginPlatformAsync();
        var draft = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            "/api/v1/platform/catalog/prices",
            owner.AccessToken,
            new ManagementCreatePlanPriceRequest(SubscriptionPlanCodes.Enterprise, BillingIntervals.Month, 500000)));
        draft.EnsureSuccessStatusCode();
        var created = await draft.Content.ReadFromJsonAsync<ManagementPlanPriceResponse>();
        Assert.NotNull(created);
        var published = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/catalog/prices/{created.Id}/publish",
            owner.AccessToken));
        published.EnsureSuccessStatusCode();

        await SeedBillingStaffAsync();
        var billing = await LoginPlatformAsAsync("billing@example.test");
        var archive = await _client.SendAsync(Authorized(
            HttpMethod.Post,
            $"/api/v1/platform/catalog/prices/{created.Id}/archive",
            billing.AccessToken));
        Assert.Equal(HttpStatusCode.Forbidden, archive.StatusCode);
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

    private async Task<ManagementAccessTokenResponse> LoginPlatformAsAsync(string email)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/platform/auth/login",
            new ManagementPlatformLoginRequest(email, Password));
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
            new ManagementMembership(Guid.NewGuid(), owner.Id, SeedIds.TenantA, SeedIds.BranchA, ownerRole.Id),
            new PlatformStaff(platform.Id, PlatformStaffRoles.Owner, now),
            new TenantSubscription(SeedIds.TenantA, SubscriptionPlanCodes.Free, now));
        await db.SaveChangesAsync();
    }

    private async Task SeedBillingStaffAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        if (await db.ManagementUsers.AnyAsync(x => x.Id == SeedIds.BillingUser))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var billing = new ManagementUser(
            SeedIds.BillingUser,
            "billing@example.test",
            "BILLING@EXAMPLE.TEST",
            "pending",
            now);
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<ManagementUser>>();
        billing.UpdatePasswordHash(hasher.HashPassword(billing, Password));
        db.AddRange(billing, new PlatformStaff(billing.Id, PlatformStaffRoles.Billing, now));
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
        var user = new ManagementUser(
            userId,
            email,
            email.ToUpperInvariant(),
            "pending",
            now);
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<ManagementUser>>();
        user.UpdatePasswordHash(hasher.HashPassword(user, Password));
        db.AddRange(user, new PlatformStaff(user.Id, roleCode, now));
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
            builder.UseSetting("ManagementAuth:PlatformAudience", "restaurant-os-platform");
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
        public static readonly Guid BillingUser = Guid.Parse("10000000-0000-0000-0000-000000000012");
        public static readonly Guid SupportUser = Guid.Parse("10000000-0000-0000-0000-000000000013");
        public static readonly Guid ReadOnlyUser = Guid.Parse("10000000-0000-0000-0000-000000000014");
        public static readonly Guid OwnerRole = Guid.Parse("10000000-0000-0000-0000-000000000020");
        public static readonly Guid PlatformRole = Guid.Parse("10000000-0000-0000-0000-000000000021");
    }
}
