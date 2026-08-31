using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class ManagementOrderService(
    RestaurantOsDbContext dbContext,
    ICustomerExperienceService customerExperienceService,
    IManagementOrderNotifier notifier) : IManagementOrderService
{
    public async Task<IReadOnlyList<ManagementOrderResult>> GetActiveOrdersAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            userId,
            tenantId,
            branchId,
            ManagementPermissions.OrderView,
            cancellationToken);

        var orders = await (
            from order in dbContext.CustomerOrders.AsNoTracking()
            join table in dbContext.DiningTables.AsNoTracking() on order.TableId equals table.Id
            where order.TenantId == tenantId
                && order.BranchId == branchId
                && order.Status != OrderStatus.Completed
                && order.Status != OrderStatus.Cancelled
            orderby order.CreatedAtUtc
            select new { order, table.Label })
            .ToListAsync(cancellationToken);
        return orders.Select(entry => new ManagementOrderResult(
                entry.order.Id,
                entry.order.DisplayNumber,
                entry.order.Status.ToString().ToLowerInvariant(),
                entry.order.CreatedAtUtc,
                entry.order.StatusChangedAtUtc,
                entry.order.EstimatedReadyAtUtc,
                entry.order.TotalAmountMinor,
                entry.order.TotalCurrency,
                entry.order.TableId,
                entry.Label))
            .ToArray();
    }

    public async Task<ManagementOrderDetailResult?> GetOrderByIdAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(
            userId,
            tenantId,
            branchId,
            ManagementPermissions.OrderView,
            cancellationToken);

        var order = await dbContext.CustomerOrders
            .AsNoTracking()
            .Include(o => o.Items)
            .SingleOrDefaultAsync(
                o => o.Id == orderId && o.TenantId == tenantId && o.BranchId == branchId,
                cancellationToken);
        if (order is null)
        {
            return null;
        }

        var tableLabel = await dbContext.DiningTables
            .AsNoTracking()
            .Where(table => table.Id == order.TableId)
            .Select(table => table.Label)
            .SingleOrDefaultAsync(cancellationToken);

        var auditEntries = await dbContext.ManagementAuditLogs
            .AsNoTracking()
            .Where(log =>
                log.SubjectId == orderId
                && log.Action == "OrderStatusChange"
                && log.Succeeded)
            .OrderBy(log => log.OccurredAtUtc)
            .Select(log => new ManagementOrderStatusHistoryEntry(
                log.Detail!,
                log.OccurredAtUtc,
                log.UserId))
            .ToListAsync(cancellationToken);

        var statusHistory = new List<ManagementOrderStatusHistoryEntry>
        {
            new("submitted", order.CreatedAtUtc, null),
        };
        statusHistory.AddRange(auditEntries);

        var items = order.Items
            .OrderBy(item => item.Name)
            .Select(item => new ManagementOrderLineResult(
                item.Id,
                item.MenuItemId,
                item.Name,
                item.Quantity,
                item.ListUnitPriceAmountMinor,
                item.DiscountUnitAmountMinor,
                item.UnitPriceAmountMinor,
                item.UnitPriceCurrency,
                item.Note))
            .ToArray();

        return new ManagementOrderDetailResult(
            order.Id,
            order.DisplayNumber,
            order.Status.ToString().ToLowerInvariant(),
            order.TableId,
            tableLabel ?? "—",
            order.CreatedAtUtc,
            order.StatusChangedAtUtc,
            order.EstimatedReadyAtUtc,
            order.SubtotalAmountMinor,
            order.DiscountAmountMinor,
            order.TotalAmountMinor,
            order.TotalCurrency,
            items,
            statusHistory);
    }

    public async Task<CustomerOrderResult> ChangeOrderStatusAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        string status,
        DateTimeOffset expectedStatusChangedAtUtc,
        CancellationToken cancellationToken,
        DateTimeOffset? estimatedReadyAtUtc = null)
    {
        await EnsurePermissionAsync(
            userId,
            tenantId,
            branchId,
            ManagementPermissions.OrderModify,
            cancellationToken);

        var result = await customerExperienceService.ChangeOrderStatusAsync(
            tenantId,
            branchId,
            orderId,
            status,
            expectedStatusChangedAtUtc,
            userId,
            cancellationToken,
            estimatedReadyAtUtc);
        await notifier.NotifyAsync(tenantId, branchId, result, cancellationToken);
        return result;
    }

    private async Task EnsurePermissionAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string permission,
        CancellationToken cancellationToken)
    {
        var allowed = await dbContext.ManagementMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.UserId == userId
                && membership.TenantId == tenantId
                && membership.IsActive
                && membership.BranchId == branchId
                && dbContext.Branches.Any(branch =>
                    branch.Id == branchId && branch.TenantId == tenantId)
                && dbContext.ManagementRolePermissions.Any(rolePermission =>
                    rolePermission.RoleId == membership.RoleId
                    && rolePermission.Permission == permission),
                cancellationToken);
        if (!allowed)
        {
            throw new ManagementAuthException("FORBIDDEN", "The requested operation is not permitted.");
        }
    }
}
