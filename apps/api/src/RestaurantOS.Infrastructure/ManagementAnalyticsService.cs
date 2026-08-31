using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class ManagementAnalyticsService(RestaurantOsDbContext dbContext) : IManagementAnalyticsService
{
    public async Task<ManagementAnalyticsSummaryResult> GetSummaryAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var (from, to) = NormalizeRange(fromUtc, toUtc);
        var completed = await QueryCompletedOrders(tenantId, branchId, from, to)
            .Select(order => new
            {
                order.TotalAmountMinor,
                order.TotalCurrency,
            })
            .ToListAsync(cancellationToken);

        var cancelled = await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && order.BranchId == branchId
                && order.Status == OrderStatus.Cancelled
                && order.CreatedAtUtc >= from
                && order.CreatedAtUtc < to)
            .Select(order => new { order.TotalAmountMinor, order.TotalCurrency })
            .ToListAsync(cancellationToken);

        var costMinor = await EstimateCostMinorAsync(tenantId, branchId, from, to, cancellationToken);
        var grossSales = completed.Sum(order => order.TotalAmountMinor);
        var cancelledSales = cancelled.Sum(order => order.TotalAmountMinor);
        var currency = completed.FirstOrDefault()?.TotalCurrency
            ?? cancelled.FirstOrDefault()?.TotalCurrency
            ?? "TRY";
        var completedCount = completed.Count;
        var averageTicket = completedCount == 0 ? 0 : grossSales / completedCount;

        var openServiceRequests = await dbContext.ServiceRequests
            .AsNoTracking()
            .CountAsync(
                request => request.TenantId == tenantId
                    && request.BranchId == branchId
                    && request.Status == ServiceRequestStatus.Open,
                cancellationToken);

        return new ManagementAnalyticsSummaryResult(
            grossSales,
            costMinor,
            grossSales - costMinor,
            cancelledSales,
            completedCount,
            cancelled.Count,
            openServiceRequests,
            averageTicket,
            currency);
    }

    public async Task<IReadOnlyList<ManagementSalesPeriodResult>> GetSalesByPeriodAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string granularity,
        CancellationToken cancellationToken)
    {
        var (from, to) = NormalizeRange(fromUtc, toUtc);
        var orders = await QueryCompletedOrders(tenantId, branchId, from, to)
            .Select(order => new { order.CreatedAtUtc, order.TotalAmountMinor, order.Id })
            .ToListAsync(cancellationToken);

        var orderIds = orders.Select(order => order.Id).ToHashSet();
        var lineCosts = await (
            from item in dbContext.CustomerOrderItems.AsNoTracking()
            join menu in dbContext.MenuItems.AsNoTracking() on item.MenuItemId equals menu.Id into menuJoin
            from menu in menuJoin.DefaultIfEmpty()
            where orderIds.Contains(item.OrderId)
            select new
            {
                item.OrderId,
                CostMinor = (long)item.Quantity * (menu != null ? menu.CostAmountMinor ?? 0 : 0),
            }).ToListAsync(cancellationToken);

        var costByOrder = lineCosts
            .GroupBy(line => line.OrderId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.CostMinor));

        var useHour = string.Equals(granularity, "hour", StringComparison.OrdinalIgnoreCase);
        var buckets = orders
            .GroupBy(order => BucketStart(order.CreatedAtUtc, useHour))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var sales = group.Sum(order => order.TotalAmountMinor);
                var cost = group.Sum(order => costByOrder.GetValueOrDefault(order.Id));
                return new ManagementSalesPeriodResult(group.Key, sales, sales - cost, group.Count());
            })
            .ToArray();

        return buckets;
    }

    public async Task<IReadOnlyList<ManagementTopItemResult>> GetTopItemsAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int limit,
        CancellationToken cancellationToken)
    {
        var (from, to) = NormalizeRange(fromUtc, toUtc);
        var cappedLimit = Math.Clamp(limit, 1, 100);
        var completedOrderIds = QueryCompletedOrders(tenantId, branchId, from, to).Select(order => order.Id);

        var rows = await (
            from item in dbContext.CustomerOrderItems.AsNoTracking()
            join menu in dbContext.MenuItems.AsNoTracking() on item.MenuItemId equals menu.Id into menuJoin
            from menu in menuJoin.DefaultIfEmpty()
            where completedOrderIds.Contains(item.OrderId)
            group new { item, menu } by new { item.MenuItemId, item.Name } into grouped
            select new
            {
                grouped.Key.MenuItemId,
                grouped.Key.Name,
                QuantitySold = grouped.Sum(x => x.item.Quantity),
                RevenueMinor = grouped.Sum(x => (long)x.item.Quantity * x.item.UnitPriceAmountMinor),
                CostMinor = grouped.Sum(x => (long)x.item.Quantity * (x.menu != null ? x.menu.CostAmountMinor ?? 0 : 0)),
            })
            .OrderByDescending(row => row.RevenueMinor)
            .Take(cappedLimit)
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ManagementTopItemResult(
                row.MenuItemId,
                row.Name,
                row.QuantitySold,
                row.RevenueMinor,
                row.CostMinor,
                row.RevenueMinor - row.CostMinor))
            .ToArray();
    }

    private IQueryable<CustomerOrder> QueryCompletedOrders(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset from,
        DateTimeOffset to) =>
        dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && order.BranchId == branchId
                && order.Status == OrderStatus.Completed
                && order.CreatedAtUtc >= from
                && order.CreatedAtUtc < to);

    private async Task<long> EstimateCostMinorAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var completedOrderIds = QueryCompletedOrders(tenantId, branchId, from, to).Select(order => order.Id);
        return await (
            from item in dbContext.CustomerOrderItems.AsNoTracking()
            join menu in dbContext.MenuItems.AsNoTracking() on item.MenuItemId equals menu.Id into menuJoin
            from menu in menuJoin.DefaultIfEmpty()
            where completedOrderIds.Contains(item.OrderId)
            select (long)item.Quantity * (menu != null ? menu.CostAmountMinor ?? 0 : 0))
            .SumAsync(cancellationToken);
    }

    private static (DateTimeOffset From, DateTimeOffset To) NormalizeRange(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc)
    {
        var from = fromUtc.ToUniversalTime();
        var to = toUtc.ToUniversalTime();
        if (to <= from)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "The analytics date range is invalid.");
        }

        if ((to - from).TotalDays > 366)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Analytics range cannot exceed 366 days.");
        }

        return (from, to);
    }

    private static DateTimeOffset BucketStart(DateTimeOffset instant, bool hourly)
    {
        var utc = instant.ToUniversalTime();
        if (hourly)
        {
            return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, 0, 0, TimeSpan.Zero);
        }

        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }
}
