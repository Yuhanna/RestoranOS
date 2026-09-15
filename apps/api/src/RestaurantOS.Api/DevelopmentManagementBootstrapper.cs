using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api;

public sealed class DevelopmentManagementBootstrapper(
    RestaurantOsDbContext dbContext,
    IPasswordHasher<ManagementUser> passwordHasher,
    IConfiguration configuration,
    IHostEnvironment environment,
    TimeProvider timeProvider)
{
    public static readonly Guid DemoTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DemoRestaurantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid DemoBranchId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            throw new InvalidOperationException("Management bootstrap is restricted to Development.");
        }

        var email = configuration["BootstrapAdmin:Email"] ?? "owner@local.test";
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Password user-secret is required and must be at least 12 characters.");
        }

        var tenantId = Guid.TryParse(configuration["BootstrapAdmin:TenantId"], out var parsedTenant)
            ? parsedTenant
            : DemoTenantId;
        var branchId = Guid.TryParse(configuration["BootstrapAdmin:BranchId"], out var parsedBranch)
            ? parsedBranch
            : DemoBranchId;
        var restaurantId = Guid.TryParse(configuration["BootstrapAdmin:RestaurantId"], out var parsedRestaurant)
            ? parsedRestaurant
            : DemoRestaurantId;

        await EnsureDemoScopeAsync(tenantId, restaurantId, branchId, cancellationToken);

        var role = await dbContext.ManagementRoles
            .SingleOrDefaultAsync(x => x.Name == "RestaurantOwner", cancellationToken);
        if (role is null)
        {
            role = new ManagementRole(Guid.NewGuid(), "RestaurantOwner");
            dbContext.ManagementRoles.Add(role);
        }

        foreach (var permission in ManagementAuthServicePermissions.Owner)
        {
            if (!await dbContext.ManagementRolePermissions
                    .AnyAsync(x => x.RoleId == role.Id && x.Permission == permission, cancellationToken))
            {
                dbContext.ManagementRolePermissions.Add(new ManagementRolePermissionGrant(role.Id, permission));
            }
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.ManagementUsers
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null)
        {
            user = new ManagementUser(
                Guid.NewGuid(),
                email.Trim(),
                normalizedEmail,
                "pending",
                timeProvider.GetUtcNow());
            user.UpdatePasswordHash(passwordHasher.HashPassword(user, password));
            dbContext.ManagementUsers.Add(user);
        }
        else
        {
            user.UpdatePasswordHash(passwordHasher.HashPassword(user, password));
        }

        var membershipExists = await dbContext.ManagementMemberships.AnyAsync(
            x => x.UserId == user.Id
                && x.TenantId == tenantId
                && x.BranchId == branchId
                && x.RoleId == role.Id,
            cancellationToken);
        if (!membershipExists)
        {
            dbContext.ManagementMemberships.Add(new ManagementMembership(
                Guid.NewGuid(),
                user.Id,
                tenantId,
                branchId,
                role.Id));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureAudienceContentAsync(cancellationToken);
        await EnsureDemoMenuPromotionAsync(tenantId, branchId, cancellationToken);
        await EnsureCustomerMenuPublishedAsync(tenantId, branchId, cancellationToken);
        await EnsurePlatformOperatorAsync(cancellationToken);
    }

    /// <summary>
    /// QR customer flow requires a published menu. In Development, publish an existing draft
    /// or seed a small demo menu so scanning a table QR works out of the box.
    /// </summary>
    private async Task EnsureCustomerMenuPublishedAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var hasPublished = await dbContext.Menus.AnyAsync(
            x => x.TenantId == tenantId
                && x.BranchId == branchId
                && x.PublishedAtUtc != null
                && x.ArchivedAtUtc == null,
            cancellationToken);
        if (hasPublished)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var drafts = await dbContext.Menus
            .Where(x => x.TenantId == tenantId
                && x.BranchId == branchId
                && x.PublishedAtUtc == null
                && x.ArchivedAtUtc == null)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var draft in drafts)
        {
            var categoryCount = await dbContext.MenuCategories.CountAsync(
                x => x.MenuId == draft.Id && x.TenantId == tenantId && x.BranchId == branchId,
                cancellationToken);
            var itemCount = await dbContext.MenuItems.CountAsync(
                x => x.MenuId == draft.Id && x.TenantId == tenantId && x.BranchId == branchId,
                cancellationToken);
            if (categoryCount > 0 && itemCount > 0)
            {
                draft.Publish(now);
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
        }

        const string demoMenuName = "Demo Menü";
        var demoMenuId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01");
        var demoCategoryId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0002");
        var demoItemId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddd0003");

        var demoMenu = await dbContext.Menus.SingleOrDefaultAsync(x => x.Id == demoMenuId, cancellationToken);
        if (demoMenu is null)
        {
            demoMenu = new PublishedMenu(demoMenuId, tenantId, branchId, demoMenuName);
            dbContext.Menus.Add(demoMenu);
            dbContext.MenuCategories.Add(new MenuCategory(
                demoCategoryId,
                tenantId,
                branchId,
                demoMenuId,
                "Ana yemekler",
                1));
            dbContext.MenuItems.Add(new MenuItem(
                demoItemId,
                tenantId,
                branchId,
                demoMenuId,
                demoCategoryId,
                "Izgara köfte",
                "Demo ürün — geliştirme ortamı",
                new Money(25000, "TRY"),
                isAvailable: true,
                sortOrder: 1));
        }

        if (demoMenu.PublishedAtUtc is null && !demoMenu.IsArchived)
        {
            demoMenu.Publish(now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePlatformOperatorAsync(CancellationToken cancellationToken)
    {
        var email = configuration["BootstrapPlatformAdmin:Email"] ?? "platform@local.test";
        var password = configuration["BootstrapPlatformAdmin:Password"]
            ?? configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException(
                "BootstrapPlatformAdmin:Password or BootstrapAdmin:Password is required for platform operator seed.");
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var user = await dbContext.ManagementUsers
            .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null)
        {
            user = new ManagementUser(
                Guid.NewGuid(),
                email.Trim(),
                normalizedEmail,
                "pending",
                timeProvider.GetUtcNow());
            user.UpdatePasswordHash(passwordHasher.HashPassword(user, password));
            dbContext.ManagementUsers.Add(user);
        }
        else
        {
            user.UpdatePasswordHash(passwordHasher.HashPassword(user, password));
        }

        var staff = await dbContext.PlatformStaff.SingleOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
        if (staff is null)
        {
            dbContext.PlatformStaff.Add(new PlatformStaff(user.Id, PlatformStaffRoles.Owner, timeProvider.GetUtcNow()));
        }
        else if (!staff.IsActive || staff.RoleCode != PlatformStaffRoles.Owner)
        {
            staff.Activate();
            staff.ChangeRole(PlatformStaffRoles.Owner);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await EnsureBillingCatalogAsync(user.Id, cancellationToken);
    }

    private async Task EnsureBillingCatalogAsync(Guid createdByUserId, CancellationToken cancellationToken)
    {
        if (await dbContext.PlanPrices.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        void AddPublished(string product, string interval, long amount)
        {
            var price = new PlanPrice(
                Guid.NewGuid(),
                product,
                interval,
                BranchBillingPolicy.Currency,
                amount,
                taxInclusive: true,
                now,
                createdByUserId);
            price.Publish(now);
            dbContext.PlanPrices.Add(price);
        }

        AddPublished(SubscriptionPlanCodes.Free, BillingIntervals.Month, 0);
        AddPublished(SubscriptionPlanCodes.Free, BillingIntervals.Year, 0);
        AddPublished(SubscriptionPlanCodes.Pro, BillingIntervals.Month, 2_499_00);
        AddPublished(SubscriptionPlanCodes.Pro, BillingIntervals.Year, 24_990_00);
        AddPublished(CatalogProductCodes.ExtraBranch, BillingIntervals.Month, BranchBillingPolicy.ExtraBranchMonthlyPriceMinor);
        AddPublished(
            CatalogProductCodes.ExtraBranch,
            BillingIntervals.Year,
            BranchBillingPolicy.ExtraBranchMonthlyPriceMinor * 10);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAudienceContentAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var offerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
        if (!await dbContext.SubscriptionOffers.AnyAsync(x => x.Id == offerId, cancellationToken))
        {
            dbContext.SubscriptionOffers.Add(new SubscriptionOffer(
                offerId,
                SubscriptionAudiences.NonPro,
                SubscriptionPlanCodes.Pro,
                20,
                3,
                "Pro'ya geçişte %20 indirim",
                "Pro olmayan üyeler için 3 ay boyunca %20 indirimli Pro abonelik.",
                now.AddDays(-1),
                now.AddMonths(6),
                isActive: true));
        }

        var notificationId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbb1");
        if (!await dbContext.TenantNotifications.AnyAsync(x => x.Id == notificationId, cancellationToken))
        {
            dbContext.TenantNotifications.Add(new TenantNotification(
                notificationId,
                tenantId: null,
                SubscriptionAudiences.NonPro,
                "Pro'ya yükselt, 3 ay %20 indirim kazan",
                "Ücretsiz planda sınırlı özelliklerle devam ediyorsunuz. Pro'ya geçerek canlı sipariş paneli, çoklu şube ve gelişmiş analitiği 3 ay %20 indirimle kullanabilirsiniz.",
                now.AddDays(-1),
                now.AddMonths(6),
                "/subscription",
                isActive: true));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDemoMenuPromotionAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var promotionId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");
        if (await dbContext.MenuPromotions.AnyAsync(x => x.Id == promotionId, cancellationToken))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        dbContext.MenuPromotions.Add(new MenuPromotion(
            promotionId,
            tenantId,
            branchId,
            "Öğle menüsü %20",
            PromotionScopes.AllMenu,
            DiscountKinds.Percent,
            20,
            now.AddDays(-1),
            endsAtUtc: null,
            dailyStartLocal: new TimeOnly(11, 0),
            dailyEndLocal: new TimeOnly(14, 0),
            categoryId: null,
            menuItemId: null,
            isActive: true));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDemoScopeAsync(
        Guid tenantId,
        Guid restaurantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken))
        {
            dbContext.Tenants.Add(new Tenant(tenantId, "Demo Tenant"));
        }

        if (!await dbContext.Restaurants.AnyAsync(x => x.Id == restaurantId, cancellationToken))
        {
            dbContext.Restaurants.Add(new Restaurant(restaurantId, tenantId, "Demo Restaurant"));
        }

        if (!await dbContext.Branches.AnyAsync(x => x.Id == branchId && x.TenantId == tenantId, cancellationToken))
        {
            dbContext.Branches.Add(new Branch(branchId, tenantId, restaurantId, "Demo Branch"));
        }

        if (!await dbContext.TenantSubscriptions.AnyAsync(x => x.TenantId == tenantId, cancellationToken))
        {
            dbContext.TenantSubscriptions.Add(
                new TenantSubscription(tenantId, SubscriptionPlanCodes.Free, timeProvider.GetUtcNow()));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
