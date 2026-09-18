using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class CustomerMenuSettingsService(
    RestaurantOsDbContext dbContext,
    IFeatureEntitlementService entitlements) : ICustomerMenuSettingsService
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
        await EnsureMenuEditAsync(userId, tenantId, branchId, cancellationToken);
        var branch = await LoadBranchAsync(tenantId, branchId, cancellationToken);

        var normalized = MenuCatalogJson.ParseSettings(MenuCatalogJson.SerializeSettings(settings));
        if (CustomerMenuThemes.IsPremium(normalized.ThemeId))
        {
            await entitlements.EnsureCanUseMenuThemesAsync(tenantId, cancellationToken);
        }

        if (normalized.ShowBrandWatermark)
        {
            await entitlements.EnsureCanUseBrandWatermarkAsync(tenantId, cancellationToken);
        }

        branch.SetCustomerMenuSettingsJson(MenuCatalogJson.SerializeSettings(normalized));
        await dbContext.SaveChangesAsync(cancellationToken);
        return normalized;
    }

    public async Task<CustomerMenuSettingsData> SetLogoAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string? logoUrl,
        string? logoAlt,
        CancellationToken cancellationToken)
    {
        await EnsureMenuEditAsync(userId, tenantId, branchId, cancellationToken);
        var branch = await LoadBranchAsync(tenantId, branchId, cancellationToken);

        var current = MenuCatalogJson.ParseSettings(branch.CustomerMenuSettingsJson);
        var updated = MenuCatalogJson.ParseSettings(MenuCatalogJson.SerializeSettings(
            current with { LogoUrl = logoUrl, LogoAlt = logoAlt }));

        branch.SetCustomerMenuSettingsJson(MenuCatalogJson.SerializeSettings(updated));
        await dbContext.SaveChangesAsync(cancellationToken);
        return updated;
    }

    private async Task EnsureMenuEditAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
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
    }

    private async Task<Branch> LoadBranchAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken) =>
        await dbContext.Branches.SingleOrDefaultAsync(
            entry => entry.Id == branchId && entry.TenantId == tenantId,
            cancellationToken)
            ?? throw new CustomerExperienceException("BRANCH_NOT_FOUND", "Branch was not found.");
}
