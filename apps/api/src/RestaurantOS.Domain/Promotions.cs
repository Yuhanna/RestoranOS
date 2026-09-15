namespace RestaurantOS.Domain;

public static class PromotionScopes
{
    public const string AllMenu = "all_menu";
    public const string Category = "category";
    public const string Product = "product";

    public static string Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            Category => Category,
            Product => Product,
            _ => AllMenu,
        };
}

public static class DiscountKinds
{
    public const string Percent = "percent";
    public const string FixedMinor = "fixed_minor";

    public static string Normalize(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            FixedMinor => FixedMinor,
            _ => Percent,
        };
}

/// <summary>Restaurant-owner SaaS audience for in-app notifications and subscription offers.</summary>
public static class SubscriptionAudiences
{
    public const string All = "all";
    public const string Free = "free";
    public const string Pro = "pro";
    public const string Trial = "trial";
    public const string NonPro = "non_pro";

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            Free => Free,
            Pro => Pro,
            Trial => Trial,
            NonPro => NonPro,
            _ => All,
        };
    }

    public static bool Matches(string audience, TenantSubscription subscription, DateTimeOffset nowUtc)
    {
        var normalized = Normalize(audience);
        if (normalized == All)
        {
            return true;
        }

        var isTrial = subscription.IsTrialActive(nowUtc);
        var plan = PlanCatalog.Normalize(subscription.PlanCode);
        return normalized switch
        {
            Free => plan == SubscriptionPlanCodes.Free,
            Pro => plan == SubscriptionPlanCodes.Pro && !isTrial,
            Trial => isTrial,
            NonPro => plan != SubscriptionPlanCodes.Pro
                && plan != SubscriptionPlanCodes.Enterprise
                && !isTrial,
            _ => false,
        };
    }
}

public sealed class MenuPromotion
{
    private MenuPromotion() { }

    public MenuPromotion(
        Guid id,
        Guid tenantId,
        Guid branchId,
        string name,
        string scope,
        string discountKind,
        int discountValue,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        TimeOnly? dailyStartLocal,
        TimeOnly? dailyEndLocal,
        Guid? categoryId,
        Guid? menuItemId,
        bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        Name = Required(name, 120);
        Scope = PromotionScopes.Normalize(scope);
        DiscountKind = DiscountKinds.Normalize(discountKind);
        DiscountValue = discountValue;
        StartsAtUtc = startsAtUtc.ToUniversalTime();
        EndsAtUtc = endsAtUtc?.ToUniversalTime();
        DailyStartLocal = dailyStartLocal;
        DailyEndLocal = dailyEndLocal;
        CategoryId = categoryId;
        MenuItemId = menuItemId;
        IsActive = isActive;
        Validate();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Scope { get; private set; } = PromotionScopes.AllMenu;
    public string DiscountKind { get; private set; } = DiscountKinds.Percent;
    public int DiscountValue { get; private set; }
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }
    public TimeOnly? DailyStartLocal { get; private set; }
    public TimeOnly? DailyEndLocal { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? MenuItemId { get; private set; }
    public bool IsActive { get; private set; }

    public void SetActive(bool isActive) => IsActive = isActive;

    public void Rename(string name) => Name = Required(name, 120);

    public void SetEndsAt(DateTimeOffset? endsAtUtc) => EndsAtUtc = endsAtUtc?.ToUniversalTime();

    public bool IsActiveAt(DateTimeOffset utcNow, TimeZoneInfo? branchTimeZone = null)
    {
        if (!IsActive || utcNow < StartsAtUtc || (EndsAtUtc is not null && utcNow >= EndsAtUtc))
        {
            return false;
        }

        if (DailyStartLocal is null || DailyEndLocal is null)
        {
            return true;
        }

        var localNow = branchTimeZone is null
            ? TimeOnly.FromDateTime(utcNow.LocalDateTime)
            : TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(utcNow, branchTimeZone).DateTime);
        return DailyStartLocal <= DailyEndLocal
            ? localNow >= DailyStartLocal && localNow <= DailyEndLocal
            : localNow >= DailyStartLocal || localNow <= DailyEndLocal;
    }

    public bool AppliesTo(Guid menuItemId, Guid categoryId)
    {
        return Scope switch
        {
            PromotionScopes.Product => MenuItemId == menuItemId,
            PromotionScopes.Category => CategoryId == categoryId,
            _ => true,
        };
    }

