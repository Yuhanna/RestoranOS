using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class FeatureEntitlementService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider) : IFeatureEntitlementService
{
    public async Task EnsureTenantSubscriptionAsync(
        Guid tenantId,
        string planCode,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TenantSubscriptions
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var normalized = PlanCatalog.Normalize(planCode);
        dbContext.TenantSubscriptions.Add(
            normalized == SubscriptionPlanCodes.Pro
                ? TenantSubscription.CreateProTrial(tenantId, now)
                : new TenantSubscription(tenantId, normalized, now));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<FeatureEntitlements> GetEntitlementsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetOrCreateSubscriptionAsync(tenantId, cancellationToken);
        await ApplyExpiryIfNeededAsync(subscription, cancellationToken);
        return subscription.Entitlements;
    }

    public async Task<TenantEntitlementUsageResult> GetUsageAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetOrCreateSubscriptionAsync(tenantId, cancellationToken);
        await ApplyExpiryIfNeededAsync(subscription, cancellationToken);
        var entitlements = subscription.Entitlements;
        var now = timeProvider.GetUtcNow();
        var isTrial = subscription.IsTrialActive(now);
        var displayName = isTrial ? "Pro (deneme)" : entitlements.DisplayName;

        var branchCount = await dbContext.Branches.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var tableCount = await dbContext.DiningTables.CountAsync(
            x => x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken);
        var userCount = await dbContext.ManagementMemberships
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Do not push plan/limit nags onto every screen. Limits are enforced at the
        // action (create table/user/branch, translation upsert) with a clear message.
        return new TenantEntitlementUsageResult(
            entitlements.PlanCode,
            displayName,
            branchCount,
            entitlements.MaxBranches,
            tableCount,
            entitlements.MaxTablesPerBranch,
            userCount,
            entitlements.MaxActiveUsers,
            entitlements.CanUseProductImages,
            entitlements.CanUseMenuTranslations,
            entitlements.CanManageAdditionalRoles,
            entitlements.CanUseLiveOrderPanel,
            entitlements.CanUseMultiBranch,
            entitlements.HasPrioritySupport,
            Warnings: [],
            isTrial,
            isTrial ? subscription.ExpiresAtUtc : null);
    }

    public async Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var entitlements = await GetEntitlementsAsync(tenantId, cancellationToken);
        if (entitlements.IsUnlimitedBranches)
        {
            return;
        }

        if (!entitlements.CanUseMultiBranch && entitlements.MaxBranches is 1)
        {
            var count = await dbContext.Branches.CountAsync(x => x.TenantId == tenantId, cancellationToken);
            if (count >= 1)
            {
                throw new EntitlementException(
                    "ENTITLEMENT_BRANCH_LIMIT",
                    "Free/Pro planında yalnızca 1 şube vardır. Çok şube için Enterprise gerekir.");
            }

            return;
        }

        var branchCount = await dbContext.Branches.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        if (branchCount >= entitlements.MaxBranches)
        {
            throw new EntitlementException(
                "ENTITLEMENT_BRANCH_LIMIT",
                $"Şube limiti aşıldı ({branchCount}/{entitlements.MaxBranches}).");
        }
    }

    public async Task EnsureCanCreateTableAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var entitlements = await GetEntitlementsAsync(tenantId, cancellationToken);
        if (entitlements.IsUnlimitedTables)
        {
            return;
        }

        var count = await dbContext.DiningTables.CountAsync(
            x => x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken);
        if (count >= entitlements.MaxTablesPerBranch)
        {
            throw new EntitlementException(
                "ENTITLEMENT_TABLE_LIMIT",
                $"Free planda en fazla {entitlements.MaxTablesPerBranch} masa eklenebilir. Pro plana geçerek limiti kaldırabilirsiniz.");
        }
    }

    public async Task EnsureCanAddUserAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var entitlements = await GetEntitlementsAsync(tenantId, cancellationToken);
        if (entitlements.IsUnlimitedUsers)
        {
            return;
        }

        if (!entitlements.CanManageAdditionalRoles && entitlements.MaxActiveUsers is 1)
        {
            var count = await dbContext.ManagementMemberships
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.IsActive)
                .Select(x => x.UserId)
                .Distinct()
                .CountAsync(cancellationToken);
            if (count >= 1)
            {
                throw new EntitlementException(
                    "ENTITLEMENT_USER_LIMIT",
                    "Free planda yalnızca 1 kullanıcı vardır. Ek roller için Pro gerekir.");
            }

            return;
        }

        var userCount = await dbContext.ManagementMemberships
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
        if (userCount >= entitlements.MaxActiveUsers)
        {
            throw new EntitlementException(
                "ENTITLEMENT_USER_LIMIT",
                $"Kullanıcı limiti aşıldı ({userCount}/{entitlements.MaxActiveUsers}).");
        }
    }

    public async Task EnsureCanUseProductImagesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var entitlements = await GetEntitlementsAsync(tenantId, cancellationToken);
        if (!entitlements.CanUseProductImages)
        {
            throw new EntitlementException(
                "ENTITLEMENT_FEATURE_IMAGES",
                "Bu planda ürün fotoğrafı kapalıdır. Planınızı yükseltmeniz gerekir.");
        }
    }

    public async Task EnsureCanUseMenuTranslationsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var entitlements = await GetEntitlementsAsync(tenantId, cancellationToken);
        if (!entitlements.CanUseMenuTranslations)
        {
            throw new EntitlementException(
                "ENTITLEMENT_FEATURE_TRANSLATIONS",
                "Menü çevirileri Pro veya Enterprise planda açılır. İngilizce vb. eklemek için planınızı yükseltin.");
        }
    }

    public async Task EnsureCanManageAdditionalRolesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var entitlements = await GetEntitlementsAsync(tenantId, cancellationToken);
        if (!entitlements.CanManageAdditionalRoles)
        {
            throw new EntitlementException(
                "ENTITLEMENT_FEATURE_ROLES",
                "Ek kullanıcı ve roller Pro veya Enterprise planda açılır.");
        }
    }

    private async Task ApplyExpiryIfNeededAsync(
        TenantSubscription subscription,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (!subscription.IsExpired(now))
        {
            return;
        }

        if (subscription.PlanCode == SubscriptionPlanCodes.Free && subscription.ExpiresAtUtc is null)
        {
            return;
        }

        subscription.DowngradeToFree(now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<TenantSubscription> GetOrCreateSubscriptionAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TenantSubscriptions
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = TenantSubscription.CreateProTrial(tenantId, timeProvider.GetUtcNow());
        dbContext.TenantSubscriptions.Add(created);
        await dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }
}
