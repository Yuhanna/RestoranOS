namespace RestaurantOS.Domain;

public static class SubscriptionPlanCodes
{
    public const string Free = "Free";
    public const string Pro = "Pro";
    public const string Enterprise = "Enterprise";
}

public static class SubscriptionTrials
{
    public static readonly TimeSpan ProTrialDuration = TimeSpan.FromDays(30);
}

/// <summary>
/// Commercial branch-seat policy. Pro is a feature pack; seats beyond the included
/// count are paid add-ons. Trial unlocks multi-branch temporarily with a hard cap.
/// </summary>
public static class BranchBillingPolicy
{
    public const int FreeMaxBranches = 1;
    public const int TrialMaxBranches = 3;
    public const int ProIncludedBranches = 2;
    public const long ExtraBranchMonthlyPriceMinor = 149900; // ₺1.499,00 illustrative list price
    public const string Currency = "TRY";

    public static int ResolveIncludedBranches(string planCode, bool isTrialActive) =>
        PlanCatalog.Normalize(planCode) switch
        {
            SubscriptionPlanCodes.Enterprise => int.MaxValue,
            SubscriptionPlanCodes.Pro when isTrialActive => TrialMaxBranches,
            SubscriptionPlanCodes.Pro => ProIncludedBranches,
            _ => FreeMaxBranches,
        };

    public static int? ResolveMaxBranches(
        string planCode,
        bool isTrialActive,
        int purchasedBranchAddonCount)
    {
        var normalized = PlanCatalog.Normalize(planCode);
        if (normalized == SubscriptionPlanCodes.Enterprise)
        {
            return null;
        }

        if (normalized == SubscriptionPlanCodes.Pro && isTrialActive)
        {
            return TrialMaxBranches;
        }

        if (normalized == SubscriptionPlanCodes.Pro)
        {
            var addons = Math.Max(0, purchasedBranchAddonCount);
            return ProIncludedBranches + addons;
        }

        return FreeMaxBranches;
    }

    public static bool ResolveCanUseMultiBranch(string planCode, bool isTrialActive)
    {
        var normalized = PlanCatalog.Normalize(planCode);
        return normalized is SubscriptionPlanCodes.Pro or SubscriptionPlanCodes.Enterprise
            || isTrialActive;
    }
}

/// <summary>
/// Commercial entitlement snapshot for a tenant. Limits use null = unlimited.
/// Payment provider is not required; plan changes can be applied manually/ops.
/// </summary>
public sealed record FeatureEntitlements(
    string PlanCode,
    string DisplayName,
    int? MaxBranches,
    int? MaxTablesPerBranch,
    int? MaxActiveUsers,
    bool CanUseProductImages,
    bool CanUseMenuTranslations,
    bool CanManageAdditionalRoles,
    bool CanUseLiveOrderPanel,
    bool CanUseMultiBranch,
    bool HasPrioritySupport)
{
    public bool IsUnlimitedTables => MaxTablesPerBranch is null;
    public bool IsUnlimitedBranches => MaxBranches is null;
    public bool IsUnlimitedUsers => MaxActiveUsers is null;
}

public static class PlanCatalog
{
    public static FeatureEntitlements Free { get; } = new(
        SubscriptionPlanCodes.Free,
        "Free",
        MaxBranches: BranchBillingPolicy.FreeMaxBranches,
        MaxTablesPerBranch: 8,
        MaxActiveUsers: 1,
        CanUseProductImages: true,
        CanUseMenuTranslations: false,
        CanManageAdditionalRoles: false,
        CanUseLiveOrderPanel: false,
        CanUseMultiBranch: false,
        HasPrioritySupport: false);

    public static FeatureEntitlements Pro { get; } = new(
        SubscriptionPlanCodes.Pro,
        "Pro",
        MaxBranches: BranchBillingPolicy.ProIncludedBranches,
        MaxTablesPerBranch: null,
        MaxActiveUsers: 25,
        CanUseProductImages: true,
        CanUseMenuTranslations: true,
        CanManageAdditionalRoles: true,
        CanUseLiveOrderPanel: true,
        CanUseMultiBranch: true,
        HasPrioritySupport: false);

    public static FeatureEntitlements Enterprise { get; } = new(
        SubscriptionPlanCodes.Enterprise,
        "Enterprise",
        MaxBranches: null,
        MaxTablesPerBranch: null,
        MaxActiveUsers: null,
        CanUseProductImages: true,
        CanUseMenuTranslations: true,
        CanManageAdditionalRoles: true,
        CanUseLiveOrderPanel: true,
        CanUseMultiBranch: true,
        HasPrioritySupport: true);

    public static FeatureEntitlements Resolve(string? planCode) =>
        Normalize(planCode) switch
        {
            SubscriptionPlanCodes.Pro => Pro,
            SubscriptionPlanCodes.Enterprise => Enterprise,
            _ => Free,
        };

