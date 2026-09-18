using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class PromotionManagementService(
    RestaurantOsDbContext dbContext,
    IFeatureEntitlementService entitlements) : IPromotionManagementService
{
    public async Task<IReadOnlyList<MenuPromotionResult>> ListMenuPromotionsAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        return await dbContext.MenuPromotions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.BranchId == branchId)
            .OrderByDescending(x => x.StartsAtUtc)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<MenuPromotionResult> CreateMenuPromotionAsync(
        Guid tenantId,
        Guid branchId,
        CreateMenuPromotionCommand command,
        CancellationToken cancellationToken)
    {
        await entitlements.EnsureCanUsePromotionsAsync(tenantId, cancellationToken);
        if (command.Scope == PromotionScopes.Category && command.CategoryId is null)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Category promotions require a category id.");
        }

        if (command.Scope == PromotionScopes.Product && command.MenuItemId is null)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Product promotions require a menu item id.");
        }

        if (command.CategoryId is not null)
        {
            var categoryOk = await dbContext.MenuCategories.AnyAsync(
                x => x.Id == command.CategoryId && x.TenantId == tenantId && x.BranchId == branchId,
                cancellationToken);
            if (!categoryOk)
            {
                throw new CustomerExperienceException("VALIDATION_ERROR", "Category was not found.");
            }
        }

        if (command.MenuItemId is not null)
        {
            var itemOk = await dbContext.MenuItems.AnyAsync(
                x => x.Id == command.MenuItemId && x.TenantId == tenantId && x.BranchId == branchId,
                cancellationToken);
            if (!itemOk)
            {
                throw new CustomerExperienceException("VALIDATION_ERROR", "Menu item was not found.");
            }
        }

        MenuPromotion entity;
        try
        {
            entity = new MenuPromotion(
                Guid.NewGuid(),
                tenantId,
                branchId,
                command.Name,
                command.Scope,
                command.DiscountKind,
                command.DiscountValue,
                command.StartsAtUtc,
                command.EndsAtUtc,
                command.DailyStartLocal,
                command.DailyEndLocal,
                command.CategoryId,
                command.MenuItemId,
                command.IsActive,
                command.DaysOfWeekMask);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.MenuPromotions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<MenuPromotionResult> UpdateMenuPromotionAsync(
        Guid tenantId,
        Guid branchId,
        Guid promotionId,
        UpdateMenuPromotionCommand command,
        CancellationToken cancellationToken)
    {
        await entitlements.EnsureCanUsePromotionsAsync(tenantId, cancellationToken);
        var entity = await dbContext.MenuPromotions.SingleOrDefaultAsync(
            x => x.Id == promotionId && x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("PROMOTION_NOT_FOUND", "Promotion was not found.");

        if (command.IsActive is not null)
        {
            entity.SetActive(command.IsActive.Value);
        }

        if (command.Name is not null)
        {
            entity.Rename(command.Name);
        }

        if (command.EndsAtUtc is not null)
        {
            entity.SetEndsAt(command.EndsAtUtc);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    private static MenuPromotionResult Map(MenuPromotion promotion) =>
        new(
            promotion.Id,
            promotion.Name,
            promotion.Scope,
            promotion.DiscountKind,
            promotion.DiscountValue,
            promotion.StartsAtUtc,
            promotion.EndsAtUtc,
            promotion.DailyStartLocal,
            promotion.DailyEndLocal,
            promotion.CategoryId,
            promotion.MenuItemId,
            promotion.IsActive,
            promotion.DaysOfWeekMask);
}
