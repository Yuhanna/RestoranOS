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

public sealed class ManagementOrderCreatedNotifyTests
{
    private const string ValidQrToken = "valid-opaque-qr-token-mgmt-notify-00001";

    [Fact]
    public async Task CreatingCustomerOrderNotifiesManagementHubPayload()
    {
        var captured = new ConcurrentQueue<(Guid TenantId, Guid BranchId, CustomerOrderResult Order)>();
        using var factory = new Factory(captured);
        using var client = factory.CreateClient();
        await SeedAsync(factory);

        var session = await (await client.PostAsJsonAsync(
                "/api/v1/customer/sessions/resolve",
                new ResolveQrRequest(ValidQrToken, "tr")))
            .Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.NotNull(session);

        using var createRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customer/orders");
        createRequest.Headers.Add("Idempotency-Key", "mgmt-notify-order-key-01");
        createRequest.Content = JsonContent.Create(new CreateCustomerOrderRequest(
            session.SessionToken,
            [new CreateCustomerOrderLineRequest(SeedIds.ProductA.ToString(), 1, [], null)]));
        var createResponse = await client.SendAsync(createRequest);
        var order = await createResponse.Content.ReadFromJsonAsync<CustomerOrderResponse>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(order);
        Assert.True(captured.TryDequeue(out var notification));
        Assert.Equal(SeedIds.TenantA, notification.TenantId);
        Assert.Equal(SeedIds.BranchA, notification.BranchId);
        Assert.Equal(Guid.Parse(order.Id), notification.Order.Id);
        Assert.Equal("submitted", notification.Order.Status);
        Assert.NotNull(notification.Order.CreatedAtUtc);
        Assert.True(captured.IsEmpty);
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

    private sealed class CapturingManagementOrderNotifier(
        ConcurrentQueue<(Guid TenantId, Guid BranchId, CustomerOrderResult Order)> captured)
        : IManagementOrderNotifier
    {
        public Task NotifyAsync(
            Guid tenantId,
            Guid branchId,
            CustomerOrderResult order,
            CancellationToken cancellationToken)
        {
            captured.Enqueue((tenantId, branchId, order));
            return Task.CompletedTask;
        }

        public Task NotifyServiceRequestAsync(
            Guid tenantId,
            Guid branchId,
            ServiceRequestResult request,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class Factory(
        ConcurrentQueue<(Guid TenantId, Guid BranchId, CustomerOrderResult Order)> captured)
        : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"mgmt-notify-{Guid.NewGuid():N}";

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
                services.AddSingleton<IManagementOrderNotifier>(
                    new CapturingManagementOrderNotifier(captured));
            });
        }
    }

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        public static readonly Guid RestaurantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
        public static readonly Guid BranchA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa3");
        public static readonly Guid TableA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa4");
        public static readonly Guid MenuA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa5");
        public static readonly Guid CategoryA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa6");
        public static readonly Guid ProductA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa7");
    }
}
