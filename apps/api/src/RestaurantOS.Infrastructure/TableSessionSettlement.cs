using Microsoft.EntityFrameworkCore;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

internal static class TableSessionSettlement
{
    public static async Task SupersedeActiveTableSessionsAsync(
        RestaurantOsDbContext dbContext,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = await dbContext.CustomerSessions
            .Where(session =>
                session.TenantId == tenantId
                && session.BranchId == branchId
                && session.TableId == tableId
                && session.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (var session in activeSessions)
        {
            session.ShortenExpiry(now);
        }
    }

    public static async Task ReleaseTableOperationalStateAsync(
        RestaurantOsDbContext dbContext,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await SupersedeActiveTableSessionsAsync(
            dbContext,
            tenantId,
            branchId,
            tableId,
            now,
            cancellationToken);

        var openRequests = await dbContext.ServiceRequests
            .Where(request =>
                request.TenantId == tenantId
                && request.BranchId == branchId
                && request.TableId == tableId
                && request.Status == ServiceRequestStatus.Open)
            .ToListAsync(cancellationToken);

        foreach (var request in openRequests)
        {
            request.Complete(now);
        }
    }

    public static async Task CompleteOpenBillRequestsAsync(
        RestaurantOsDbContext dbContext,
        Guid customerSessionId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken)
    {
        var openBillRequests = await dbContext.ServiceRequests
            .Where(request =>
                request.CustomerSessionId == customerSessionId
                && request.Type == ServiceRequestType.Bill
                && request.Status == ServiceRequestStatus.Open)
            .ToListAsync(cancellationToken);

        foreach (var request in openBillRequests)
        {
            request.Complete(completedAtUtc);
        }
    }

    public static async Task ShortenSessionAfterSettlementAsync(
        RestaurantOsDbContext dbContext,
        Guid customerSessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? excludingOrderId = null)
    {
        var hasOtherActiveRound = await dbContext.CustomerOrders.AnyAsync(
            order =>
                order.CustomerSessionId == customerSessionId
                && (excludingOrderId == null || order.Id != excludingOrderId)
                && order.Status != OrderStatus.Completed
                && order.Status != OrderStatus.Cancelled,
            cancellationToken);
        if (hasOtherActiveRound)
        {
            return;
        }

        var session = await dbContext.CustomerSessions
            .SingleOrDefaultAsync(entry => entry.Id == customerSessionId, cancellationToken);
        if (session is null)
        {
            return;
        }

        session.ShortenExpiry(now.Add(TableOccupancyPolicy.PostBillInactivityGrace));
    }
}
