using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class CustomerMenuSettingsService(RestaurantOsDbContext dbContext) : ICustomerMenuSettingsService
{
    public async Task<CustomerMenuSettingsData> GetAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(
                entry => entry.Id == branchId && entry.TenantId == tenantId,
                cancellationToken);
        return branch is null
            ? MenuCatalogJson.DefaultSettings()
            : MenuCatalogJson.ParseSettings(branch.CustomerMenuSettingsJson);
    }

    public async Task<CustomerMenuSettingsData> UpdateAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CustomerMenuSettingsData settings,
        CancellationToken cancellationToken)
    {
        var allowed = await dbContext.ManagementMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.UserId == userId
                && membership.TenantId == tenantId
                && membership.BranchId == branchId
                && membership.IsActive
                && dbContext.ManagementRolePermissions.Any(rolePermission =>
                    rolePermission.RoleId == membership.RoleId
                    && rolePermission.Permission == ManagementPermissions.MenuEdit),
                cancellationToken);
        if (!allowed)
        {
            throw new ManagementAuthException("FORBIDDEN", "The requested operation is not permitted.");
        }

        var branch = await dbContext.Branches.SingleOrDefaultAsync(
            entry => entry.Id == branchId && entry.TenantId == tenantId,
            cancellationToken)
            ?? throw new CustomerExperienceException("BRANCH_NOT_FOUND", "Branch was not found.");

        var normalized = MenuCatalogJson.ParseSettings(MenuCatalogJson.SerializeSettings(settings));
        branch.SetCustomerMenuSettingsJson(MenuCatalogJson.SerializeSettings(normalized));
        await dbContext.SaveChangesAsync(cancellationToken);
        return normalized;
    }
}
