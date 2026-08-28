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

        var orders = await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && order.BranchId == branchId
                && order.Status != OrderStatus.Completed
                && order.Status != OrderStatus.Cancelled)
            .OrderBy(order => order.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return orders.Select(order => new ManagementOrderResult(
                order.Id,
                order.DisplayNumber,
                order.Status.ToString().ToLowerInvariant(),
                order.CreatedAtUtc,
                order.StatusChangedAtUtc,
                order.EstimatedReadyAtUtc,
                order.TotalAmountMinor,
                order.TotalCurrency))
            .ToArray();
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
