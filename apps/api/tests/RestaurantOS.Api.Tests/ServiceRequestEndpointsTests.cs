using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
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

public sealed class ServiceRequestEndpointsTests
{
    private const string ValidQrToken = "valid-opaque-qr-token-service-req-0001";

    [Fact]
    public async Task CustomerCanCreateWaiterCallAndManagementIsNotified()
    {
        var captured = new ConcurrentQueue<ServiceRequestResult>();
        using var factory = new Factory(captured);
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var session = await (await client.PostAsJsonAsync(
                "/api/v1/customer/sessions/resolve",
                new ResolveQrRequest(ValidQrToken, "tr")))
            .Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(session);

        var create = await client.PostAsJsonAsync(
            "/api/v1/customer/service-requests",
            new CreateServiceRequestRequest(session.SessionToken, "waiter"));
        var payload = await create.Content.ReadFromJsonAsync<CustomerServiceRequestResponse>();

        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        Assert.NotNull(payload);
        Assert.Equal("waiter", payload.Type);
        Assert.Equal("open", payload.Status);
        Assert.Equal("Masa 1", payload.TableLabel);
        Assert.True(captured.TryDequeue(out var notified));
        Assert.Equal(Guid.Parse(payload.Id), notified.Id);

        var cooldown = await client.PostAsJsonAsync(
            "/api/v1/customer/service-requests",
            new CreateServiceRequestRequest(session.SessionToken, "waiter"));
        Assert.Equal(HttpStatusCode.Conflict, cooldown.StatusCode);

        var sessionAfterRefresh = await (await client.PostAsJsonAsync(
                "/api/v1/customer/sessions/resolve",
                new ResolveQrRequest(ValidQrToken, "tr")))
            .Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(sessionAfterRefresh);
        Assert.Contains("waiter", sessionAfterRefresh.OpenServiceRequestTypes);

        var afterRefresh = await client.PostAsJsonAsync(
            "/api/v1/customer/service-requests",
            new CreateServiceRequestRequest(sessionAfterRefresh.SessionToken, "waiter"));
        Assert.Equal(HttpStatusCode.Conflict, afterRefresh.StatusCode);
    }

    private static async Task SeedAsync(Factory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = DateTimeOffset.UtcNow;
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
                now),
            new PublishedMenu(SeedIds.MenuA, SeedIds.TenantA, SeedIds.BranchA, "Published", now),
            new MenuCategory(SeedIds.CategoryA, SeedIds.TenantA, SeedIds.BranchA, SeedIds.MenuA, "Ana", 1),
            new MenuItem(
                SeedIds.ProductA,
                SeedIds.TenantA,
                SeedIds.BranchA,
                SeedIds.MenuA,
                SeedIds.CategoryA,
                "Levrek",
                "Izgara",
                Money.Try(42_000),
                true,
                1));
        await db.SaveChangesAsync();
    }

    private sealed class CapturingNotifier(ConcurrentQueue<ServiceRequestResult> captured) : IManagementOrderNotifier
    {
        public Task NotifyAsync(
            Guid tenantId,
            Guid branchId,
            CustomerOrderResult order,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifyServiceRequestAsync(
            Guid tenantId,
            Guid branchId,
            ServiceRequestResult request,
            CancellationToken cancellationToken)
        {
            captured.Enqueue(request);
            return Task.CompletedTask;
        }
    }

    private sealed class Factory(ConcurrentQueue<ServiceRequestResult> captured) : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"service-req-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ManagementAuth:SigningKey",
                "test-only-signing-key-32-bytes-minimum-value");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<RestaurantOsDbContext>>();
                services.AddDbContext<RestaurantOsDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<IManagementOrderNotifier>();
                services.AddSingleton<IManagementOrderNotifier>(new CapturingNotifier(captured));
            });
        }
    }

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");
        public static readonly Guid RestaurantA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb2");
        public static readonly Guid BranchA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb3");
        public static readonly Guid TableA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb4");
        public static readonly Guid MenuA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb5");
        public static readonly Guid CategoryA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb6");
        public static readonly Guid ProductA = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb7");
    }
}
