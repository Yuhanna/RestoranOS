using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class ManagementMenuService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider,
    IFeatureEntitlementService entitlements) : IManagementMenuService
{
    public async Task<IReadOnlyList<ManagementMenuSummaryResult>> ListMenusAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuView, cancellationToken);
        var menus = await dbContext.Menus
            .AsNoTracking()
            .Where(menu => menu.TenantId == tenantId && menu.BranchId == branchId)
            .OrderByDescending(menu => menu.PublishedAtUtc)
            .ThenBy(menu => menu.Name)
            .Select(menu => new ManagementMenuSummaryResult(
                menu.Id,
                menu.Name,
                menu.ArchivedAtUtc != null ? "archived" : menu.PublishedAtUtc != null ? "published" : "draft",
                menu.PublishedAtUtc,
                dbContext.MenuCategories.Count(category =>
                    category.MenuId == menu.Id && category.TenantId == tenantId && category.BranchId == branchId),
                dbContext.MenuItems.Count(item =>
                    item.MenuId == menu.Id && item.TenantId == tenantId && item.BranchId == branchId)))
            .ToListAsync(cancellationToken);
        return menus;
    }

    public async Task<ManagementMenuDetailResult> GetMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuView, cancellationToken);
        var menu = await FindMenuAsync(tenantId, branchId, menuId, cancellationToken, tracking: false);
        var categories = await dbContext.MenuCategories
            .AsNoTracking()
            .Where(category => category.MenuId == menu.Id && category.TenantId == tenantId && category.BranchId == branchId)
            .OrderBy(category => category.SortOrder)
            .ToListAsync(cancellationToken);
        var items = await dbContext.MenuItems
            .AsNoTracking()
            .Where(item => item.MenuId == menu.Id && item.TenantId == tenantId && item.BranchId == branchId)
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        var categoryIds = categories.Select(category => category.Id).ToArray();
        var itemIds = items.Select(item => item.Id).ToArray();
        var categoryTranslations = await dbContext.MenuCategoryTranslations
            .AsNoTracking()
            .Where(translation =>
                translation.TenantId == tenantId
                && translation.BranchId == branchId
                && categoryIds.Contains(translation.CategoryId))
            .ToListAsync(cancellationToken);
        var itemTranslations = await dbContext.MenuItemTranslations
            .AsNoTracking()
            .Where(translation =>
                translation.TenantId == tenantId
                && translation.BranchId == branchId
                && itemIds.Contains(translation.ItemId))
            .ToListAsync(cancellationToken);
        return new ManagementMenuDetailResult(
            menu.Id,
            menu.Name,
            menu.Lifecycle,
            menu.PublishedAtUtc,
            categories.Select(category => MapCategory(
                category,
                categoryTranslations.Where(translation => translation.CategoryId == category.Id))).ToArray(),
            items.Select(item => MapItem(
                item,
                itemTranslations.Where(translation => translation.ItemId == item.Id))).ToArray());
    }

    public async Task<ManagementMenuSummaryResult> CreateMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string name,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuEdit, cancellationToken);
        PublishedMenu menu;
        try
        {
            menu = new PublishedMenu(Guid.NewGuid(), tenantId, branchId, name);
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "A menu name is required.");
        }

        dbContext.Menus.Add(menu);
        await AuditAsync(userId, tenantId, branchId, "MenuCreated", menu.Id, menu.Name, cancellationToken);
        return ToSummary(menu, 0, 0);
    }

    public async Task<ManagementMenuSummaryResult> RenameMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        string name,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuEdit, cancellationToken);
        var menu = await FindMenuAsync(tenantId, branchId, menuId, cancellationToken);
        try
        {
            menu.Rename(name);
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "A menu name is required.");
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        await AuditAsync(userId, tenantId, branchId, "MenuUpdated", menu.Id, menu.Name, cancellationToken);
        return await ToSummaryAsync(menu, cancellationToken);
    }

    public async Task<ManagementMenuSummaryResult> PublishMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuPublish, cancellationToken);
        var menu = await FindMenuAsync(tenantId, branchId, menuId, cancellationToken);
        var availableCount = await dbContext.MenuItems.CountAsync(
            item => item.MenuId == menu.Id
                && item.TenantId == tenantId
                && item.BranchId == branchId
                && item.IsAvailable,
            cancellationToken);
        if (availableCount == 0)
        {
            throw new CustomerExperienceException("MENU_EMPTY", "A menu needs at least one available product before publish.");
        }

        try
        {
            var others = await dbContext.Menus
                .Where(existing =>
                    existing.Id != menu.Id
                    && existing.TenantId == tenantId
                    && existing.BranchId == branchId
                    && existing.PublishedAtUtc != null
                    && existing.ArchivedAtUtc == null)
                .ToListAsync(cancellationToken);
            foreach (var other in others)
            {
                other.Unpublish();
            }

            menu.Publish(timeProvider.GetUtcNow());
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        await AuditAsync(userId, tenantId, branchId, "MenuPublished", menu.Id, menu.Name, cancellationToken);
        return await ToSummaryAsync(menu, cancellationToken);
    }

    public async Task<ManagementMenuSummaryResult> UnpublishMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuPublish, cancellationToken);
        var menu = await FindMenuAsync(tenantId, branchId, menuId, cancellationToken);
        try
        {
            menu.Unpublish();
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        await AuditAsync(userId, tenantId, branchId, "MenuUnpublished", menu.Id, menu.Name, cancellationToken);
        return await ToSummaryAsync(menu, cancellationToken);
    }

    public async Task<ManagementMenuSummaryResult> ArchiveMenuAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuPublish, cancellationToken);
        var menu = await FindMenuAsync(tenantId, branchId, menuId, cancellationToken);
        menu.Archive(timeProvider.GetUtcNow());
        await AuditAsync(userId, tenantId, branchId, "MenuArchived", menu.Id, menu.Name, cancellationToken);
        return await ToSummaryAsync(menu, cancellationToken);
    }

    public async Task<ManagementMenuCategoryResult> AddCategoryAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        string name,
        int sortOrder,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuEdit, cancellationToken);
        var menu = await FindMenuAsync(tenantId, branchId, menuId, cancellationToken);
        MenuCategory category;
        try
        {
            menu.EnsureEditable();
            category = new MenuCategory(Guid.NewGuid(), tenantId, branchId, menu.Id, name, sortOrder);
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "A category name is required.");
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        dbContext.MenuCategories.Add(category);
        var categoryTranslation = new MenuCategoryTranslation(
            category.Id,
            tenantId,
            branchId,
            SupportedLocales.Turkish,
            category.Name);
        dbContext.MenuCategoryTranslations.Add(categoryTranslation);
        await AuditAsync(userId, tenantId, branchId, "MenuCategoryCreated", category.Id, category.Name, cancellationToken);
        return MapCategory(category, [categoryTranslation]);
    }

    public async Task<ManagementMenuCategoryResult> UpdateCategoryAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid categoryId,
        string? name,
        int? sortOrder,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuEdit, cancellationToken);
        var category = await dbContext.MenuCategories.SingleOrDefaultAsync(
            existing => existing.Id == categoryId && existing.TenantId == tenantId && existing.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("CATEGORY_NOT_FOUND", "Category was not found.");
        var menu = await FindMenuAsync(tenantId, branchId, category.MenuId, cancellationToken);
        try
        {
            menu.EnsureEditable();
            if (!string.IsNullOrWhiteSpace(name))
            {
                category.Rename(name);
            }

            if (sortOrder is not null)
            {
                category.SetSortOrder(sortOrder.Value);
            }
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "A category name is required.");
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        await AuditAsync(userId, tenantId, branchId, "MenuCategoryUpdated", category.Id, category.Name, cancellationToken);
        var translations = await dbContext.MenuCategoryTranslations
            .AsNoTracking()
            .Where(translation => translation.CategoryId == category.Id)
            .ToListAsync(cancellationToken);
        return MapCategory(category, translations);
    }

    public async Task<ManagementMenuItemResult> AddItemAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        Guid categoryId,
        string name,
        string description,
        long amountMinor,
        bool isAvailable,
        int sortOrder,
        string? imageUrl,
        string? imageAlt,
        CancellationToken cancellationToken,
        int? prepTimeSeconds = null,
        MenuItemCatalogData? catalog = null)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuEdit, cancellationToken);
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            await entitlements.EnsureCanUseProductImagesAsync(tenantId, cancellationToken);
        }

        var menu = await FindMenuAsync(tenantId, branchId, menuId, cancellationToken);
        try
        {
            menu.EnsureEditable();
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        await EnsureCategoryAsync(tenantId, branchId, menu.Id, categoryId, cancellationToken);
        MenuItem item;
        try
        {
            item = new MenuItem(
                Guid.NewGuid(),
                tenantId,
                branchId,
                menu.Id,
                categoryId,
                name,
                description ?? string.Empty,
                Money.Try(amountMinor),
                isAvailable,
                sortOrder,
                imageUrl,
                imageAlt,
                prepTimeSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Prep time must be between 1 second and 24 hours.");
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Product name, price, and image fields are invalid.");
        }

        if (catalog is not null)
        {
            item.SetCatalogJson(MenuCatalogJson.SerializeItem(catalog));
        }

        dbContext.MenuItems.Add(item);
        var itemTranslation = new MenuItemTranslation(
            item.Id,
            tenantId,
            branchId,
            SupportedLocales.Turkish,
            item.Name,
            item.Description);
        dbContext.MenuItemTranslations.Add(itemTranslation);
        await AuditAsync(userId, tenantId, branchId, "MenuItemCreated", item.Id, item.Name, cancellationToken);
        return MapItem(item, [itemTranslation]);
    }

    public async Task<ManagementMenuItemResult> UpdateItemAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid itemId,
        Guid? categoryId,
        string? name,
        string? description,
        long? amountMinor,
        bool? isAvailable,
        int? sortOrder,
        string? imageUrl,
        string? imageAlt,
        CancellationToken cancellationToken,
        int? prepTimeSeconds = null,
        bool updatePrepTime = false,
        MenuItemCatalogData? catalog = null,
        bool updateCatalog = false)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuEdit, cancellationToken);
        if (imageUrl is not null && !string.IsNullOrWhiteSpace(imageUrl))
        {
            await entitlements.EnsureCanUseProductImagesAsync(tenantId, cancellationToken);
        }

        var item = await dbContext.MenuItems.SingleOrDefaultAsync(
            existing => existing.Id == itemId && existing.TenantId == tenantId && existing.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("PRODUCT_NOT_FOUND", "Product was not found.");
        var menu = await FindMenuAsync(tenantId, branchId, item.MenuId, cancellationToken);
        try
        {
            menu.EnsureEditable();
            var nextCategory = categoryId ?? item.CategoryId;
            await EnsureCategoryAsync(tenantId, branchId, menu.Id, nextCategory, cancellationToken);
            item.Update(
                name ?? item.Name,
                description ?? item.Description,
                amountMinor is null ? item.Price : Money.Try(amountMinor.Value),
                isAvailable ?? item.IsAvailable,
                sortOrder ?? item.SortOrder,
                nextCategory,
                imageUrl,
                imageAlt,
                prepTimeSeconds,
                updatePrepTime);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Product name, price, and prep time are invalid.");
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Product name, price, and image fields are invalid.");
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        if (updateCatalog)
        {
            item.SetCatalogJson(catalog is null ? null : MenuCatalogJson.SerializeItem(catalog));
        }

        await AuditAsync(userId, tenantId, branchId, "MenuItemUpdated", item.Id, item.Name, cancellationToken);
        var translations = await dbContext.MenuItemTranslations
            .AsNoTracking()
            .Where(translation => translation.ItemId == item.Id)
            .ToListAsync(cancellationToken);
        return MapItem(item, translations);
    }

    public async Task<ManagementMenuItemResult> SetItemImageAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid itemId,
        string imageUrl,
        string? imageAlt,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuEdit, cancellationToken);
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            await entitlements.EnsureCanUseProductImagesAsync(tenantId, cancellationToken);
        }

        var item = await dbContext.MenuItems.SingleOrDefaultAsync(
            existing => existing.Id == itemId && existing.TenantId == tenantId && existing.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("PRODUCT_NOT_FOUND", "Product was not found.");
        var menu = await FindMenuAsync(tenantId, branchId, item.MenuId, cancellationToken);
        try
        {
            menu.EnsureEditable();
            item.SetImage(imageUrl, imageAlt ?? item.ImageAlt);
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Image URL is invalid.");
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        await AuditAsync(userId, tenantId, branchId, "MenuItemImageUpdated", item.Id, item.ImageUrl, cancellationToken);
        var translations = await dbContext.MenuItemTranslations
            .AsNoTracking()
            .Where(translation => translation.ItemId == item.Id)
            .ToListAsync(cancellationToken);
        return MapItem(item, translations);
    }

    public async Task<MenuTextTranslationResult> UpsertCategoryTranslationAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid categoryId,
        string locale,
        string name,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuEdit, cancellationToken);
        if (!SupportedLocales.IsSupported(locale))
        {
            throw new CustomerExperienceException("UNSUPPORTED_LOCALE", "Only tr and en translations are supported.");
        }

        var normalized = SupportedLocales.Normalize(locale);
        if (!string.Equals(normalized, SupportedLocales.Turkish, StringComparison.OrdinalIgnoreCase))
        {
            await entitlements.EnsureCanUseMenuTranslationsAsync(tenantId, cancellationToken);
        }

        var category = await dbContext.MenuCategories.SingleOrDefaultAsync(
            existing => existing.Id == categoryId && existing.TenantId == tenantId && existing.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("CATEGORY_NOT_FOUND", "Category was not found.");
        var menu = await FindMenuAsync(tenantId, branchId, category.MenuId, cancellationToken);
        try
        {
            menu.EnsureEditable();
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        var translation = await dbContext.MenuCategoryTranslations.SingleOrDefaultAsync(
            existing => existing.CategoryId == category.Id && existing.Locale == normalized,
            cancellationToken);
        try
        {
            if (translation is null)
            {
                translation = new MenuCategoryTranslation(category.Id, tenantId, branchId, normalized, name);
                dbContext.MenuCategoryTranslations.Add(translation);
            }
            else
            {
                translation.SetName(name);
            }
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "A translated name is required.");
        }

        await AuditAsync(userId, tenantId, branchId, "MenuCategoryTranslated", category.Id, normalized, cancellationToken);
        return new MenuTextTranslationResult(translation.Locale, translation.Name, string.Empty);
    }

    public async Task<MenuTextTranslationResult> UpsertItemTranslationAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid itemId,
        string locale,
        string name,
        string description,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.MenuEdit, cancellationToken);
        if (!SupportedLocales.IsSupported(locale))
        {
            throw new CustomerExperienceException("UNSUPPORTED_LOCALE", "Only tr and en translations are supported.");
        }

        var normalized = SupportedLocales.Normalize(locale);
        if (!string.Equals(normalized, SupportedLocales.Turkish, StringComparison.OrdinalIgnoreCase))
        {
            await entitlements.EnsureCanUseMenuTranslationsAsync(tenantId, cancellationToken);
        }

        var item = await dbContext.MenuItems.SingleOrDefaultAsync(
            existing => existing.Id == itemId && existing.TenantId == tenantId && existing.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("PRODUCT_NOT_FOUND", "Product was not found.");
        var menu = await FindMenuAsync(tenantId, branchId, item.MenuId, cancellationToken);
        try
        {
            menu.EnsureEditable();
        }
        catch (InvalidMenuStateException exception)
        {
            throw new CustomerExperienceException("MENU_ARCHIVED", exception.Message);
        }

        var translation = await dbContext.MenuItemTranslations.SingleOrDefaultAsync(
            existing => existing.ItemId == item.Id && existing.Locale == normalized,
            cancellationToken);
        try
        {
            if (translation is null)
            {
                translation = new MenuItemTranslation(item.Id, tenantId, branchId, normalized, name, description);
                dbContext.MenuItemTranslations.Add(translation);
            }
            else
            {
                translation.SetText(name, description);
            }
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "A translated name is required.");
        }

        await AuditAsync(userId, tenantId, branchId, "MenuItemTranslated", item.Id, normalized, cancellationToken);
        return new MenuTextTranslationResult(translation.Locale, translation.Name, translation.Description);
    }

    private async Task<PublishedMenu> FindMenuAsync(
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        CancellationToken cancellationToken,
        bool tracking = true)
    {
        var query = tracking ? dbContext.Menus.AsQueryable() : dbContext.Menus.AsNoTracking();
        return await query.SingleOrDefaultAsync(
            menu => menu.Id == menuId && menu.TenantId == tenantId && menu.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("MENU_NOT_FOUND", "Menu was not found.");
    }

    private async Task EnsureCategoryAsync(
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.MenuCategories.AnyAsync(
            category => category.Id == categoryId
                && category.MenuId == menuId
                && category.TenantId == tenantId
                && category.BranchId == branchId,
            cancellationToken);
        if (!exists)
        {
            throw new CustomerExperienceException("CATEGORY_NOT_FOUND", "Category was not found.");
        }
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

    private async Task AuditAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string action,
        Guid subjectId,
        string detail,
        CancellationToken cancellationToken)
    {
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            action,
            true,
            timeProvider.GetUtcNow(),
            userId,
            tenantId,
            branchId,
            subjectId,
            detail));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<ManagementMenuSummaryResult> ToSummaryAsync(
        PublishedMenu menu,
        CancellationToken cancellationToken)
    {
        var categoryCount = await dbContext.MenuCategories.CountAsync(
            category => category.MenuId == menu.Id,
            cancellationToken);
        var itemCount = await dbContext.MenuItems.CountAsync(
            item => item.MenuId == menu.Id,
            cancellationToken);
        return ToSummary(menu, categoryCount, itemCount);
    }

    private static ManagementMenuSummaryResult ToSummary(PublishedMenu menu, int categoryCount, int itemCount) =>
        new(menu.Id, menu.Name, menu.Lifecycle, menu.PublishedAtUtc, categoryCount, itemCount);

    private static ManagementMenuCategoryResult MapCategory(
        MenuCategory category,
        IEnumerable<MenuCategoryTranslation> translations) =>
        new(
            category.Id,
            category.MenuId,
            category.Name,
            category.SortOrder,
            translations.Select(translation => new MenuTextTranslationResult(
                translation.Locale,
                translation.Name,
                string.Empty)).ToArray());

    private static ManagementMenuItemResult MapItem(
        MenuItem item,
        IEnumerable<MenuItemTranslation> translations) =>
        new(
            item.Id,
            item.MenuId,
            item.CategoryId,
            item.Name,
            item.Description,
            item.PriceAmountMinor,
            item.PriceCurrency,
            item.IsAvailable,
            item.SortOrder,
            item.ImageUrl,
            item.ImageAlt,
            translations.Select(translation => new MenuTextTranslationResult(
                translation.Locale,
                translation.Name,
                translation.Description)).ToArray(),
            item.PrepTimeSeconds,
            MenuCatalogJson.ParseItem(item.CatalogJson));
}
