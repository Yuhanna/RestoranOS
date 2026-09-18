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
/// Free-tier capacity for order-taking surfaces. Tables may be created freely;
/// monetization gates on concurrently active QR codes and live-panel sessions.
/// </summary>
public static class FreeTierCapacity
{
    public const int MaxActiveQrCodesPerBranch = 8;
    public const int MaxActiveUsers = 2;
    public const int MaxConcurrentLiveSessions = 1;
}

/// <summary>
/// Operational order-history lookback by plan. History itself is always available;
/// longer windows are the paid differentiator. Enterprise contracts may raise the
/// included 90-day ceiling later without changing Free/Pro defaults.
/// </summary>
public static class OrderHistoryRetention
{
    public const int FreeMaxHours = 72; // 24–72 saat operasyon penceresi
    public const int ProMaxHours = 24 * 30; // 7–30 gün
    public const int EnterpriseIncludedMaxHours = 24 * 90; // 90 gün dahil

    public static int ResolveMaxHours(
        string? planCode,
        bool isTrialActive = false,
        int? overrideMaxHours = null)
    {
        if (overrideMaxHours is > 0)
        {
            return overrideMaxHours.Value;
        }

        var normalized = PlanCatalog.Normalize(planCode);
        if (normalized == SubscriptionPlanCodes.Enterprise)
        {
            return EnterpriseIncludedMaxHours;
        }

        if (normalized == SubscriptionPlanCodes.Pro || isTrialActive)
        {
            return ProMaxHours;
        }

        return FreeMaxHours;
    }

    public static int ClampRequestedHours(int requestedHours, int maxHours)
    {
        var safeMax = Math.Max(1, maxHours);
        return Math.Clamp(requestedHours, 1, safeMax);
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
    bool HasPrioritySupport,
    bool CanUseMenuThemes = false,
    bool CanUseBrandWatermark = false,
    int? MaxActiveQrCodes = null,
    int? MaxConcurrentLiveSessions = null,
    bool CanUsePromotions = false,
    bool CanUseAnalytics = false)
{
    public bool IsUnlimitedTables => MaxTablesPerBranch is null;
    public bool IsUnlimitedActiveQrCodes => MaxActiveQrCodes is null;
    public bool IsUnlimitedBranches => MaxBranches is null;
    public bool IsUnlimitedUsers => MaxActiveUsers is null;
    public bool IsUnlimitedLiveSessions => MaxConcurrentLiveSessions is null;
}

public static class PlanCatalog
{
    public static FeatureEntitlements Free { get; } = new(
        SubscriptionPlanCodes.Free,
        "Free",
        MaxBranches: BranchBillingPolicy.FreeMaxBranches,
        MaxTablesPerBranch: null,
        MaxActiveUsers: FreeTierCapacity.MaxActiveUsers,
        CanUseProductImages: true,
        CanUseMenuTranslations: false,
        CanManageAdditionalRoles: false,
        CanUseLiveOrderPanel: true,
        CanUseMultiBranch: false,
        HasPrioritySupport: false,
        CanUseMenuThemes: false,
        CanUseBrandWatermark: false,
        MaxActiveQrCodes: FreeTierCapacity.MaxActiveQrCodesPerBranch,
        MaxConcurrentLiveSessions: FreeTierCapacity.MaxConcurrentLiveSessions,
        CanUsePromotions: false,
        CanUseAnalytics: true);

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
        HasPrioritySupport: false,
        CanUseMenuThemes: true,
        CanUseBrandWatermark: true,
        MaxActiveQrCodes: null,
        MaxConcurrentLiveSessions: null,
        CanUsePromotions: true,
        CanUseAnalytics: true);

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
        HasPrioritySupport: true,
        CanUseMenuThemes: true,
        CanUseBrandWatermark: true,
        MaxActiveQrCodes: null,
        MaxConcurrentLiveSessions: null,
        CanUsePromotions: true,
        CanUseAnalytics: true);

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
        // Active trial unlocks Pro feature surface (themes, watermark, live panel, …).
        if (isTrial && baseline.PlanCode == SubscriptionPlanCodes.Free)
        {
            baseline = Pro with { DisplayName = "Pro (deneme)" };
        }

        var maxBranches = subscription.OverrideMaxBranches
            ?? BranchBillingPolicy.ResolveMaxBranches(
                subscription.PlanCode,
                isTrial,
                subscription.PurchasedBranchAddonCount);
        var canMulti = maxBranches is null or > 1;
        return baseline with
        {
            MaxBranches = maxBranches,
            MaxActiveUsers = subscription.OverrideMaxActiveUsers ?? baseline.MaxActiveUsers,
            MaxActiveQrCodes = subscription.OverrideMaxActiveQrCodes ?? baseline.MaxActiveQrCodes,
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
    public int? OverrideMaxBranches { get; private set; }
    public int? OverrideMaxActiveUsers { get; private set; }
    public int? OverrideMaxOrderHistoryHours { get; private set; }
    public int? OverrideMaxActiveQrCodes { get; private set; }
    public string? ContractNote { get; private set; }

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

    /// <summary>Start or restart a Pro trial from Free. Paid Pro (no expiry) is left unchanged.</summary>
    public void StartProTrial(DateTimeOffset nowUtc)
    {
        var now = nowUtc.ToUniversalTime();
        if (PlanCode == SubscriptionPlanCodes.Pro && ExpiresAtUtc is null)
        {
            throw new InvalidOperationException("Paid Pro subscriptions cannot start a trial.");
        }

        PlanCode = SubscriptionPlanCodes.Pro;
        StartedAtUtc = now;
        ExpiresAtUtc = now.Add(SubscriptionTrials.ProTrialDuration);
        UpdatedAtUtc = now;
    }

    public void DowngradeToFree(DateTimeOffset nowUtc)
    {
        ChangePlan(SubscriptionPlanCodes.Free, nowUtc, expiresAtUtc: null);
        PurchasedBranchAddonCount = 0;
        OverrideMaxBranches = null;
        OverrideMaxActiveUsers = null;
        OverrideMaxOrderHistoryHours = null;
        OverrideMaxActiveQrCodes = null;
        ContractNote = null;
    }

    public void SetPurchasedBranchAddonCount(int count, DateTimeOffset nowUtc)
    {
        PurchasedBranchAddonCount = Math.Max(0, count);
        UpdatedAtUtc = nowUtc.ToUniversalTime();
    }

    public void SetContractOverrides(
        int? maxBranches,
        int? maxActiveUsers,
        int? maxOrderHistoryHours,
        int? maxActiveQrCodes,
        string? contractNote,
        DateTimeOffset nowUtc)
    {
        OverrideMaxBranches = NormalizePositiveLimit(maxBranches, "şube");
        OverrideMaxActiveUsers = NormalizePositiveLimit(maxActiveUsers, "kullanıcı");
        OverrideMaxActiveQrCodes = NormalizePositiveLimit(maxActiveQrCodes, "QR");
        OverrideMaxOrderHistoryHours = NormalizeHours(maxOrderHistoryHours);
        var note = string.IsNullOrWhiteSpace(contractNote) ? null : contractNote.Trim();
        if (note is { Length: > 2000 })
        {
            throw new ArgumentException("Sözleşme notu en fazla 2000 karakter olabilir.");
        }

        ContractNote = note;
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

    private static int? NormalizePositiveLimit(int? value, string label)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 1 || value > 100_000)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"{label} tavanı 1–100000 arasında olmalı.");
        }

        return value;
    }

    private static int? NormalizeHours(int? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value < 1 || value > 24 * 365 * 5)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Sipariş geçmişi 1 saat–5 yıl arasında olmalı.");
        }

        return value;
    }
}

