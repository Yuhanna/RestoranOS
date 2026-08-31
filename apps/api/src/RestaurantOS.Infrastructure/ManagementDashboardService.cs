using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class ManagementDashboardService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider) : IManagementDashboardService
{
    public async Task<ManagementTodayDashboardResult> GetTodayAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var dayStart = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var todaysOrdersQuery = dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && order.BranchId == branchId
                && order.CreatedAtUtc >= dayStart
                && order.CreatedAtUtc < dayEnd);

        var todaysOrderCount = await todaysOrdersQuery.CountAsync(cancellationToken);

        var completedTodayQuery = dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && order.BranchId == branchId
                && order.Status == OrderStatus.Completed
                && order.StatusChangedAtUtc >= dayStart
                && order.StatusChangedAtUtc < dayEnd);

        var completedOrdersTodayCount = await completedTodayQuery.CountAsync(cancellationToken);
        var todaysRevenueMinor = await completedTodayQuery.SumAsync(
            order => (long?)order.TotalAmountMinor,
            cancellationToken) ?? 0;

        var currency = await completedTodayQuery
                .Select(order => order.TotalCurrency)
                .FirstOrDefaultAsync(cancellationToken)
            ?? await todaysOrdersQuery
                .Select(order => order.TotalCurrency)
                .FirstOrDefaultAsync(cancellationToken)
            ?? "TRY";

        var activeOrdersQuery = dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && order.BranchId == branchId
                && order.Status != OrderStatus.Completed
                && order.Status != OrderStatus.Cancelled);

        var pendingOrdersCount = await activeOrdersQuery.CountAsync(cancellationToken);
        var openTablesCount = await activeOrdersQuery
            .Select(order => order.TableId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new ManagementTodayDashboardResult(
            todaysOrderCount,
            todaysRevenueMinor,
            openTablesCount,
            pendingOrdersCount,
            completedOrdersTodayCount,
            currency,
            dayStart,
            dayEnd);
    }
}
