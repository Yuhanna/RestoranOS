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

public sealed class IncidentEventsEndpointsTests
{
    private const string ValidQrToken = "valid-opaque-qr-token-incident-0001";
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task InvalidQrIsPersistedAsCustomerIncident()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var rejected = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new ResolveQrRequest("not-a-valid-qr-token", "tr"));
        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);

        var incident = await WaitForIncidentAsync(factory, "INVALID_QR");
        Assert.Equal("customer", incident.Channel);
        Assert.Equal(404, incident.HttpStatus);
        Assert.False(string.IsNullOrWhiteSpace(incident.CorrelationId));
    }

    [Fact]
    public async Task OrderRejectIncidentsEnrichScopeAndAreQueryableByBranch()
    {
        using var factory = new Factory();
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var sessionResponse = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new ResolveQrRequest(ValidQrToken, "tr"));
        var session = await sessionResponse.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(session);

        var unknownProductId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        using var orderRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customer/orders")
        {
            Content = JsonContent.Create(new CreateCustomerOrderRequest(
                session.SessionToken,
                [new CreateCustomerOrderLineRequest(unknownProductId.ToString(), 1, null, null)])),
        };
        orderRequest.Headers.Add("Idempotency-Key", "incident-reject-order-001");
        var rejected = await client.SendAsync(orderRequest);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);

        var incident = await WaitForIncidentAsync(factory, "ORDER_REJECTED");
        Assert.Equal(SeedIds.TenantA, incident.TenantId);
        Assert.Equal(SeedIds.BranchA, incident.BranchId);
        Assert.Equal(SeedIds.TableA, incident.TableId);
        Assert.NotNull(incident.CustomerSessionId);

        var login = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@incident.test", Password, SeedIds.TenantA, SeedIds.BranchA));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.NotNull(tokens);

        using var query = new HttpRequestMessage(HttpMethod.Get, "/api/v1/management/incidents?code=ORDER_REJECTED&limit=20");
        query.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var response = await client.SendAsync(query);
        var body = await response.Content.ReadFromJsonAsync<ManagementIncidentResponse[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Contains(
            body,
            row => row.Code == "ORDER_REJECTED"
                && row.TenantId == SeedIds.TenantA
                && row.BranchId == SeedIds.BranchA
                && row.TableId == SeedIds.TableA);
    }

    private static async Task<IncidentEvent> WaitForIncidentAsync(Factory factory, string code)
    {
        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var row = await db.IncidentEvents
                .AsNoTracking()
                .OrderByDescending(entry => entry.OccurredAtUtc)
                .FirstOrDefaultAsync(entry => entry.Code == code);
            if (row is not null)
            {
                return row;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Incident {code} was not persisted in time.");
    }

    private static async Task SeedAsync(Factory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
        var ownerRoleId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001");
        var ownerUserId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ManagementUser>>();
        var owner = new ManagementUser(
            ownerUserId,
            "owner@incident.test",
            "OWNER@INCIDENT.TEST",
            "pending",
            now,
            "Owner");
        owner.UpdatePasswordHash(hasher.HashPassword(owner, Password));
        var role = new ManagementRole(ownerRoleId, "Owner");

        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            new DiningTable(SeedIds.TableA, SeedIds.TenantA, SeedIds.BranchA, "Masa 1"),
            new TableQrCode(
                Guid.NewGuid(),
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.TableA,
                OpaqueToken.Hash(ValidQrToken),
                now.AddDays(-1)),
            new PublishedMenu(SeedIds.MenuA, SeedIds.TenantA, SeedIds.BranchA, "Published", now),
            new MenuCategory(SeedIds.CategoryA, SeedIds.TenantA, SeedIds.BranchA, SeedIds.MenuA, "Ana", 1),
            new MenuItem(
                SeedIds.ProductA,
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.MenuA,
                SeedIds.CategoryA,
                "Pizza",
                "Test pizza",
                Money.Try(42_000),
                true,
                1),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.OrderView),
            owner,
            new ManagementMembership(
                Guid.NewGuid(),
                ownerUserId,
                SeedIds.TenantA,
                SeedIds.BranchA,
                role.Id));
        await db.SaveChangesAsync();
    }

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111110001");
        public static readonly Guid RestaurantA = Guid.Parse("22222222-2222-2222-2222-222222220001");
        public static readonly Guid BranchA = Guid.Parse("33333333-3333-3333-3333-333333330001");
        public static readonly Guid TableA = Guid.Parse("44444444-4444-4444-4444-444444440001");
        public static readonly Guid MenuA = Guid.Parse("55555555-5555-5555-5555-555555550001");
        public static readonly Guid CategoryA = Guid.Parse("66666666-6666-6666-6666-666666660001");
        public static readonly Guid ProductA = Guid.Parse("77777777-7777-7777-7777-777777770001");
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = "IncidentEvents-" + Guid.NewGuid().ToString("N");

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<RestaurantOsDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<RestaurantOsDbContext>>();
                services.AddDbContext<RestaurantOsDbContext>(options => options.UseInMemoryDatabase(_dbName));
            });
        }
    }
}
