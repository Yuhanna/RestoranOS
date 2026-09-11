using Microsoft.EntityFrameworkCore;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;
using Xunit;

namespace RestaurantOS.Api.Tests;

public sealed class TableSessionSettlementMultiOrderTests
{
    [Fact]
    public async Task ShortenSessionSkipsWhileAnotherRoundIsActive()
    {
        await using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var restaurantId = Guid.NewGuid();
        var tableId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        db.Tenants.Add(new Tenant(tenantId, "Cafe"));
        db.Restaurants.Add(new Restaurant(restaurantId, tenantId, "Cafe"));
        db.Branches.Add(new Branch(branchId, tenantId, restaurantId, "Ana"));
        db.DiningTables.Add(new DiningTable(tableId, tenantId, branchId, "Masa 7"));
        var session = new CustomerSession(
            sessionId,
            tenantId,
            branchId,
            tableId,
            new string('a', 64),
            "tr",
            now,
            now.AddHours(16));
        db.CustomerSessions.Add(session);

        var first = new CustomerOrder(
            Guid.NewGuid(),
            tenantId,
            branchId,
            tableId,
            sessionId,
            "idem-1",
            new string('b', 64),
            "#1",
            Money.Try(10000),
            now,
            now.AddMinutes(20));
        first.ChangeStatus(OrderStatus.Completed, now);
        var second = new CustomerOrder(
            Guid.NewGuid(),
            tenantId,
            branchId,
            tableId,
            sessionId,
            "idem-2",
            new string('c', 64),
            "#2",
            Money.Try(3000),
            now.AddMinutes(5),
            now.AddMinutes(25));
        db.CustomerOrders.AddRange(first, second);
        await db.SaveChangesAsync();

        var originalExpiry = session.ExpiresAtUtc;
        await TableSessionSettlement.ShortenSessionAfterSettlementAsync(
            db,
            sessionId,
            now,
            CancellationToken.None,
            excludingOrderId: first.Id);

        Assert.Equal(originalExpiry, session.ExpiresAtUtc);

        second.ChangeStatus(OrderStatus.Completed, now.AddMinutes(6));
        await db.SaveChangesAsync();

        await TableSessionSettlement.ShortenSessionAfterSettlementAsync(
            db,
            sessionId,
            now.AddMinutes(6),
            CancellationToken.None,
            excludingOrderId: second.Id);

        Assert.True(session.ExpiresAtUtc < originalExpiry);
    }

    private static RestaurantOsDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<RestaurantOsDbContext>()
            .UseInMemoryDatabase($"settlement-{Guid.NewGuid():N}")
            .Options;
        return new RestaurantOsDbContext(options);
    }
}
