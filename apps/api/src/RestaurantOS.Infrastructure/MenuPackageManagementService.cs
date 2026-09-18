using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class MenuPackageManagementService(RestaurantOsDbContext dbContext) : IMenuPackageManagementService
{
    public async Task<IReadOnlyList<MenuPackageResult>> ListAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var packages = await dbContext.MenuPackages
            .AsNoTracking()
            .Include(x => x.Components)
            .ThenInclude(x => x.MenuItem)
            .Where(x => x.TenantId == tenantId && x.BranchId == branchId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return packages.Select(Map).ToArray();
    }

    public async Task<MenuPackageResult> CreateAsync(
        Guid tenantId,
        Guid branchId,
        UpsertMenuPackageCommand command,
        CancellationToken cancellationToken)
    {
        var package = BuildEntity(Guid.NewGuid(), tenantId, branchId, command);
        await AttachValidatedComponentsAsync(package, tenantId, branchId, command.Components, cancellationToken);
        dbContext.MenuPackages.Add(package);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(tenantId, branchId, package.Id, cancellationToken);
    }

    public async Task<MenuPackageResult> UpdateAsync(
        Guid tenantId,
        Guid branchId,
        Guid packageId,
        UpsertMenuPackageCommand command,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.MenuPackages
            .Include(x => x.Components)
            .SingleOrDefaultAsync(
                x => x.Id == packageId && x.TenantId == tenantId && x.BranchId == branchId,
                cancellationToken)
            ?? throw new CustomerExperienceException("PACKAGE_NOT_FOUND", "Lunch package was not found.");

        try
        {
            var currency = string.IsNullOrWhiteSpace(command.Currency)
                ? "TRY"
                : command.Currency.Trim().ToUpperInvariant();
            package.Update(
                command.Name,
                command.Description,
                new Money(command.PriceAmountMinor, currency),
                command.IsActive,
                command.SortOrder,
                command.DailyStartLocal,
                command.DailyEndLocal,
                command.DaysOfWeekMask);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.MenuPackageComponents.RemoveRange(package.Components);
        package.Components.Clear();
        await AttachValidatedComponentsAsync(package, tenantId, branchId, command.Components, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ReloadAsync(tenantId, branchId, package.Id, cancellationToken);
    }

    public async Task<MenuPackageResult> SetActiveAsync(
        Guid tenantId,
        Guid branchId,
        Guid packageId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.MenuPackages
            .Include(x => x.Components)
            .ThenInclude(x => x.MenuItem)
            .SingleOrDefaultAsync(
                x => x.Id == packageId && x.TenantId == tenantId && x.BranchId == branchId,
                cancellationToken)
            ?? throw new CustomerExperienceException("PACKAGE_NOT_FOUND", "Lunch package was not found.");
        package.SetActive(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(package);
    }

    public async Task DeleteAsync(
        Guid tenantId,
        Guid branchId,
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.MenuPackages
            .Include(x => x.Components)
            .SingleOrDefaultAsync(
                x => x.Id == packageId && x.TenantId == tenantId && x.BranchId == branchId,
                cancellationToken)
            ?? throw new CustomerExperienceException("PACKAGE_NOT_FOUND", "Lunch package was not found.");
        dbContext.MenuPackages.Remove(package);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AttachValidatedComponentsAsync(
        MenuPackage package,
        Guid tenantId,
        Guid branchId,
        IReadOnlyList<MenuPackageComponentInput> components,
        CancellationToken cancellationToken)
    {
        if (components.Count is < 2 or > 12)
        {
            throw new CustomerExperienceException(
                "VALIDATION_ERROR",
                "A lunch package needs between 2 and 12 menu items.");
        }

        var itemIds = components.Select(x => x.MenuItemId).Distinct().ToArray();
        if (itemIds.Length != components.Count)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Duplicate package components are not allowed.");
        }

        var items = await dbContext.MenuItems
            .AsNoTracking()
            .Where(x => itemIds.Contains(x.Id) && x.TenantId == tenantId && x.BranchId == branchId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (items.Count != itemIds.Length)
        {
            throw new CustomerExperienceException(
                "VALIDATION_ERROR",
                "One or more package products were not found on this branch.");
        }

        try
        {
            package.ReplaceComponents(
                components
                    .OrderBy(x => x.SortOrder)
                    .Select((component, index) => new MenuPackageComponent(
                        package.Id,
                        component.MenuItemId,
                        component.SlotLabel,
                        component.SortOrder == 0 ? index : component.SortOrder)));
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }
    }

    private static MenuPackage BuildEntity(
        Guid id,
        Guid tenantId,
        Guid branchId,
        UpsertMenuPackageCommand command)
    {
        try
        {
            var currency = string.IsNullOrWhiteSpace(command.Currency)
                ? "TRY"
                : command.Currency.Trim().ToUpperInvariant();
            return new MenuPackage(
                id,
                tenantId,
                branchId,
                command.Name,
                command.Description,
                new Money(command.PriceAmountMinor, currency),
                command.IsActive,
                command.SortOrder,
                command.DailyStartLocal,
                command.DailyEndLocal,
                command.DaysOfWeekMask);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }
    }

    private async Task<MenuPackageResult> ReloadAsync(
        Guid tenantId,
        Guid branchId,
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var package = await dbContext.MenuPackages
            .AsNoTracking()
            .Include(x => x.Components)
            .ThenInclude(x => x.MenuItem)
            .SingleAsync(
                x => x.Id == packageId && x.TenantId == tenantId && x.BranchId == branchId,
                cancellationToken);
        return Map(package);
    }

    private static MenuPackageResult Map(MenuPackage package)
    {
        var components = package.Components
            .OrderBy(x => x.SortOrder)
            .Select(x => new MenuPackageComponentResult(
                x.MenuItemId,
                x.MenuItemId,
                x.MenuItem?.Name ?? string.Empty,
                x.SlotLabel,
                x.SortOrder,
                x.MenuItem?.PriceAmountMinor ?? 0))
            .ToArray();
        return new MenuPackageResult(
            package.Id,
            package.Name,
            package.Description,
            package.PriceAmountMinor,
            package.PriceCurrency,
            package.IsActive,
            package.SortOrder,
            package.DailyStartLocal,
            package.DailyEndLocal,
            package.DaysOfWeekMask,
            components.Sum(x => x.ListAmountMinor),
            components);
    }
}
