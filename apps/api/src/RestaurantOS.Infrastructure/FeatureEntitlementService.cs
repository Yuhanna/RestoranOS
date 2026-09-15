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

    public async Task<TenantEntitlementUsageResult> ConvertToPaidPlanAsync(
        Guid tenantId,
        Guid branchId,
        string planCode,
        CancellationToken cancellationToken)
    {
        var normalized = PlanCatalog.Normalize(planCode);
        if (normalized == SubscriptionPlanCodes.Enterprise)
        {
            throw new EntitlementException(
                "ENTERPRISE_QUOTE_REQUIRED",
                "Enterprise self-serve satın alınamaz. Lütfen teklif formu ile satış ekibine ulaşın.");
        }

        if (normalized != SubscriptionPlanCodes.Pro)
        {
            throw new EntitlementException("VALIDATION_ERROR", "Only Pro can be purchased self-serve.");
        }

        var subscription = await GetOrCreateSubscriptionAsync(tenantId, cancellationToken);
        await ApplyExpiryIfNeededAsync(subscription, cancellationToken);
        var now = timeProvider.GetUtcNow();
        subscription.ConvertToPaid(normalized, now);
        await ReconcileBranchQuotaAsync(subscription, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetUsageAsync(tenantId, branchId, cancellationToken);
    }

    public async Task<EnterpriseQuoteRequestResult> RequestEnterpriseQuoteAsync(
        Guid tenantId,
        Guid userId,
        string contactName,
        string email,
        string? phone,
        int estimatedBranchCount,
        string? note,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = new EnterpriseQuoteRequest(
                Guid.NewGuid(),
                tenantId,
                userId,
                contactName,
                email,
                phone,
                estimatedBranchCount,
                note,
                timeProvider.GetUtcNow());
            dbContext.EnterpriseQuoteRequests.Add(request);
            dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
                Guid.NewGuid(),
                "EnterpriseQuoteRequested",
                true,
                timeProvider.GetUtcNow(),
                userId,
                tenantId,
                subjectId: request.Id,
                detail: $"{estimatedBranchCount} şube"));
            await dbContext.SaveChangesAsync(cancellationToken);
            return new EnterpriseQuoteRequestResult(
                request.Id,
                request.CreatedAtUtc,
                "Talebiniz alındı. Satış ekibi en kısa sürede sizinle iletişime geçecek.");
        }
        catch (ArgumentException exception)
        {
            throw new EntitlementException("VALIDATION_ERROR", exception.Message);
        }
    }

    public async Task<FeatureEntitlements> GetEntitlementsAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetOrCreateSubscriptionAsync(tenantId, cancellationToken);
        await ApplyExpiryIfNeededAsync(subscription, cancellationToken);
        return PlanCatalog.ResolveEffective(subscription, timeProvider.GetUtcNow());
    }

    public async Task<TenantEntitlementUsageResult> GetUsageAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var subscription = await GetOrCreateSubscriptionAsync(tenantId, cancellationToken);
        await ApplyExpiryIfNeededAsync(subscription, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var entitlements = PlanCatalog.ResolveEffective(subscription, now);
        var isTrial = subscription.IsTrialActive(now);
        var displayName = isTrial ? "Pro (deneme)" : entitlements.DisplayName;
        var included = BranchBillingPolicy.ResolveIncludedBranches(subscription.PlanCode, isTrial);
        if (included == int.MaxValue)
        {
            included = 0; // unlimited display
        }
        else if (!isTrial && entitlements.PlanCode == SubscriptionPlanCodes.Pro)
        {
            included = BranchBillingPolicy.ProIncludedBranches;
        }

        var branchCount = await dbContext.Branches.CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var frozenCount = await dbContext.Branches.CountAsync(
            x => x.TenantId == tenantId && x.IsFrozen,
            cancellationToken);
        var activeCount = branchCount - frozenCount;
        var tableCount = await dbContext.DiningTables.CountAsync(
            x => x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken);
        var userCount = await dbContext.ManagementMemberships
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Select(x => x.UserId)
            .Distinct()
            .CountAsync(cancellationToken);

        var notifications = await dbContext.TenantNotifications
            .AsNoTracking()
            .Where(x => (x.TenantId == null || x.TenantId == tenantId) && x.IsActive)
            .ToListAsync(cancellationToken);
        var visibleNotifications = notifications
            .Where(x => x.IsVisibleAt(now) && SubscriptionAudiences.Matches(x.Audience, subscription, now))
            .Select(x => new TenantAudienceNotificationResult(x.Id, x.Title, x.Body, x.ActionUrl))
            .ToArray();

        var offers = await dbContext.SubscriptionOffers
            .AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(cancellationToken);
        var visibleOffers = offers
            .Where(x => x.IsActiveAt(now) && SubscriptionAudiences.Matches(x.Audience, subscription, now))
            .Select(x => new SubscriptionOfferResult(
                x.Id,
                x.TargetPlanCode,
                x.DiscountPercent,
                x.DurationMonths,
                x.Title,
                x.Body))
            .ToArray();

        var nextRequiresAddon = RequiresAddonForNextBranch(entitlements, isTrial, activeCount);
        var extraBranchMonthly = await ResolveExtraBranchMonthlyAsync(cancellationToken);

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
            Warnings: BuildWarnings(entitlements, isTrial, frozenCount, activeCount),
            isTrial,
            isTrial ? subscription.ExpiresAtUtc : null,
            visibleNotifications,
            visibleOffers,
            IncludedBranches: included,
            PurchasedBranchAddonCount: subscription.PurchasedBranchAddonCount,
            FrozenBranchCount: frozenCount,
            ActiveBranchCount: activeCount,
            ExtraBranchMonthlyPriceMinor: extraBranchMonthly,
            BillingCurrency: BranchBillingPolicy.Currency,
            NextBranchRequiresAddon: nextRequiresAddon);
    }

    public async Task<ManagementBranchBillingPreviewResult> GetBranchBillingPreviewAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var usage = await GetUsageAsync(tenantId, Guid.Empty, cancellationToken);
        var subscription = await GetOrCreateSubscriptionAsync(tenantId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var isTrial = subscription.IsTrialActive(now);
        var entitlements = PlanCatalog.ResolveEffective(subscription, now);
        var extraBranchMonthly = await ResolveExtraBranchMonthlyAsync(cancellationToken);
        return new ManagementBranchBillingPreviewResult(
            usage.PlanCode,
            usage.PlanDisplayName,
            isTrial,
            usage.TrialEndsAtUtc,
            usage.IncludedBranches,
            usage.PurchasedBranchAddonCount,
            usage.MaxBranches,
            usage.ActiveBranchCount,
            usage.FrozenBranchCount,
            usage.NextBranchRequiresAddon,
            extraBranchMonthly,
            BranchBillingPolicy.Currency,
            entitlements.CanUseMultiBranch,
            usage.IsTrial
                ? $"Denemede en fazla {BranchBillingPolicy.TrialMaxBranches} şube açabilirsiniz. Süre bitince yalnızca {BranchBillingPolicy.FreeMaxBranches} şube aktif kalır."
                : usage.PlanCode == SubscriptionPlanCodes.Pro
                    ? $"Pro pakete {BranchBillingPolicy.ProIncludedBranches} şube dahildir. Ek şube {FormatMoney(extraBranchMonthly)} / ay."
                    : usage.PlanCode == SubscriptionPlanCodes.Enterprise
                        ? "Enterprise planda şube kotası sözleşmenize göredir."
                        : "Free planda yalnızca 1 şube vardır. Çok şube için Pro deneme veya Pro plana geçin.");
    }

    public async Task EnsureCanCreateBranchAsync(
        Guid tenantId,
        bool confirmAddonPurchase,
        CancellationToken cancellationToken)
    {
        var subscription = await GetOrCreateSubscriptionAsync(tenantId, cancellationToken);
        await ApplyExpiryIfNeededAsync(subscription, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var entitlements = PlanCatalog.ResolveEffective(subscription, now);
        var isTrial = subscription.IsTrialActive(now);

        if (!entitlements.CanUseMultiBranch && entitlements.MaxBranches is 1)
        {
            var count = await dbContext.Branches.CountAsync(x => x.TenantId == tenantId, cancellationToken);
            if (count >= 1)
            {
                throw new EntitlementException(
                    "ENTITLEMENT_BRANCH_LIMIT",
                    "Free planda yalnızca 1 şube vardır. Çok şube için Pro deneme veya Pro plana geçin.");
            }

            return;
        }

        if (entitlements.IsUnlimitedBranches)
        {
            return;
        }

        var activeCount = await dbContext.Branches.CountAsync(
            x => x.TenantId == tenantId && !x.IsFrozen,
            cancellationToken);

        if (activeCount < entitlements.MaxBranches)
        {
            return;
        }

        // At cap: paid Pro may buy an addon seat (not during trial / free).
        if (!isTrial
            && entitlements.PlanCode == SubscriptionPlanCodes.Pro
            && confirmAddonPurchase)
        {
            subscription.PurchaseBranchAddon(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (!isTrial
            && entitlements.PlanCode == SubscriptionPlanCodes.Pro
            && !confirmAddonPurchase)
        {
            throw new EntitlementException(
                "BRANCH_ADDON_REQUIRED",
                $"Pro pakete {BranchBillingPolicy.ProIncludedBranches} şube dahildir. Ek şube için {FormatMoney(await ResolveExtraBranchMonthlyAsync(cancellationToken))} / ay onaylayın.");
        }

        throw new EntitlementException(
            "ENTITLEMENT_BRANCH_LIMIT",
            $"Şube limiti aşıldı ({activeCount}/{entitlements.MaxBranches}).");
    }

    public Task EnsureCanCreateBranchAsync(Guid tenantId, CancellationToken cancellationToken) =>
        EnsureCanCreateBranchAsync(tenantId, confirmAddonPurchase: false, cancellationToken);

    public async Task EnsureBranchNotFrozenAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var frozen = await dbContext.Branches.AsNoTracking().AnyAsync(
            x => x.Id == branchId && x.TenantId == tenantId && x.IsFrozen,
            cancellationToken);
        if (frozen)
        {
            throw new EntitlementException(
                "BRANCH_FROZEN",
                "Bu şube dondurulmuş. Planınızı yükseltin veya ek şube koltuğu satın alın.");
        }
    }

    public async Task EnsureCanCreateTableAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await EnsureBranchNotFrozenAsync(tenantId, branchId, cancellationToken);
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
        await ReconcileBranchQuotaAsync(subscription, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ReconcileBranchQuotaAsync(
        TenantSubscription subscription,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var entitlements = PlanCatalog.ResolveEffective(subscription, now);
        var branches = await dbContext.Branches
            .Where(x => x.TenantId == subscription.TenantId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (entitlements.IsUnlimitedBranches)
        {
            foreach (var branch in branches.Where(x => x.IsFrozen))
            {
                branch.Unfreeze();
            }

            return;
        }

        var keep = entitlements.MaxBranches ?? BranchBillingPolicy.FreeMaxBranches;
        for (var i = 0; i < branches.Count; i++)
        {
            if (i < keep)
            {
                if (branches[i].IsFrozen)
                {
                    branches[i].Unfreeze();
                }
            }
            else if (!branches[i].IsFrozen)
            {
                branches[i].Freeze();
            }
        }
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

    private static bool RequiresAddonForNextBranch(
        FeatureEntitlements entitlements,
        bool isTrial,
        int activeCount)
    {
        if (entitlements.IsUnlimitedBranches || !entitlements.CanUseMultiBranch || isTrial)
        {
            return false;
        }

        if (entitlements.PlanCode != SubscriptionPlanCodes.Pro || entitlements.MaxBranches is null)
        {
            return false;
        }

        return activeCount >= entitlements.MaxBranches;
    }

    private static List<string> BuildWarnings(
        FeatureEntitlements entitlements,
        bool isTrial,
        int frozenCount,
        int activeCount)
    {
        var warnings = new List<string>();
        if (frozenCount > 0)
        {
            warnings.Add(
                $"{frozenCount} şube donduruldu. Operasyon için Pro’ya geçin veya ek şube koltuğu ekleyin (aktif: {activeCount}).");
        }

        if (isTrial && entitlements.MaxBranches is int trialMax)
        {
            warnings.Add(
                $"Deneme sürüyor: en fazla {trialMax} şube. Süre bitince fazla şubeler dondurulur.");
        }

        return warnings;
    }

    private async Task<long> ResolveExtraBranchMonthlyAsync(CancellationToken cancellationToken)
    {
        var published = await dbContext.PlanPrices
            .AsNoTracking()
            .Where(x =>
                x.ProductCode == CatalogProductCodes.ExtraBranch
                && x.Interval == BillingIntervals.Month
                && x.Currency == BranchBillingPolicy.Currency
                && x.Status == PlanPriceStatuses.Published)
            .OrderByDescending(x => x.PublishedAtUtc)
            .Select(x => (long?)x.AmountMinor)
            .FirstOrDefaultAsync(cancellationToken);
        return published ?? BranchBillingPolicy.ExtraBranchMonthlyPriceMinor;
    }

    private static string FormatMoney(long minor) =>
        $"{(minor / 100m):N2} {BranchBillingPolicy.Currency}";
}