    public static FeatureEntitlements ResolveEffective(
        TenantSubscription subscription,
        DateTimeOffset nowUtc)
    {
        var now = nowUtc.ToUniversalTime();
        var isTrial = subscription.IsTrialActive(now);
        var baseline = Resolve(subscription.PlanCode);
        var maxBranches = BranchBillingPolicy.ResolveMaxBranches(
            subscription.PlanCode,
            isTrial,
            subscription.PurchasedBranchAddonCount);
        var canMulti = BranchBillingPolicy.ResolveCanUseMultiBranch(subscription.PlanCode, isTrial);
        return baseline with
        {
            MaxBranches = maxBranches,
            CanUseMultiBranch = canMulti,
        };
    }

    public static string Normalize(string? planCode)
    {
        if (string.IsNullOrWhiteSpace(planCode))
        {
            return SubscriptionPlanCodes.Free;
        }

        var value = planCode.Trim();
        if (value.Equals(SubscriptionPlanCodes.Pro, StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionPlanCodes.Pro;
        }

        if (value.Equals(SubscriptionPlanCodes.Enterprise, StringComparison.OrdinalIgnoreCase))
        {
            return SubscriptionPlanCodes.Enterprise;
        }

        return SubscriptionPlanCodes.Free;
    }
}

public sealed class TenantSubscription
{
    private TenantSubscription() { }

    public TenantSubscription(
        Guid tenantId,
        string planCode,
        DateTimeOffset startedAtUtc,
        DateTimeOffset? expiresAtUtc = null,
        int purchasedBranchAddonCount = 0)
    {
        TenantId = tenantId;
        PlanCode = PlanCatalog.Normalize(planCode);
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc?.ToUniversalTime();
        PurchasedBranchAddonCount = Math.Max(0, purchasedBranchAddonCount);
    }

    public static TenantSubscription CreateProTrial(Guid tenantId, DateTimeOffset nowUtc)
    {
        var started = nowUtc.ToUniversalTime();
        return new TenantSubscription(
            tenantId,
            SubscriptionPlanCodes.Pro,
            started,
            started.Add(SubscriptionTrials.ProTrialDuration));
    }

    public Guid TenantId { get; private set; }
    public string PlanCode { get; private set; } = SubscriptionPlanCodes.Free;
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? ExpiresAtUtc { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public int PurchasedBranchAddonCount { get; private set; }

    public FeatureEntitlements Entitlements => PlanCatalog.Resolve(PlanCode);

    /// <summary>Pro (or paid plan) with a finite expiry is treated as an active trial.</summary>
    public bool IsTrialActive(DateTimeOffset nowUtc) =>
        PlanCode is SubscriptionPlanCodes.Pro or SubscriptionPlanCodes.Enterprise
        && ExpiresAtUtc is not null
        && ExpiresAtUtc.Value > nowUtc.ToUniversalTime();

    public void ChangePlan(string planCode, DateTimeOffset nowUtc, DateTimeOffset? expiresAtUtc = null)
    {
        PlanCode = PlanCatalog.Normalize(planCode);
        UpdatedAtUtc = nowUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc?.ToUniversalTime();
    }

    /// <summary>Convert trial or expired plan into ongoing paid Pro/Enterprise (no expiry).</summary>
    public void ConvertToPaid(string planCode, DateTimeOffset nowUtc) =>
        ChangePlan(planCode, nowUtc, expiresAtUtc: null);

    public void DowngradeToFree(DateTimeOffset nowUtc)
    {
        ChangePlan(SubscriptionPlanCodes.Free, nowUtc, expiresAtUtc: null);
        PurchasedBranchAddonCount = 0;
    }

    public void SetPurchasedBranchAddonCount(int count, DateTimeOffset nowUtc)
    {
        PurchasedBranchAddonCount = Math.Max(0, count);
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void PurchaseBranchAddon(DateTimeOffset nowUtc, int quantity = 1)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        PurchasedBranchAddonCount += quantity;
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public bool IsExpired(DateTimeOffset nowUtc) =>
        ExpiresAtUtc is not null && ExpiresAtUtc.Value <= nowUtc.ToUniversalTime();
}

public sealed class EnterpriseQuoteRequest
{
    private EnterpriseQuoteRequest() { }

    public EnterpriseQuoteRequest(
        Guid id,
        Guid tenantId,
        Guid requestedByUserId,
        string contactName,
        string email,
        string? phone,
        int estimatedBranchCount,
        string? note,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        RequestedByUserId = requestedByUserId;
        ContactName = Required(contactName, 120);
        Email = Required(email, 256);
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        if (Phone is { Length: > 40 })
        {
            throw new ArgumentException("Phone is too long.");
        }

        EstimatedBranchCount = estimatedBranchCount < 1
            ? throw new ArgumentException("Estimated branch count must be at least 1.", nameof(estimatedBranchCount))
            : estimatedBranchCount;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (Note is { Length: > 2000 })
        {
            throw new ArgumentException("Note is too long.");
        }

        Status = "Open";
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public string ContactName { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string? Phone { get; private set; }
    public int EstimatedBranchCount { get; private set; }
    public string? Note { get; private set; }
    public string Status { get; private set; } = "Open";
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private static string Required(string value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.");
        }

        var trimmed = value.Trim();
        return trimmed.Length > max ? throw new ArgumentException("Value is too long.") : trimmed;
    }
}
