namespace RestaurantOS.Domain;

/// <summary>
/// How a commercial feature is presented and gated in product surfaces.
/// SoftDiscover = visible/locked with upgrade copy; CapacityMeter = usage bar;
/// HardBlock = API EnsureCanUse*; AhaOpen = never sold (core trust path).
/// </summary>
public enum MonetizationGateMode
{
    AhaOpen = 0,
    SoftDiscover = 1,
    CapacityMeter = 2,
    HardBlock = 3,
}

public enum MonetizationCtaAction
{
    UpgradePro = 0,
    StartTrial = 1,
    EnterpriseQuote = 2,
    BuyAddon = 3,
}

/// <summary>
/// Single source of upgrade copy and gate mode for a sellable capability.
/// New paid features should register here before shipping UI.
/// </summary>
public sealed record MonetizationFeature(
    string FeatureKey,
    string MinPlanCode,
    MonetizationGateMode GateMode,
    bool AhaSafe,
    string TitleTr,
    string BodyTr,
    string? ProofTr,
    string CtaLabelTr,
    MonetizationCtaAction CtaAction,
    string EventSeen,
    string EventDismissed,
    string EventCta);

/// <summary>
/// Reusable Free → Pro → Enterprise messaging policy.
/// Numbers/limits stay in PlanCatalog / FreeTierCapacity / OrderHistoryRetention;
/// this catalog owns tone, CTA, and gate mode so every surface stays consistent.
/// </summary>
public static class MonetizationPolicy
{
    public const string ActiveQr = "active_qr";
    public const string ActiveUsers = "active_users";
    public const string LivePanelSessions = "live_panel_sessions";
    public const string AnalyticsLookback = "analytics_lookback";
    public const string OrderHistoryLookback = "order_history_lookback";
    public const string MenuThemes = "menu_themes";
    public const string BrandWatermark = "brand_watermark";
    public const string MultiBranch = "multi_branch";
    public const string AdditionalRoles = "additional_roles";
    public const string Promotions = "promotions";
    public const string MenuTranslations = "menu_translations";
    public const string TrialExpiring = "trial_expiring";

    public const int UsageWarnPercent = 70;
    public const int UsageCriticalPercent = 90;

    public static readonly MonetizationFeature ActiveQrFeature = new(
        ActiveQr,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.CapacityMeter,
        AhaSafe: true,
        TitleTr: "Masalarınız Free kotasını doldurdu",
        BodyTr: "Aktif QR kotanız doldu. Pro’da sınırsız aktif QR ile tüm salonu aynı anda menüye bağlarsınız.",
        ProofTr: $"Free’de en fazla {FreeTierCapacity.MaxActiveQrCodesPerBranch} aktif QR.",
        CtaLabelTr: "Pro’ya geç",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_active_qr",
        EventDismissed: "upgrade_dismissed_active_qr",
        EventCta: "upgrade_cta_active_qr");

    public static readonly MonetizationFeature ActiveUsersFeature = new(
        ActiveUsers,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.CapacityMeter,
        AhaSafe: true,
        TitleTr: "Ekip büyüyor",
        BodyTr: "Free’de sınırlı kullanıcı koltuğu vardır. Yoğun vardiyada yetki dağıtımı için Pro gerekir.",
        ProofTr: $"Free’de en fazla {FreeTierCapacity.MaxActiveUsers} aktif kullanıcı; yönetici rolleri Pro’dadır.",
        CtaLabelTr: "Rolleri Pro ile aç",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_active_users",
        EventDismissed: "upgrade_dismissed_active_users",
        EventCta: "upgrade_cta_active_users");

    public static readonly MonetizationFeature LivePanelFeature = new(
        LivePanelSessions,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.CapacityMeter,
        AhaSafe: true,
        TitleTr: "İkinci ekran Pro’da",
        BodyTr: "Free’de 1 canlı oturum vardır. Mutfak ve kasa aynı anda izlemek için Pro ile eşzamanlı paneller açılır.",
        ProofTr: $"Free’de en fazla {FreeTierCapacity.MaxConcurrentLiveSessions} canlı panel.",
        CtaLabelTr: "Canlı paneli yükselt",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_live_panel",
        EventDismissed: "upgrade_dismissed_live_panel",
        EventCta: "upgrade_cta_live_panel");

