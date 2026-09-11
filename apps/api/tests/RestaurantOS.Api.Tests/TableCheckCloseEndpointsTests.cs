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

public sealed class TableCheckCloseEndpointsTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task GetCheckAndCloseEmptyTableReturnsValidationError()
    {
        using var factory = new TableCheckApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginForAccessAsync(client);

        var check = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            $"/api/v1/management/tables/{SeedIds.TableA}/check"));
        var checkBody = await check.Content.ReadFromJsonAsync<ManagementTableCheckResponse>();
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.NotNull(checkBody);
        Assert.Equal(0, checkBody.RoundCount);
        Assert.Equal(0, checkBody.TotalAmountMinor);

        var close = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{SeedIds.TableA}/close-check",
            new ManagementCloseTableCheckRequest("cash")));
        Assert.Equal(HttpStatusCode.BadRequest, close.StatusCode);
        var problem = await close.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("TABLE_CHECK_EMPTY", problem?.Title ?? problem?.Code);
    }

    [Fact]
    public async Task UnfinishedKitchenRequiresConfirmThenForceCloseCompletesRounds()
    {
        using var factory = new TableCheckApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginForAccessAsync(client);
        var now = DateTimeOffset.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var sessionId = Guid.NewGuid();
            db.CustomerSessions.Add(new CustomerSession(
                sessionId,
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                new string('s', 64),
                "tr",
                now,
                now.AddHours(4)));
            var preparing = new CustomerOrder(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                sessionId,
                "idem-prep",
                new string('p', 64),
                "#P1",
                Money.Try(4500),
                now,
                now.AddMinutes(20));
            preparing.ChangeStatus(OrderStatus.Preparing, now);
            var ready = new CustomerOrder(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                sessionId,
                "idem-ready",
                new string('r', 64),
                "#R1",
                Money.Try(2500),
                now.AddMinutes(1),
                now.AddMinutes(25));
            ready.ChangeStatus(OrderStatus.Ready, now.AddMinutes(1));
            db.CustomerOrders.AddRange(preparing, ready);
            await db.SaveChangesAsync();
        }

        var blocked = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{SeedIds.TableA}/close-check",
            new ManagementCloseTableCheckRequest("card", ConfirmIncompleteKitchen: false)));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        var blockedProblem = await blocked.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("TABLE_HAS_UNFINISHED_ORDERS", blockedProblem?.Title ?? blockedProblem?.Code);

        var closed = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{SeedIds.TableA}/close-check",
            new ManagementCloseTableCheckRequest("card", ConfirmIncompleteKitchen: true, Note: "müşteri bekleyemedi")));
        var body = await closed.Content.ReadFromJsonAsync<ManagementTableCheckCloseResponse>();
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(2, body.ClosedOrderCount);
        Assert.Equal(7000, body.TotalAmountMinor);
        Assert.Equal("card", body.Tender);
        Assert.True(body.ForcedIncompleteKitchen);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var statuses = await db.CustomerOrders
                .Where(order => order.TableId == SeedIds.TableA)
                .Select(order => order.Status)
                .ToListAsync();
            Assert.All(statuses, status => Assert.Equal(OrderStatus.Completed, status));
            Assert.Contains(
                await db.ManagementAuditLogs.ToListAsync(),
                log => log.Action == "TableCheckClose" && log.Succeeded);
        }

        var history = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            "/api/v1/management/orders/history?hours=24"));
        var historyBody = await history.Content.ReadFromJsonAsync<ManagementOrderResponse[]>();
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.NotNull(historyBody);
        Assert.True(historyBody.Length >= 2);
    }

    [Fact]
    public async Task ReadyOnlyTableClosesWithoutForceConfirm()
    {
        using var factory = new TableCheckApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginForAccessAsync(client);
        var now = DateTimeOffset.UtcNow;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var sessionId = Guid.NewGuid();
            db.CustomerSessions.Add(new CustomerSession(
                sessionId,
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                new string('t', 64),
                "tr",
                now,
                now.AddHours(2)));
            var ready = new CustomerOrder(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                sessionId,
                "idem-ok",
                new string('o', 64),
                "#OK",
                Money.Try(1200),
                now,
                now.AddMinutes(15));
            ready.ChangeStatus(OrderStatus.Ready, now);
            db.CustomerOrders.Add(ready);
            await db.SaveChangesAsync();
        }

        var closed = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{SeedIds.TableA}/close-check",
            new ManagementCloseTableCheckRequest("nakit")));
        var body = await closed.Content.ReadFromJsonAsync<ManagementTableCheckCloseResponse>();
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("cash", body.Tender);
        Assert.False(body.ForcedIncompleteKitchen);
        Assert.Equal(1, body.ClosedOrderCount);
    }

    [Fact]
    public async Task ServedRoundsStayOnActiveBoardAndCheckUntilClose()
    {
        using var factory = new TableCheckApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginForAccessAsync(client);
        var now = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var sessionId = Guid.NewGuid();
            db.CustomerSessions.Add(new CustomerSession(
                sessionId,
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                new string('u', 64),
                "tr",
                now,
                now.AddHours(2)));
            var served = new CustomerOrder(
                orderId,
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                sessionId,
                "idem-served",
                new string('v', 64),
                "#SV",
                Money.Try(226000),
                now,
                now.AddMinutes(15));
            served.ChangeStatus(OrderStatus.Served, now);
            db.CustomerOrders.Add(served);
            await db.SaveChangesAsync();
        }

        var active = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            "/api/v1/management/orders/active"));
        var activeBody = await active.Content.ReadFromJsonAsync<ManagementOrderResponse[]>();
        Assert.Equal(HttpStatusCode.OK, active.StatusCode);
        Assert.NotNull(activeBody);
        Assert.Contains(activeBody, order => order.Id == orderId && order.Status == "served");

        var check = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            $"/api/v1/management/tables/{SeedIds.TableA}/check"));
        var checkBody = await check.Content.ReadFromJsonAsync<ManagementTableCheckResponse>();
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.NotNull(checkBody);
        Assert.Equal(1, checkBody.RoundCount);
        Assert.Equal(226000, checkBody.TotalAmountMinor);
        Assert.False(checkBody.HasIncompleteKitchen);

        var closed = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{SeedIds.TableA}/close-check",
            new ManagementCloseTableCheckRequest("cash")));
        Assert.Equal(HttpStatusCode.OK, closed.StatusCode);

        var activeAfter = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            "/api/v1/management/orders/active"));
        var activeAfterBody = await activeAfter.Content.ReadFromJsonAsync<ManagementOrderResponse[]>();
        Assert.NotNull(activeAfterBody);
        Assert.DoesNotContain(activeAfterBody, order => order.Id == orderId);
    }

    [Fact]
    public async Task InvalidTenderIsRejected()
    {
        using var factory = new TableCheckApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);
        var access = await LoginForAccessAsync(client);

        var response = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{SeedIds.TableA}/close-check",
            new ManagementCloseTableCheckRequest("bitcoin")));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        Assert.Equal("INVALID_TENDER", problem?.Title ?? problem?.Code);
    }

    private static async Task SeedAsync(TableCheckApiFactory factory)
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
            new DiningTable(SeedIds.TableA, SeedIds.TenantA, SeedIds.BranchA, "Masa 1"),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderView),
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderModify),
            user,
            new ManagementMembership(
                Guid.NewGuid(),
                user.Id,
                SeedIds.TenantA,
                SeedIds.BranchA,
                role.Id));
        await db.SaveChangesAsync();
    }

    private static async Task<string> LoginForAccessAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@example.test", Password, SeedIds.TenantA, SeedIds.BranchA));
        var access = await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(access);
        return access.AccessToken;
    }

    private static HttpRequestMessage Authorized(
        string accessToken,
        HttpMethod method,
        string url,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private sealed class ProblemDetailsDto
    {
        public string? Code { get; set; }
        public string? Title { get; set; }
        public string? Detail { get; set; }
    }

    public sealed class TableCheckApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"table-check-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(
                "ManagementAuth:SigningKey",
                "test-only-signing-key-32-bytes-minimum-value");
            builder.UseSetting("CustomerWeb:PublicBaseUrl", "http://localhost:5173");
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
        public static readonly Guid TenantA = Guid.Parse("51000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("51000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("51000000-0000-0000-0000-000000000003");
        public static readonly Guid TableA = Guid.Parse("51000000-0000-0000-0000-000000000004");
        public static readonly Guid User = Guid.Parse("51000000-0000-0000-0000-000000000005");
        public static readonly Guid Role = Guid.Parse("51000000-0000-0000-0000-000000000006");
        public static readonly DateTimeOffset CreatedAtUtc =
            DateTimeOffset.Parse("2026-09-11T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }
}
