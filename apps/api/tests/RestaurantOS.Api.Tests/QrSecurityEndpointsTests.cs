using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

/// <summary>
/// Spec §86 — QR security: invalid/revoked/inactive QR denied, valid QR creates session,
/// expired session blocked, table context stays bound after resolve.
/// </summary>
public sealed class QrSecurityEndpointsTests : IAsyncLifetime, IDisposable
{
    private const string ActiveQrToken = "qr-security-active-token-tenant-a-00000001";
    private const string RevocableQrToken = "qr-security-revocable-active-token-a-0001";
    private const string RevokedQrToken = "qr-security-revoked-token-tenant-a-000001";
    private const string InactiveQrToken = "qr-security-inactive-token-tenant-a-000001";
    private const string TableBActiveQrToken = "qr-security-table-b-active-token-a-00001";

    private readonly QrSecurityApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await SeedAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task MissingOrMalformedQrTokenIsDeniedWithoutLeakingTenantData()
    {
        var emptyResponse = await ResolveAsync("");
        var emptyProblem = await emptyResponse.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        Assert.Equal(HttpStatusCode.BadRequest, emptyResponse.StatusCode);
        Assert.Equal("VALIDATION_ERROR", emptyProblem?.Code);
        Assert.DoesNotContain("Tenant A", emptyProblem?.Detail ?? string.Empty, StringComparison.Ordinal);

        foreach (var token in new[] { "short", new string('x', 513) })
        {
            var response = await ResolveAsync(token);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("INVALID_QR", problem?.Code);
            Assert.DoesNotContain("Tenant A", problem?.Detail ?? string.Empty, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task UnknownQrTokenIsDenied()
    {
        var response = await ResolveAsync(new string('z', 48));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("INVALID_QR", problem?.Code);
    }

    [Fact]
    public async Task RevokedQrCannotCreateCustomerSession()
    {
        var response = await ResolveAsync(RevokedQrToken);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("INVALID_QR", problem?.Code);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        Assert.Equal(0, await db.CustomerSessions.CountAsync());
    }

    [Fact]
    public async Task InactiveQrCannotCreateCustomerSession()
    {
        var response = await ResolveAsync(InactiveQrToken);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("INVALID_QR", problem?.Code);
    }

    [Fact]
    public async Task DeactivatedTableBlocksActiveQrResolve()
    {
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var table = await db.DiningTables.SingleAsync(x => x.Id == SeedIds.TableA);
            table.SetActive(false);
            await db.SaveChangesAsync();
        }

        try
        {
            var response = await ResolveAsync(ActiveQrToken);
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("INVALID_QR", problem?.Code);
        }
        finally
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var table = await db.DiningTables.SingleAsync(x => x.Id == SeedIds.TableA);
            table.SetActive(true);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task ValidActiveQrCreatesSessionWithMenu()
    {
        var response = await ResolveAsync(ActiveQrToken);
        var session = await response.Content.ReadFromJsonAsync<CustomerSessionResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionToken));
        Assert.Equal("Tenant A Restaurant", session.RestaurantName);
        Assert.Equal("Masa 7", session.TableLabel);
        Assert.NotEmpty(session.Products);
    }

    [Fact]
    public async Task OrderWithoutValidSessionIsDenied()
    {
        using var request = CreateOrderRequest("not-a-valid-customer-session-token-00000001", "no-session-order");
        var response = await _client.SendAsync(request);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_SESSION", problem?.Code);
    }

    [Fact]
    public async Task ExpiredSessionCannotPlaceOrder()
    {
        var session = await ResolveSessionAsync(ActiveQrToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var stored = await db.CustomerSessions.SingleAsync();
            db.Entry(stored).Property("ExpiresAtUtc").CurrentValue = DateTimeOffset.UtcNow.AddMinutes(-5);
            await db.SaveChangesAsync();
        }

        using var orderRequest = CreateOrderRequest(session.SessionToken, "expired-session-order");
        var response = await _client.SendAsync(orderRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("INVALID_SESSION", problem?.Code);
    }

    [Fact]
    public async Task ResolvingAnotherTableQrBindsOrderToThatTableOnly()
    {
        var tableASession = await ResolveSessionAsync(ActiveQrToken);
        var tableBSession = await ResolveSessionAsync(TableBActiveQrToken);

        Assert.Equal("Masa 7", tableASession.TableLabel);
        Assert.Equal("Masa 9", tableBSession.TableLabel);
        Assert.NotEqual(tableASession.SessionToken, tableBSession.SessionToken);

        using var orderA = CreateOrderRequest(tableASession.SessionToken, "table-a-order-key-00001");
        using var orderB = CreateOrderRequest(tableBSession.SessionToken, "table-b-order-key-00001");
        var responseA = await _client.SendAsync(orderA);
        var responseB = await _client.SendAsync(orderB);
        Assert.Equal(HttpStatusCode.Created, responseA.StatusCode);
        Assert.Equal(HttpStatusCode.Created, responseB.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        var orders = await db.CustomerOrders.AsNoTracking().OrderBy(x => x.CreatedAtUtc).ToListAsync();
        Assert.Equal(2, orders.Count);
        Assert.All(orders, order => Assert.Equal(SeedIds.TenantA, order.TenantId));
        Assert.Contains(orders, order => order.TableId == SeedIds.TableA);
        Assert.Contains(orders, order => order.TableId == SeedIds.TableB);
    }

    [Fact]
    public async Task ExistingSessionSurvivesQrRevocationButNewResolveFails()
    {
        var session = await ResolveSessionAsync(RevocableQrToken);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var qr = await db.TableQrCodes.SingleAsync(x => x.TokenHash == OpaqueToken.Hash(RevocableQrToken));
            qr.Revoke(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var blocked = await ResolveAsync(RevocableQrToken);
        Assert.Equal(HttpStatusCode.NotFound, blocked.StatusCode);

        using var orderRequest = CreateOrderRequest(session.SessionToken, "post-revoke-order-key-01");
        var orderResponse = await _client.SendAsync(orderRequest);
        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
    }

    private async Task<CustomerSessionResponse> ResolveSessionAsync(string token)
    {
        var response = await ResolveAsync(token);
        var session = await response.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(session);
        return session;
    }

    private Task<HttpResponseMessage> ResolveAsync(string token) =>
        _client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new ResolveQrRequest(token, "tr"));

    private static HttpRequestMessage CreateOrderRequest(string sessionToken, string idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customer/orders");
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new CreateCustomerOrderRequest(
            sessionToken,
            [new CreateCustomerOrderLineRequest(SeedIds.ProductA.ToString(), 1, [], null)]));
        return request;
    }

    private async Task SeedAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        var revokedQr = new TableQrCode(
            Guid.NewGuid(),
            SeedIds.TenantA,
            SeedIds.BranchA,
            SeedIds.TableA,
            OpaqueToken.Hash(RevokedQrToken),
            now);
        revokedQr.Revoke(now);

        var inactiveQr = new TableQrCode(
            Guid.NewGuid(),
            SeedIds.TenantA,
            SeedIds.BranchA,
            SeedIds.TableA,
            OpaqueToken.Hash(InactiveQrToken),
            now);
        inactiveQr.Deactivate();

        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Tenant A Restaurant"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            new DiningTable(SeedIds.TableA, SeedIds.TenantA, SeedIds.BranchA, "Masa 7"),
            new DiningTable(SeedIds.TableB, SeedIds.TenantA, SeedIds.BranchA, "Masa 9"),
            new TableQrCode(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                OpaqueToken.Hash(ActiveQrToken),
                now),
            new TableQrCode(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                OpaqueToken.Hash(RevocableQrToken),
                now),
            revokedQr,
            inactiveQr,
            new TableQrCode(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableB,
                OpaqueToken.Hash(TableBActiveQrToken),
                now),
            new PublishedMenu(SeedIds.MenuA, SeedIds.TenantA, SeedIds.BranchA, "Published", now),
            new MenuCategory(SeedIds.CategoryA, SeedIds.TenantA, SeedIds.BranchA, SeedIds.MenuA, "Ana yemekler", 1),
            new MenuItem(
                SeedIds.ProductA,
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.MenuA,
                SeedIds.CategoryA,
                "Levrek",
                "Izgara levrek",
                Money.Try(42_000),
                true,
                1));
        await db.SaveChangesAsync();
    }

    private sealed class QrSecurityApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"qr-security-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
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

    private sealed record ProblemDetailsContract(string? Title, string? Detail, string? Code);

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("61000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("61000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("61000000-0000-0000-0000-000000000003");
        public static readonly Guid TableA = Guid.Parse("61000000-0000-0000-0000-000000000004");
        public static readonly Guid TableB = Guid.Parse("61000000-0000-0000-0000-000000000005");
        public static readonly Guid MenuA = Guid.Parse("61000000-0000-0000-0000-000000000006");
        public static readonly Guid CategoryA = Guid.Parse("61000000-0000-0000-0000-000000000007");
        public static readonly Guid ProductA = Guid.Parse("61000000-0000-0000-0000-000000000008");
    }
}