    public static readonly MonetizationFeature AnalyticsLookbackFeature = new(
        AnalyticsLookback,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.SoftDiscover,
        AhaSafe: true,
        TitleTr: "Daha uzun bakış Pro’da",
        BodyTr: "Free’de son birkaç günün istatistiğini görebilirsiniz. Haftalık ve aylık trend Pro ile açılır.",
        ProofTr: $"Free ≈ {OrderHistoryRetention.FreeMaxHours / 24} gün · Pro ≈ {OrderHistoryRetention.ProMaxHours / 24} gün.",
        CtaLabelTr: "İstatistikleri uzat",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_analytics",
        EventDismissed: "upgrade_dismissed_analytics",
        EventCta: "upgrade_cta_analytics");

    public static readonly MonetizationFeature OrderHistoryFeature = new(
        OrderHistoryLookback,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.SoftDiscover,
        AhaSafe: true,
        TitleTr: "Daha uzun sipariş geçmişi Pro’da",
        BodyTr: "Free operasyon penceresi kısadır. Geçen hafta veya ayı incelemek için Pro gerekir.",
        ProofTr: $"Free en fazla {OrderHistoryRetention.FreeMaxHours} saat · Pro {OrderHistoryRetention.ProMaxHours} saat.",
        CtaLabelTr: "Geçmişi uzat",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_order_history",
        EventDismissed: "upgrade_dismissed_order_history",
        EventCta: "upgrade_cta_order_history");

    public static readonly MonetizationFeature MenuThemesFeature = new(
        MenuThemes,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.SoftDiscover,
        AhaSafe: true,
        TitleTr: "Menünüz markanız gibi görünsün",
        BodyTr: "Free’de Modern görünüm açıktır. Diğer temalar Pro veya denemede — misafir deneyimini restoran karakterinize uyarlar.",
        ProofTr: "Modern ücretsiz; diğer görünümler Pro.",
        CtaLabelTr: "Temaları aç",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_menu_themes",
        EventDismissed: "upgrade_dismissed_menu_themes",
        EventCta: "upgrade_cta_menu_themes");

    public static readonly MonetizationFeature BrandWatermarkFeature = new(
        BrandWatermark,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.SoftDiscover,
        AhaSafe: true,
        TitleTr: "Marka nefesi Pro’da",
        BodyTr: "Silik arka plan logosu Pro (veya aktif deneme) ile açılır. Free’de logo yalnızca menü başlığında kalır.",
        ProofTr: null,
        CtaLabelTr: "Filigranı aç",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_watermark",
        EventDismissed: "upgrade_dismissed_watermark",
        EventCta: "upgrade_cta_watermark");

    public static readonly MonetizationFeature MultiBranchFeature = new(
        MultiBranch,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.HardBlock,
        AhaSafe: true,
        TitleTr: "İkinci lokasyon Pro / deneme ile",
        BodyTr: "Free tek şubedir. Denemede en fazla 3 şube deneyin; Pro’da 2 şube dahil, fazlası ek ücrettir.",
        ProofTr: "Başarı sinyali — ceza değil.",
        CtaLabelTr: "Çok şubeyi aç",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_multi_branch",
        EventDismissed: "upgrade_dismissed_multi_branch",
        EventCta: "upgrade_cta_multi_branch");

    public static readonly MonetizationFeature AdditionalRolesFeature = new(
        AdditionalRoles,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.HardBlock,
        AhaSafe: true,
        TitleTr: "Yönetici rolleri Pro’da",
        BodyTr: "Free’de personel ekleyebilirsiniz. Şube yöneticisi ve merkez rolleri Pro’dadır.",
        ProofTr: null,
        CtaLabelTr: "Rolleri yükselt",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_roles",
        EventDismissed: "upgrade_dismissed_roles",
        EventCta: "upgrade_cta_roles");

    public static readonly MonetizationFeature PromotionsFeature = new(
        Promotions,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.HardBlock,
        AhaSafe: true,
        TitleTr: "Kampanyalar Pro’da",
        BodyTr: "İndirim ve menü kampanyaları Pro ile yönetilir. Free’de menü ve sipariş akışınız açık kalır.",
        ProofTr: null,
        CtaLabelTr: "Kampanyaları aç",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_promotions",
        EventDismissed: "upgrade_dismissed_promotions",
        EventCta: "upgrade_cta_promotions");

