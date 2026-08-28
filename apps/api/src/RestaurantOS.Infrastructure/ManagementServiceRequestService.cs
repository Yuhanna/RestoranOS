using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class ManagementServiceRequestService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider) : IManagementServiceRequestService
{
    public async Task<IReadOnlyList<ServiceRequestResult>> ListOpenAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.OrderView, cancellationToken);

        var rows = await (
            from request in dbContext.ServiceRequests.AsNoTracking()
            join table in dbContext.DiningTables.AsNoTracking()
                on new { request.TenantId, request.BranchId, request.TableId }
                equals new { table.TenantId, table.BranchId, TableId = table.Id }
            where request.TenantId == tenantId
                && request.BranchId == branchId
                && request.Status == ServiceRequestStatus.Open
            orderby request.CreatedAtUtc
            select new ServiceRequestResult(
                request.Id,
                request.TableId,
                table.Label,
                ServiceRequest.ToApiCode(request.Type),
                ServiceRequest.ToApiStatus(request.Status),
                request.Note,
                request.CreatedAtUtc,
                request.CompletedAtUtc)).ToListAsync(cancellationToken);

        return rows;
    }

    public async Task<ServiceRequestResult> CompleteAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.OrderModify, cancellationToken);

        var request = await dbContext.ServiceRequests.SingleOrDefaultAsync(
            x => x.Id == requestId && x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("SERVICE_REQUEST_NOT_FOUND", "Service request was not found.");

        try
        {
            request.Complete(timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            throw new CustomerExperienceException("SERVICE_REQUEST_CLOSED", exception.Message);
        }

        var tableLabel = await dbContext.DiningTables
            .AsNoTracking()
            .Where(x => x.Id == request.TableId && x.TenantId == tenantId && x.BranchId == branchId)
            .Select(x => x.Label)
            .SingleAsync(cancellationToken);

        return new ServiceRequestResult(
            request.Id,
            request.TableId,
            tableLabel,
            ServiceRequest.ToApiCode(request.Type),
            ServiceRequest.ToApiStatus(request.Status),
            request.Note,
            request.CreatedAtUtc,
            request.CompletedAtUtc);
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
            .AnyAsync(
                membership => membership.UserId == userId
                    && membership.TenantId == tenantId
                    && membership.BranchId == branchId
                    && membership.IsActive
                    && dbContext.ManagementRolePermissions.Any(rolePermission =>
                        rolePermission.RoleId == membership.RoleId
                        && rolePermission.Permission == permission),
                cancellationToken);
        if (!allowed)
        {
            throw new ManagementAuthException("FORBIDDEN", "Permission is required.");
        }
    }
}
