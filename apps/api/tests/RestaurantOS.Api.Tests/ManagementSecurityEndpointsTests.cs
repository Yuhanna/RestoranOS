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

public sealed class ManagementSecurityEndpointsTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task LoginRejectsInvalidCredentialsAndStoresOnlyHashedSecrets()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);

        var denied = await LoginAsync(client, "owner@example.test", "wrong-password");
        var allowed = await LoginAsync(client, "owner@example.test", Password);
        var token = await allowed.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        var refreshToken = ReadRefreshCookie(allowed);

        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.NotNull(token);
        Assert.NotEmpty(refreshToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        var user = await db.ManagementUsers.SingleAsync();
        var session = await db.ManagementRefreshSessions.SingleAsync();
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.DoesNotContain(refreshToken, user.PasswordHash, StringComparison.Ordinal);
        Assert.NotEqual(refreshToken, session.TokenHash);
        Assert.Equal(OpaqueToken.Hash(refreshToken), session.TokenHash);
        Assert.Contains(await db.ManagementAuditLogs.ToListAsync(), x => x.Action == "FailedLogin" && !x.Succeeded);
        Assert.Contains(await db.ManagementAuditLogs.ToListAsync(), x => x.Action == "Login" && x.Succeeded);
    }

    [Fact]
    public async Task ActiveMembershipAndPermissionAreRevalidatedForEveryRequest()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);
        var login = await LoginAsync(client, "owner@example.test", Password);
        var access = await login.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.NotNull(access);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var membership = await db.ManagementMemberships.SingleAsync();
            membership.Deactivate();
            await db.SaveChangesAsync();
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/management/orders/active");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access.AccessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StatusChangeEnforcesPermissionAndTenantBranchIsolation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);
        var access = await LoginForAccessAsync(client);

        var foreignResponse = await ChangeStatusAsync(client, access, SeedIds.OrderB, "accepted");
        var ownResponse = await ChangeStatusAsync(client, access, SeedIds.OrderA, "accepted");

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        Assert.Equal(OrderStatus.Submitted, (await db.CustomerOrders.FindAsync(SeedIds.OrderB))!.Status);
        Assert.Equal(OrderStatus.Accepted, (await db.CustomerOrders.FindAsync(SeedIds.OrderA))!.Status);
        var audit = await db.ManagementAuditLogs.SingleAsync(x => x.Action == "OrderStatusChange");
        Assert.Equal(SeedIds.OrderA, audit.SubjectId);
        Assert.Equal(SeedIds.TenantA, audit.TenantId);
        Assert.Equal(SeedIds.BranchA, audit.BranchId);
    }

    [Fact]
    public async Task StatusChangeWithoutModifyPermissionIsForbidden()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: false);
        var access = await LoginForAccessAsync(client);

        var response = await ChangeStatusAsync(client, access, SeedIds.OrderA, "accepted");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StatusChangeRequiresAuthentication()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);

        var response = await client.PutAsJsonAsync(
            $"/api/v1/management/orders/{SeedIds.OrderA}/status",
            new ManagementChangeOrderStatusRequest("accepted", SeedIds.CreatedAtUtc));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BackwardStatusTransitionReturnsConflictProblemWithoutAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);
        var access = await LoginForAccessAsync(client);

        var forward = await ChangeStatusAsync(client, access, SeedIds.OrderA, "ready", SeedIds.CreatedAtUtc);
        Assert.Equal(HttpStatusCode.OK, forward.StatusCode);
        var ready = await forward.Content.ReadFromJsonAsync<ManagementOrderResponse>();
        Assert.NotNull(ready);

        var response = await ChangeStatusAsync(
            client,
            access,
            SeedIds.OrderA,
            "preparing",
            ready.StatusChangedAtUtc);
        var problem = await response.Content.ReadFromJsonAsync<ProblemContract>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("INVALID_STATUS_TRANSITION", problem?.Code);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        Assert.DoesNotContain(
            await db.ManagementAuditLogs.ToListAsync(),
            x => x.Action == "OrderStatusChange" && x.Detail == "preparing");
    }

    [Fact]
    public async Task StatusChangeRejectsStaleExpectedVersion()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);
        var access = await LoginForAccessAsync(client);

        var first = await ChangeStatusAsync(client, access, SeedIds.OrderA, "accepted");
        var stale = await ChangeStatusAsync(client, access, SeedIds.OrderA, "accepted");
        var problem = await stale.Content.ReadFromJsonAsync<ProblemContract>();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("ORDER_CONCURRENCY_CONFLICT", problem?.Code);
    }

    [Fact]
    public async Task ManagementHubNegotiationRequiresOrderViewPermission()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);
        var access = await LoginForAccessAsync(client);

        var anonymous = await client.PostAsync(
            "/hubs/v1/management-orders/negotiate?negotiateVersion=1",
            content: null);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/hubs/v1/management-orders/negotiate?negotiateVersion=1");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var authenticated = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
    }

    [Fact]
    public async Task RefreshRotationDetectsReplayAndRevokesTokenFamily()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);
        var login = await LoginAsync(client, "owner@example.test", Password);
        var originalRefresh = ReadRefreshCookie(login);

        var rotated = await RefreshAsync(client, originalRefresh);
        var replacementRefresh = ReadRefreshCookie(rotated);
        var replay = await RefreshAsync(client, originalRefresh);
        var replacementAfterReplay = await RefreshAsync(client, replacementRefresh);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        Assert.NotEqual(originalRefresh, replacementRefresh);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replacementAfterReplay.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        Assert.All(await db.ManagementRefreshSessions.ToListAsync(), x => Assert.NotNull(x.RevokedAtUtc));
        Assert.Contains(await db.ManagementAuditLogs.ToListAsync(), x => x.Action == "RefreshReplay");
    }

    [Fact]
    public async Task LoginRateLimitRejectsBruteForceBurst()
    {
        using var factory = CreateFactory(loginLimit: 2);
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);

        var first = await LoginAsync(client, "owner@example.test", "wrong-1");
        var second = await LoginAsync(client, "owner@example.test", "wrong-2");
        var limited = await LoginAsync(client, "owner@example.test", "wrong-3");

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task RepeatedFailuresLockAccountEvenWithCorrectPassword()
    {
        using var factory = CreateFactory(loginLimit: 100);
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeModifyPermission: true);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var denied = await LoginAsync(client, "owner@example.test", $"wrong-{attempt}");
            Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        }

        var locked = await LoginAsync(client, "owner@example.test", Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
    }

    private static ManagementApiFactory CreateFactory(int loginLimit = 100) => new(loginLimit);

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest(email, password, SeedIds.TenantA, SeedIds.BranchA));

    private static async Task<string> LoginForAccessAsync(HttpClient client)
    {
        var response = await LoginAsync(client, "owner@example.test", Password);
        var access = await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(access);
        return access.AccessToken;
    }

    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/management/auth/refresh");
        request.Headers.Add("Cookie", $"__Secure-restaurantos-refresh={refreshToken}");
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> ChangeStatusAsync(
        HttpClient client,
        string accessToken,
        Guid orderId,
        string status,
        DateTimeOffset? expectedStatusChangedAtUtc = null)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/management/orders/{orderId}/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(
            new ManagementChangeOrderStatusRequest(
                status,
                expectedStatusChangedAtUtc ?? SeedIds.CreatedAtUtc));
        return client.SendAsync(request);
    }

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        var header = response.Headers.GetValues("Set-Cookie")
            .Single(x => x.StartsWith("__Secure-restaurantos-refresh=", StringComparison.Ordinal));
        return header.Split(';', 2)[0].Split('=', 2)[1];
    }

    private static async Task SeedAsync(ManagementApiFactory factory, bool includeModifyPermission)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = SeedIds.CreatedAtUtc;
        var role = new ManagementRole(SeedIds.Role, "RestaurantManager");
        var user = new ManagementUser(
            SeedIds.User,
            "owner@example.test",
            "OWNER@EXAMPLE.TEST",
            "pending",
            now);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ManagementUser>>();
        user.UpdatePasswordHash(hasher.HashPassword(user, Password));
        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            new Tenant(SeedIds.TenantB, "Tenant B"),
            new Restaurant(SeedIds.RestaurantB, SeedIds.TenantB, "Restaurant B"),
            new Branch(SeedIds.BranchB, SeedIds.TenantB, SeedIds.RestaurantB, "Branch B"),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderView),
            user,
            new ManagementMembership(
                Guid.NewGuid(),
                user.Id,
                SeedIds.TenantA,
                SeedIds.BranchA,
                role.Id),
            CreateOrder(SeedIds.OrderA, SeedIds.TenantA, SeedIds.BranchA, now),
            CreateOrder(SeedIds.OrderB, SeedIds.TenantB, SeedIds.BranchB, now));
        if (includeModifyPermission)
        {
            db.ManagementRolePermissions.Add(
                new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderModify));
        }

        await db.SaveChangesAsync();
    }

    private static CustomerOrder CreateOrder(
        Guid id,
        Guid tenantId,
        Guid branchId,
        DateTimeOffset now) =>
        new(
            id,
            tenantId,
            branchId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"key-{id:N}",
            new string('A', 64),
            $"#{id.ToString("N")[..6]}",
            Money.Try(1_000),
            now,
            now.AddMinutes(18));

    public sealed class ManagementApiFactory(int loginLimit) : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"management-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(
                "ManagementAuth:SigningKey",
                "test-only-signing-key-32-bytes-minimum-value");
            builder.UseSetting(
                "RateLimits:ManagementLogin:PermitLimit",
                loginLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
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
        public static readonly Guid TenantA = Guid.Parse("31000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("31000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("31000000-0000-0000-0000-000000000003");
        public static readonly Guid TenantB = Guid.Parse("32000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantB = Guid.Parse("32000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchB = Guid.Parse("32000000-0000-0000-0000-000000000003");
        public static readonly Guid User = Guid.Parse("33000000-0000-0000-0000-000000000001");
        public static readonly Guid Role = Guid.Parse("33000000-0000-0000-0000-000000000002");
        public static readonly Guid OrderA = Guid.Parse("34000000-0000-0000-0000-000000000001");
        public static readonly Guid OrderB = Guid.Parse("34000000-0000-0000-0000-000000000002");
        public static readonly DateTimeOffset CreatedAtUtc =
            DateTimeOffset.Parse("2026-08-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }

    private sealed record ProblemContract(string? Code);
}