    public static readonly MonetizationFeature MenuTranslationsFeature = new(
        MenuTranslations,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.HardBlock,
        AhaSafe: true,
        TitleTr: "Çok dilli menü Pro’da",
        BodyTr: "TR dışında menü çevirileri Pro ile açılır. Misafir diline göre ürün adları ve açıklamalar gösterilir.",
        ProofTr: null,
        CtaLabelTr: "Çevirileri aç",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_translations",
        EventDismissed: "upgrade_dismissed_translations",
        EventCta: "upgrade_cta_translations");

    public static readonly MonetizationFeature TrialExpiringFeature = new(
        TrialExpiring,
        SubscriptionPlanCodes.Pro,
        MonetizationGateMode.SoftDiscover,
        AhaSafe: true,
        TitleTr: "Denemeniz yakında bitiyor",
        BodyTr: "Temalar, kampanya ve çok şube deneme bitince Free sınırlarına döner. Kesintisiz devam için Pro’yu sabitleyin.",
        ProofTr: "7 / 3 / 1 gün kala uyarı güçlenir.",
        CtaLabelTr: "Pro’yu sabitle",
        CtaAction: MonetizationCtaAction.UpgradePro,
        EventSeen: "upgrade_seen_trial_expiring",
        EventDismissed: "upgrade_dismissed_trial_expiring",
        EventCta: "upgrade_cta_trial_expiring");

    public static IReadOnlyList<MonetizationFeature> All { get; } =
    [
        ActiveQrFeature,
        ActiveUsersFeature,
        LivePanelFeature,
        AnalyticsLookbackFeature,
        OrderHistoryFeature,
        MenuThemesFeature,
        BrandWatermarkFeature,
        MultiBranchFeature,
        AdditionalRolesFeature,
        PromotionsFeature,
        MenuTranslationsFeature,
        TrialExpiringFeature,
    ];

    private static readonly Dictionary<string, MonetizationFeature> ByKey =
        All.ToDictionary(x => x.FeatureKey, StringComparer.Ordinal);

    public static MonetizationFeature Get(string featureKey) =>
        ByKey.TryGetValue(featureKey, out var feature)
            ? feature
            : throw new ArgumentException($"Unknown monetization feature '{featureKey}'.", nameof(featureKey));

    public static bool TryGet(string featureKey, out MonetizationFeature feature) =>
        ByKey.TryGetValue(featureKey, out feature!);

    /// <summary>Usage bar tone: below warn = ok, warn..critical = warn, else critical.</summary>
    public static string ResolveUsageTone(int used, int? max)
    {
        if (max is null or <= 0)
        {
            return "ok";
        }

        var pct = used * 100.0 / max.Value;
        if (pct >= 100 || used >= max.Value)
        {
            return "critical";
        }

        if (pct >= UsageCriticalPercent)
        {
            return "critical";
        }

        if (pct >= UsageWarnPercent)
        {
            return "warn";
        }

        return "ok";
    }

    public static int ResolveMaxAnalyticsDays(int maxOrderHistoryHours) =>
        Math.Max(1, (int)Math.Ceiling(maxOrderHistoryHours / 24.0));

    public static int ClampAnalyticsDays(int requestedDays, int maxOrderHistoryHours)
    {
        var maxDays = ResolveMaxAnalyticsDays(maxOrderHistoryHours);
        return Math.Clamp(requestedDays, 1, maxDays);
    }

    public static int? TrialDaysRemaining(DateTimeOffset? trialEndsAtUtc, DateTimeOffset nowUtc)
    {
        if (trialEndsAtUtc is null)
        {
            return null;
        }

        var remaining = trialEndsAtUtc.Value.ToUniversalTime() - nowUtc.ToUniversalTime();
        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }

        return (int)Math.Ceiling(remaining.TotalDays);
    }

    public static string TrialUrgency(int? daysRemaining) =>
        daysRemaining switch
        {
            null => "none",
            <= 1 => "critical",
            <= 3 => "high",
            <= 7 => "medium",
            _ => "low",
        };
}
