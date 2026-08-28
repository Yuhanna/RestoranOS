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
        MaxBranches: 1,
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
        MaxBranches: 1,
        MaxTablesPerBranch: null,
        MaxActiveUsers: 25,
        CanUseProductImages: true,
        CanUseMenuTranslations: true,
        CanManageAdditionalRoles: true,
        CanUseLiveOrderPanel: true,
        CanUseMultiBranch: false,
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
        DateTimeOffset? expiresAtUtc = null)
    {
        TenantId = tenantId;
        PlanCode = PlanCatalog.Normalize(planCode);
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc?.ToUniversalTime();
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

    public void DowngradeToFree(DateTimeOffset nowUtc) =>
        ChangePlan(SubscriptionPlanCodes.Free, nowUtc, expiresAtUtc: null);

    public bool IsExpired(DateTimeOffset nowUtc) =>
        ExpiresAtUtc is not null && ExpiresAtUtc.Value <= nowUtc.ToUniversalTime();
}
