namespace RestaurantOS.Domain;

public static class AuthRealms
{
    public const string Management = "management";
    public const string Platform = "platform";

    public static string Normalize(string? realm) =>
        string.Equals(realm?.Trim(), Platform, StringComparison.OrdinalIgnoreCase)
            ? Platform
            : Management;
}

public static class PlatformStaffRoles
{
    public const string Owner = "Owner";
    public const string Billing = "Billing";
    public const string Support = "Support";
    public const string ReadOnly = "ReadOnly";

    public static string Normalize(string? roleCode)
    {
        if (string.IsNullOrWhiteSpace(roleCode))
        {
            return ReadOnly;
        }

        var value = roleCode.Trim();
        if (value.Equals(Owner, StringComparison.OrdinalIgnoreCase))
        {
            return Owner;
        }

        if (value.Equals(Billing, StringComparison.OrdinalIgnoreCase))
        {
            return Billing;
        }

        if (value.Equals(Support, StringComparison.OrdinalIgnoreCase))
        {
            return Support;
        }

        return ReadOnly;
    }

    public static bool CanWriteCatalog(string roleCode)
    {
        var role = Normalize(roleCode);
        return role is Owner or Billing;
    }

    public static bool CanPublishCatalog(string roleCode) => Normalize(roleCode) == Owner;

    public static bool CanManageCampaigns(string roleCode)
    {
        var role = Normalize(roleCode);
        return role is Owner or Billing or Support;
    }
}

public static class BillingIntervals
{
    public const string Month = "month";
    public const string Year = "year";

    public static string Normalize(string? interval)
    {
        if (string.Equals(interval?.Trim(), Year, StringComparison.OrdinalIgnoreCase))
        {
            return Year;
        }

        if (string.Equals(interval?.Trim(), Month, StringComparison.OrdinalIgnoreCase))
        {
            return Month;
        }

        throw new ArgumentException("Interval must be month or year.");
    }
}

public static class CatalogProductKinds
{
    public const string Plan = "plan";
    public const string Addon = "addon";
}

public static class CatalogProductCodes
{
    public const string ExtraBranch = "ExtraBranch";

    public static string KindOf(string productCode) =>
        Normalize(productCode) == ExtraBranch ? CatalogProductKinds.Addon : CatalogProductKinds.Plan;

    public static string Normalize(string? productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            throw new ArgumentException("Product code is required.");
        }

        var value = productCode.Trim();
        if (value.Equals(ExtraBranch, StringComparison.OrdinalIgnoreCase))
        {
            return ExtraBranch;
        }

        return PlanCatalog.Normalize(value);
    }

    public static string DisplayName(string productCode) => Normalize(productCode) switch
    {
        SubscriptionPlanCodes.Free => "Free",
        SubscriptionPlanCodes.Pro => "Pro",
        SubscriptionPlanCodes.Enterprise => "Enterprise",
        ExtraBranch => "Ek şube",
        _ => productCode,
    };
}

public static class PlanPriceStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Archived = "archived";
}

public sealed class PlatformStaff
{
    private PlatformStaff() { }

    public PlatformStaff(Guid userId, string roleCode, DateTimeOffset grantedAtUtc)
    {
        UserId = userId;
        RoleCode = PlatformStaffRoles.Normalize(roleCode);
        GrantedAtUtc = grantedAtUtc.ToUniversalTime();
        IsActive = true;
    }

    public Guid UserId { get; private set; }
    public string RoleCode { get; private set; } = PlatformStaffRoles.ReadOnly;
    public bool IsActive { get; private set; }
    public DateTimeOffset GrantedAtUtc { get; private set; }

    public void ChangeRole(string roleCode) => RoleCode = PlatformStaffRoles.Normalize(roleCode);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}

/// <summary>
/// Immutable list-price version. Amount is never updated in place; publish a new row.
/// Published prices are the vitrine for new upgrades. Existing tenants keep PlanCode only
/// until a payment provider binds TenantSubscription to a PriceId.
/// </summary>
public sealed class PlanPrice
{
    private PlanPrice() { }

    public PlanPrice(
        Guid id,
        string productCode,
        string interval,
        string currency,
        long amountMinor,
        bool taxInclusive,
        DateTimeOffset createdAtUtc,
        Guid createdByUserId)
    {
        if (amountMinor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amountMinor), "Amount cannot be negative.");
        }

        ProductCode = CatalogProductCodes.Normalize(productCode);
        Interval = BillingIntervals.Normalize(interval);
        Currency = NormalizeCurrency(currency);
        if (ProductCode == SubscriptionPlanCodes.Free && amountMinor != 0)
        {
            throw new ArgumentException("Free plan price must be zero.");
        }

        Id = id;
        ProductKind = CatalogProductCodes.KindOf(ProductCode);
        AmountMinor = amountMinor;
        TaxInclusive = taxInclusive;
        Status = PlanPriceStatuses.Draft;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        CreatedByUserId = createdByUserId;
    }

    public Guid Id { get; private set; }
    public string ProductCode { get; private set; } = SubscriptionPlanCodes.Free;
    public string ProductKind { get; private set; } = CatalogProductKinds.Plan;
    public string Interval { get; private set; } = BillingIntervals.Month;
    public string Currency { get; private set; } = "TRY";
    public long AmountMinor { get; private set; }
    public bool TaxInclusive { get; private set; }
    public string Status { get; private set; } = PlanPriceStatuses.Draft;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public bool IsDraft => Status == PlanPriceStatuses.Draft;
    public bool IsPublished => Status == PlanPriceStatuses.Published;
    public bool IsArchived => Status == PlanPriceStatuses.Archived;

    public void Publish(DateTimeOffset nowUtc)
    {
        if (IsArchived)
        {
            throw new InvalidOperationException("Archived prices cannot be published.");
        }

        Status = PlanPriceStatuses.Published;
        PublishedAtUtc = nowUtc.ToUniversalTime();
        ArchivedAtUtc = null;
    }

    public void Archive(DateTimeOffset nowUtc)
    {
        if (IsDraft)
        {
            Status = PlanPriceStatuses.Archived;
            ArchivedAtUtc = nowUtc.ToUniversalTime();
            return;
        }

        if (!IsPublished)
        {
            return;
        }

        Status = PlanPriceStatuses.Archived;
        ArchivedAtUtc = nowUtc.ToUniversalTime();
    }

    private static string NormalizeCurrency(string? currency)
    {
        var value = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim().ToUpperInvariant();
        if (value.Length is < 3 or > 3)
        {
            throw new ArgumentException("Currency must be a 3-letter code.");
        }

        return value;
    }
}
