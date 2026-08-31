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

        foreach (var permission in new[]
                 {
                     ManagementPermissions.OrderView,
                     ManagementPermissions.OrderModify,
                     ManagementPermissions.TableView,
                     ManagementPermissions.TableEdit,
                     ManagementPermissions.MenuView,
                     ManagementPermissions.MenuEdit,
                     ManagementPermissions.MenuPublish,
                     ManagementPermissions.AnalyticsView,
                     ManagementPermissions.AnalyticsFinancialView,
                     ManagementPermissions.SubscriptionManage,
                 })
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
        await EnsurePlatformOperatorAsync(tenantId, branchId, cancellationToken);
    }

    private async Task EnsurePlatformOperatorAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var email = configuration["BootstrapPlatformAdmin:Email"] ?? "platform@local.test";
        var password = configuration["BootstrapPlatformAdmin:Password"]
            ?? configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new InvalidOperationException(
                "BootstrapPlatformAdmin:Password or BootstrapAdmin:Password is required for platform operator seed.");
        }

        var role = await dbContext.ManagementRoles
            .SingleOrDefaultAsync(x => x.Name == "PlatformOperator", cancellationToken);
        if (role is null)
        {
            role = new ManagementRole(Guid.NewGuid(), "PlatformOperator");
            dbContext.ManagementRoles.Add(role);
        }

        if (!await dbContext.ManagementRolePermissions.AnyAsync(
                x => x.RoleId == role.Id && x.Permission == ManagementPermissions.PlatformManage,
                cancellationToken))
        {
            dbContext.ManagementRolePermissions.Add(
                new ManagementRolePermissionGrant(role.Id, ManagementPermissions.PlatformManage));
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