    public long ApplyDiscount(long listAmountMinor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(listAmountMinor);
        if (DiscountKind == DiscountKinds.FixedMinor)
        {
            return Math.Max(0, listAmountMinor - DiscountValue);
        }

        var percent = Math.Clamp(DiscountValue, 0, 100);
        var discount = (listAmountMinor * percent + 50) / 100;
        return Math.Max(0, listAmountMinor - discount);
    }

    public long DiscountAmount(long listAmountMinor) => Math.Max(0, listAmountMinor - ApplyDiscount(listAmountMinor));

    public DateTimeOffset? EffectiveEndsAtUtc(DateTimeOffset utcNow, TimeZoneInfo? branchTimeZone) => EndsAtUtc;

    private void Validate()
    {
        if (Scope == PromotionScopes.Category && CategoryId is null)
        {
            throw new ArgumentException("Category promotions require a category id.");
        }

        if (Scope == PromotionScopes.Product && MenuItemId is null)
        {
            throw new ArgumentException("Product promotions require a menu item id.");
        }

        if (DiscountKind == DiscountKinds.Percent && DiscountValue is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(DiscountValue), "Percent discount must be between 1 and 100.");
        }

        if (DiscountKind == DiscountKinds.FixedMinor && DiscountValue < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(DiscountValue), "Fixed discount must be positive.");
        }
    }

    private static string Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.");
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed class TenantNotification
{
    private TenantNotification() { }

    public TenantNotification(
        Guid id,
        Guid? tenantId,
        string audience,
        string title,
        string body,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        string? actionUrl,
        bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        Audience = SubscriptionAudiences.Normalize(audience);
        Title = Required(title, 160);
        Body = Required(body, 2000);
        StartsAtUtc = startsAtUtc.ToUniversalTime();
        EndsAtUtc = endsAtUtc?.ToUniversalTime();
        ActionUrl = string.IsNullOrWhiteSpace(actionUrl) ? null : actionUrl.Trim();
        IsActive = isActive;
    }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Audience { get; private set; } = SubscriptionAudiences.All;
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }
    public string? ActionUrl { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastDispatchedAtUtc { get; private set; }

    public void MarkDispatched(DateTimeOffset utcNow) => LastDispatchedAtUtc = utcNow.ToUniversalTime();

    public void SetActive(bool isActive) => IsActive = isActive;

    public bool IsVisibleAt(DateTimeOffset utcNow) =>
        IsActive && utcNow >= StartsAtUtc && (EndsAtUtc is null || utcNow < EndsAtUtc);

    private static string Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.");
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed class SubscriptionOffer
{
    private SubscriptionOffer() { }

    public SubscriptionOffer(
        Guid id,
        string audience,
        string targetPlanCode,
        int discountPercent,
        int durationMonths,
        string title,
        string body,
        DateTimeOffset startsAtUtc,
        DateTimeOffset? endsAtUtc,
        bool isActive)
    {
        Id = id;
        Audience = SubscriptionAudiences.Normalize(audience);
        TargetPlanCode = PlanCatalog.Normalize(targetPlanCode);
        DiscountPercent = Math.Clamp(discountPercent, 1, 100);
        DurationMonths = Math.Clamp(durationMonths, 1, 36);
        Title = Required(title, 160);
        Body = Required(body, 2000);
        StartsAtUtc = startsAtUtc.ToUniversalTime();
        EndsAtUtc = endsAtUtc?.ToUniversalTime();
        IsActive = isActive;
    }

    public Guid Id { get; private set; }
    public string Audience { get; private set; } = SubscriptionAudiences.NonPro;
    public string TargetPlanCode { get; private set; } = SubscriptionPlanCodes.Pro;
    public int DiscountPercent { get; private set; }
    public int DurationMonths { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;
    public DateTimeOffset StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }
    public bool IsActive { get; private set; }

    public bool IsActiveAt(DateTimeOffset utcNow) =>
        IsActive && utcNow >= StartsAtUtc && (EndsAtUtc is null || utcNow < EndsAtUtc);

    public void SetActive(bool isActive) => IsActive = isActive;

    private static string Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.");
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public sealed record PriceBreakdown(long ListAmountMinor, long DiscountAmountMinor, long FinalAmountMinor, string Currency)
{
    public static PriceBreakdown FromListPrice(long listAmountMinor, MenuPromotion? promotion, string currency = "TRY")
    {
        if (promotion is null || listAmountMinor <= 0)
        {
            return new PriceBreakdown(listAmountMinor, 0, listAmountMinor, currency);
        }

        var final = promotion.ApplyDiscount(listAmountMinor);
        return new PriceBreakdown(listAmountMinor, listAmountMinor - final, final, currency);
    }
}
