namespace RestaurantOS.Domain;

public sealed class MenuPackage
{
    private readonly List<MenuPackageComponent> _components = [];

    private MenuPackage() { }

    public MenuPackage(
        Guid id,
        Guid tenantId,
        Guid branchId,
        string name,
        string? description,
        long priceAmountMinor,
        string priceCurrency,
        int sortOrder,
        TimeOnly? dailyStartLocal,
        TimeOnly? dailyEndLocal,
        bool isActive)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        Name = name;
        Description = description;
        PriceAmountMinor = priceAmountMinor;
        PriceCurrency = string.IsNullOrWhiteSpace(priceCurrency) ? "TRY" : priceCurrency;
        SortOrder = sortOrder;
        DailyStartLocal = dailyStartLocal;
        DailyEndLocal = dailyEndLocal;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public long PriceAmountMinor { get; private set; }
    public string PriceCurrency { get; private set; } = "TRY";
    public int SortOrder { get; private set; }
    public TimeOnly? DailyStartLocal { get; private set; }
    public TimeOnly? DailyEndLocal { get; private set; }
    public bool IsActive { get; private set; }
    public IReadOnlyCollection<MenuPackageComponent> Components => _components;

    public bool IsOfferActiveAt(DateTimeOffset utcNow, TimeZoneInfo? branchTimeZone)
    {
        if (!IsActive)
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
}

public sealed class MenuPackageComponent
{
    private MenuPackageComponent() { }

    public MenuPackageComponent(Guid menuItemId, string? slotLabel, int sortOrder)
    {
        MenuItemId = menuItemId;
        SlotLabel = slotLabel;
        SortOrder = sortOrder;
    }

    public Guid MenuPackageId { get; private set; }
    public Guid MenuItemId { get; private set; }
    public string? SlotLabel { get; private set; }
    public int SortOrder { get; private set; }
    public MenuItem? MenuItem { get; private set; }
}
