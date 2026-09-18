using RestaurantOS.Domain;

namespace RestaurantOS.Web.Models;

public sealed class UpgradePromptViewModel
{
    public string FeatureKey { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? Proof { get; set; }
    public string PrimaryLabel { get; set; } = "Pro’ya geç";
    public string PrimaryAction { get; set; } = "upgrade"; // upgrade | trial
    public bool Dismissible { get; set; } = true;
    public string Tone { get; set; } = "info"; // info | warn | critical
    public bool Compact { get; set; }
}

public sealed class UsageMeterViewModel
{
    public string Label { get; set; } = string.Empty;
    public int Used { get; set; }
    public int? Max { get; set; }
    public string Tone { get; set; } = "ok";
    public string? FeatureKey { get; set; }
    public string? Hint { get; set; }
}

public sealed class TrialCountdownViewModel
{
    public int DaysRemaining { get; set; }
    public DateTimeOffset EndsAtUtc { get; set; }
    public string Urgency { get; set; } = "low";
    public bool CanManageSubscription { get; set; }
}

public sealed class SubscriptionPageViewModel
{
    public WorkspaceViewModel? Workspace { get; set; }
    public IReadOnlyList<UpgradePromptViewModel> Highlights { get; set; } = [];
}

public static class MonetizationUi
{
    public static UpgradePromptViewModel FromFeature(
        MonetizationFeature feature,
        string? toneOverride = null,
        bool compact = false)
    {
        var action = feature.CtaAction switch
        {
            MonetizationCtaAction.StartTrial => "trial",
            _ => "upgrade",
        };

        return new UpgradePromptViewModel
        {
            FeatureKey = feature.FeatureKey,
            Title = feature.TitleTr,
            Body = feature.BodyTr,
            Proof = feature.ProofTr,
            PrimaryLabel = feature.CtaLabelTr,
            PrimaryAction = action,
            Tone = toneOverride ?? (feature.GateMode == MonetizationGateMode.HardBlock ? "warn" : "info"),
            Compact = compact,
            Dismissible = true,
        };
    }

    public static UsageMeterViewModel Meter(
        string label,
        int used,
        int? max,
        string? featureKey = null,
        string? hint = null)
    {
        return new UsageMeterViewModel
        {
            Label = label,
            Used = used,
            Max = max,
            Tone = MonetizationPolicy.ResolveUsageTone(used, max),
            FeatureKey = featureKey,
            Hint = hint,
        };
    }

    public static TrialCountdownViewModel? TrialCountdown(
        EntitlementUsageViewModel? entitlements,
        bool canManageSubscription)
    {
        if (entitlements is not { IsTrial: true, TrialEndsAtUtc: not null })
        {
            return null;
        }

        var days = MonetizationPolicy.TrialDaysRemaining(entitlements.TrialEndsAtUtc, DateTimeOffset.UtcNow) ?? 0;
        return new TrialCountdownViewModel
        {
            DaysRemaining = days,
            EndsAtUtc = entitlements.TrialEndsAtUtc.Value,
            Urgency = MonetizationPolicy.TrialUrgency(days),
            CanManageSubscription = canManageSubscription,
        };
    }
}