public static class EnterpriseQuoteStatuses
{
    public const string Open = "Open";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";

    public static string Normalize(string? status)
    {
        if (string.Equals(status, Accepted, StringComparison.OrdinalIgnoreCase))
        {
            return Accepted;
        }

        if (string.Equals(status, Rejected, StringComparison.OrdinalIgnoreCase))
        {
            return Rejected;
        }

        return Open;
    }
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

        Status = EnterpriseQuoteStatuses.Open;
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
    public string Status { get; private set; } = EnterpriseQuoteStatuses.Open;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string? DecisionNote { get; private set; }

    public bool IsOpen => Status == EnterpriseQuoteStatuses.Open;

    public void Accept(Guid reviewerUserId, DateTimeOffset nowUtc, string? decisionNote) =>
        Close(EnterpriseQuoteStatuses.Accepted, reviewerUserId, nowUtc, decisionNote);

    public void Reject(Guid reviewerUserId, DateTimeOffset nowUtc, string? decisionNote) =>
        Close(EnterpriseQuoteStatuses.Rejected, reviewerUserId, nowUtc, decisionNote);

    private void Close(string status, Guid reviewerUserId, DateTimeOffset nowUtc, string? decisionNote)
    {
        if (!IsOpen)
        {
            throw new InvalidOperationException("Quote is not open.");
        }

        if (reviewerUserId == Guid.Empty)
        {
            throw new ArgumentException("Reviewer is required.", nameof(reviewerUserId));
        }

        var note = string.IsNullOrWhiteSpace(decisionNote) ? null : decisionNote.Trim();
        if (note is { Length: > 2000 })
        {
            throw new ArgumentException("Decision note is too long.");
        }

        Status = status;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = nowUtc.ToUniversalTime();
        DecisionNote = note;
    }

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
