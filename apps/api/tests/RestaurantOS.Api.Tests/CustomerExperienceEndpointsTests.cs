using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class CustomerExperienceEndpointsTests : IAsyncLifetime, IDisposable
{
    private const string ValidQrToken = "valid-opaque-qr-token-tenant-a-000000000001";
    private const string UnpublishedQrToken = "valid-opaque-qr-token-tenant-b-000000000002";
    private readonly CustomerApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task InvalidQrReturnsSafeProblemDetails()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new ResolveQrRequest(SeedIds.TableA.ToString(), "tr"));
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("INVALID_QR", problem?.Code);
        Assert.DoesNotContain("tenant", problem?.Detail ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnpublishedMenuIsNotExposed()
    {
        var response = await ResolveAsync(UnpublishedQrToken);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsContract>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MENU_UNAVAILABLE", problem?.Code);
    }

    [Fact]
    public async Task MenuAndOrderStayInsideResolvedTenantAndBranch()
    {
        var sessionResponse = await ResolveAsync(ValidQrToken);
        var session = await sessionResponse.Content.ReadFromJsonAsync<CustomerSessionResponse>();

        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.NotNull(session);
        Assert.Equal("Tenant A Restaurant", session.RestaurantName);
        Assert.Single(session.Products);
        Assert.Equal(SeedIds.ProductA.ToString(), session.Products[0].Id);
        Assert.DoesNotContain(session.Products, product => product.Id == SeedIds.ProductB.ToString());

        using var crossTenantOrder = CreateOrderRequest(
            session.SessionToken,
            "tenant-isolation-order-0001",
            SeedIds.ProductB);
        var rejected = await _client.SendAsync(crossTenantOrder);

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
    }

    [Fact]
    public async Task ResolveQrCreatesGuestSessionLinkedToTableSession()
    {
        var sessionResponse = await ResolveAsync(ValidQrToken);
        var session = await sessionResponse.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(session);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        var tableSession = await db.CustomerSessions.SingleAsync(
            x => x.TokenHash == OpaqueToken.Hash(session.SessionToken));
        var guestSession = await db.GuestSessions.SingleAsync(x => x.TableSessionId == tableSession.Id);

        Assert.Equal(GuestSessionStatus.Active, guestSession.Status);
        Assert.Equal(tableSession.TenantId, guestSession.TenantId);
        Assert.Equal(tableSession.BranchId, guestSession.BranchId);
    }

    [Fact]
    public async Task RepeatedOrderRequestReturnsSameOrder()
    {
        var sessionResponse = await ResolveAsync(ValidQrToken);
        var session = await sessionResponse.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(session);

        using var firstRequest = CreateOrderRequest(
            session.SessionToken,
            "repeat-order-key-0000001",
            SeedIds.ProductA);
        using var secondRequest = CreateOrderRequest(
            session.SessionToken,
            "repeat-order-key-0000001",
            SeedIds.ProductA);

        var firstResponse = await _client.SendAsync(firstRequest);
        var secondResponse = await _client.SendAsync(secondRequest);
        var first = await firstResponse.Content.ReadFromJsonAsync<CustomerOrderResponse>();
        var second = await secondResponse.Content.ReadFromJsonAsync<CustomerOrderResponse>();

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        Assert.Equal(1, await dbContext.CustomerOrders.CountAsync());
    }

    [Fact]
    public async Task GuestOrderRateLimitRejectsBurstOrders()
    {
        var sessionResponse = await ResolveAsync(ValidQrToken);
        var session = await sessionResponse.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(session);

        using var first = CreateOrderRequest(session.SessionToken, "rate-limit-order-001", SeedIds.ProductA);
        using var second = CreateOrderRequest(session.SessionToken, "rate-limit-order-002", SeedIds.ProductA);
        using var third = CreateOrderRequest(session.SessionToken, "rate-limit-order-003", SeedIds.ProductA);

        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(second)).StatusCode);

        var limited = await _client.SendAsync(third);
        var problem = await limited.Content.ReadFromJsonAsync<ProblemDetailsContract>();

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("ORDER_RATE_LIMITED", problem?.Code);
    }

    [Fact]
    public async Task IdempotentOrderRetryDoesNotConsumeGuestRateLimit()
    {
        var sessionResponse = await ResolveAsync(ValidQrToken);
        var session = await sessionResponse.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(session);

        using var first = CreateOrderRequest(session.SessionToken, "rate-limit-idempotent-001", SeedIds.ProductA);
        using var retry = CreateOrderRequest(session.SessionToken, "rate-limit-idempotent-001", SeedIds.ProductA);
        using var second = CreateOrderRequest(session.SessionToken, "rate-limit-idempotent-002", SeedIds.ProductA);
        using var third = CreateOrderRequest(session.SessionToken, "rate-limit-idempotent-003", SeedIds.ProductA);

        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(retry)).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await _client.SendAsync(second)).StatusCode);

        var limited = await _client.SendAsync(third);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task MenuPromotionAppliesDiscountToSessionAndOrder()
    {
        var sessionResponse = await ResolveAsync(ValidQrToken);
        var session = await sessionResponse.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(session);
        var product = session.Products[0];
        Assert.Equal(33_600, product.Price.AmountMinor);
        Assert.NotNull(product.Pricing);
        Assert.Equal(42_000, product.Pricing!.List.AmountMinor);
        Assert.Equal(8_400, product.Pricing.Discount.AmountMinor);

        using var createRequest = CreateOrderRequest(
            session.SessionToken,
            "discount-order-key-000001",
            SeedIds.ProductA);
        var orderResponse = await _client.SendAsync(createRequest);
        var order = await orderResponse.Content.ReadFromJsonAsync<CustomerOrderResponse>();

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        Assert.NotNull(order);
        Assert.Equal(67_200, order.Total.AmountMinor);
        Assert.Equal(84_000, order.Subtotal!.AmountMinor);
        Assert.Equal(16_800, order.Discount!.AmountMinor);
    }

    [Fact]
    public async Task OrderReadAllowsSameTableSessionAfterQrRescan()
    {
        var owner = await (await ResolveAsync(ValidQrToken))
            .Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(owner);

        using var createRequest = CreateOrderRequest(
            owner.SessionToken,
            "owned-order-key-00000001",
            SeedIds.ProductA);
        var order = await (await _client.SendAsync(createRequest))
            .Content.ReadFromJsonAsync<CustomerOrderResponse>();
        Assert.NotNull(order);

        var rescanned = await (await ResolveAsync(ValidQrToken))
            .Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(rescanned);
        Assert.Contains(rescanned.ActiveOrders, item => item.Id == order.Id);

        using var allowedRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/customer/orders/{order.Id}");
        allowedRequest.Headers.Add("X-Customer-Session", rescanned.SessionToken);
        var allowed = await _client.SendAsync(allowedRequest);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [Fact]
    public async Task StatusApplicationServiceEnforcesTenantAndBranch()
    {
        var owner = await (await ResolveAsync(ValidQrToken))
            .Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(owner);
        using var createRequest = CreateOrderRequest(
            owner.SessionToken,
            "status-order-key-0000001",
            SeedIds.ProductA);
        var order = await (await _client.SendAsync(createRequest))
            .Content.ReadFromJsonAsync<CustomerOrderResponse>();
        Assert.NotNull(order);

        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ICustomerExperienceService>();
        var denied = await Assert.ThrowsAsync<CustomerExperienceException>(() =>
            service.ChangeOrderStatusAsync(
                SeedIds.TenantB,
                SeedIds.BranchB,
                Guid.Parse(order.Id),
                "accepted",
                CancellationToken.None));
        Assert.Equal("ORDER_NOT_FOUND", denied.Code);

        var updated = await service.ChangeOrderStatusAsync(
            SeedIds.TenantA,
            SeedIds.BranchA,
            Guid.Parse(order.Id),
            "accepted",
            CancellationToken.None);
        Assert.Equal("accepted", updated.Status);
    }

    [Fact]
    public async Task SignalRHubAllowsAuthorizedOrderSubscription()
    {
        var sessionResponse = await ResolveAsync(ValidQrToken);
        var session = await sessionResponse.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(session);
        using var createRequest = CreateOrderRequest(
            session.SessionToken,
            "signalr-subscribe-key-0001",
            SeedIds.ProductA);
        var order = await (await _client.SendAsync(createRequest))
            .Content.ReadFromJsonAsync<CustomerOrderResponse>();
        Assert.NotNull(order);

        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(_client.BaseAddress!, "/hubs/v1/customer-orders"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();
        await connection.StartAsync(CancellationToken.None);

        var error = await Record.ExceptionAsync(() =>
            connection.InvokeAsync(
                "SubscribeToOrder",
                Guid.Parse(order.Id),
                session.SessionToken,
                CancellationToken.None));

        Assert.Null(error);
    }

    [Fact]
    public async Task SignalRHubRejectsUnauthorizedOrderSubscription()
    {
        await using var connection = new HubConnectionBuilder()
            .WithUrl(
                new Uri(_client.BaseAddress!, "/hubs/v1/customer-orders"),
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
            .Build();
        await connection.StartAsync(CancellationToken.None);

        await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync(
                "SubscribeToOrder",
                Guid.NewGuid(),
                "invalid-session-token",
                CancellationToken.None));
    }

    private Task<HttpResponseMessage> ResolveAsync(string token) =>
        _client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new ResolveQrRequest(token, "tr"));

    private static HttpRequestMessage CreateOrderRequest(string sessionToken, string key, Guid productId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customer/orders");
        request.Headers.Add("Idempotency-Key", key);
        request.Content = JsonContent.Create(new CreateCustomerOrderRequest(
            sessionToken,
            [new CreateCustomerOrderLineRequest(productId.ToString(), 2, [], "Az tuzlu")]));
        return request;
    }

    private async Task SeedAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();

        var now = DateTimeOffset.UtcNow;
        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Tenant A Restaurant"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            new DiningTable(SeedIds.TableA, SeedIds.TenantA, SeedIds.BranchA, "Masa 7"),
            new TableQrCode(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                OpaqueToken.Hash(ValidQrToken),
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
                1),
            new MenuPromotion(
                Guid.Parse("10000000-0000-0000-0000-000000000099"),
                SeedIds.TenantA,
                SeedIds.BranchA,
                "Test %20",
                PromotionScopes.AllMenu,
                DiscountKinds.Percent,
                20,
                now.AddDays(-1),
                endsAtUtc: null,
                dailyStartLocal: null,
                dailyEndLocal: null,
                categoryId: null,
                menuItemId: null,
                isActive: true),
            new Tenant(SeedIds.TenantB, "Tenant B"),
            new Restaurant(SeedIds.RestaurantB, SeedIds.TenantB, "Tenant B Restaurant"),
            new Branch(SeedIds.BranchB, SeedIds.TenantB, SeedIds.RestaurantB, "Branch B"),
            new DiningTable(SeedIds.TableB, SeedIds.TenantB, SeedIds.BranchB, "Masa 9"),
            new TableQrCode(
                Guid.NewGuid(),
                SeedIds.TenantB,
                SeedIds.BranchB,
                SeedIds.TableB,
                OpaqueToken.Hash(UnpublishedQrToken),
                now),
            new PublishedMenu(SeedIds.MenuB, SeedIds.TenantB, SeedIds.BranchB, "Draft"),
            new MenuCategory(SeedIds.CategoryB, SeedIds.TenantB, SeedIds.BranchB, SeedIds.MenuB, "Other", 1),
            new MenuItem(
                SeedIds.ProductB,
                SeedIds.TenantB,
                SeedIds.BranchB,
                SeedIds.MenuB,
                SeedIds.CategoryB,
                "Other product",
                "Must never leak",
                Money.Try(1_000),
                true,
                1));
        await db.SaveChangesAsync();
    }

    private sealed class CustomerApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"customer-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
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

    private sealed record ProblemDetailsContract(string? Title, string? Detail, string? Code);

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("10000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("10000000-0000-0000-0000-000000000003");
        public static readonly Guid TableA = Guid.Parse("10000000-0000-0000-0000-000000000004");
        public static readonly Guid MenuA = Guid.Parse("10000000-0000-0000-0000-000000000005");
        public static readonly Guid CategoryA = Guid.Parse("10000000-0000-0000-0000-000000000006");
        public static readonly Guid ProductA = Guid.Parse("10000000-0000-0000-0000-000000000007");
        public static readonly Guid TenantB = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantB = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchB = Guid.Parse("20000000-0000-0000-0000-000000000003");
        public static readonly Guid TableB = Guid.Parse("20000000-0000-0000-0000-000000000004");
        public static readonly Guid MenuB = Guid.Parse("20000000-0000-0000-0000-000000000005");
        public static readonly Guid CategoryB = Guid.Parse("20000000-0000-0000-0000-000000000006");
        public static readonly Guid ProductB = Guid.Parse("20000000-0000-0000-0000-000000000007");
    }
}
