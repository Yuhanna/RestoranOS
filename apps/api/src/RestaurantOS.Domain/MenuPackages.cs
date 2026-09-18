namespace RestaurantOS.Domain;

/// <summary>
/// Optional fixed-price meal package (e.g. office lunch: main + rice + soup + salad + drink).
/// Branches that never create packages see no customer UI for this feature.
/// </summary>
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
        Money price,
        bool isActive,
        int sortOrder,
        TimeOnly? dailyStartLocal,
        TimeOnly? dailyEndLocal,
        byte? daysOfWeekMask)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        ApplyCore(name, description, price, isActive, sortOrder, dailyStartLocal, dailyEndLocal, daysOfWeekMask);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public long PriceAmountMinor { get; private set; }
    public string PriceCurrency { get; private set; } = null!;
    public Money Price => new(PriceAmountMinor, PriceCurrency);
    public bool IsActive { get; private set; }
    public int SortOrder { get; private set; }
    public TimeOnly? DailyStartLocal { get; private set; }
    public TimeOnly? DailyEndLocal { get; private set; }
    public byte? DaysOfWeekMask { get; private set; }
    public ICollection<MenuPackageComponent> Components => _components;

    public void Update(
        string name,
        string? description,
        Money price,
        bool isActive,
        int sortOrder,
        TimeOnly? dailyStartLocal,
        TimeOnly? dailyEndLocal,
        byte? daysOfWeekMask) =>
        ApplyCore(name, description, price, isActive, sortOrder, dailyStartLocal, dailyEndLocal, daysOfWeekMask);

    public void SetActive(bool isActive) => IsActive = isActive;

    public void ReplaceComponents(IEnumerable<MenuPackageComponent> components)
    {
        _components.Clear();
        var ordered = components.OrderBy(x => x.SortOrder).ToList();
        if (ordered.Count is < 2 or > 12)
        {
            throw new ArgumentException("A package needs between 2 and 12 components.");
        }

        var distinct = ordered.Select(x => x.MenuItemId).Distinct().Count();
        if (distinct != ordered.Count)
        {
            throw new ArgumentException("Package components must be unique menu items.");
        }

        foreach (var component in ordered)
        {
            _components.Add(component);
        }
    }

    /// <summary>Available for customer ordering at the given instant in branch local time.</summary>
    public bool IsOfferActiveAt(DateTimeOffset utcNow, TimeZoneInfo? branchTimeZone)
    {
        if (!IsActive || _components.Count < 2)
        {
            return false;
        }

        var local = branchTimeZone is null
            ? utcNow.LocalDateTime
            : TimeZoneInfo.ConvertTime(utcNow, branchTimeZone).DateTime;
        if (!AppliesOnWeekday(local.DayOfWeek))
        {
            return false;
        }

        if (DailyStartLocal is null || DailyEndLocal is null)
        {
            return true;
        }

        var time = TimeOnly.FromDateTime(local);
        var start = DailyStartLocal.Value;
        var end = DailyEndLocal.Value;
        if (start == end)
        {
            return true;
        }

        return start < end
            ? time >= start && time < end
            : time >= start || time < end;
    }

    public bool AppliesOnWeekday(DayOfWeek dayOfWeek)
    {
        if (DaysOfWeekMask is null)
        {
            return true;
        }

        var bit = 1 << (int)dayOfWeek;
        return (DaysOfWeekMask.Value & bit) != 0;
    }

    private void ApplyCore(
        string name,
        string? description,
        Money price,
        bool isActive,
        int sortOrder,
        TimeOnly? dailyStartLocal,
        TimeOnly? dailyEndLocal,
        byte? daysOfWeekMask)
    {
        if (dailyStartLocal is null ^ dailyEndLocal is null)
        {
            throw new ArgumentException("Daily start and end must both be set or both empty.");
        }

        if (daysOfWeekMask is < 0 or > 0b0111_1111)
        {
            throw new ArgumentOutOfRangeException(nameof(daysOfWeekMask));
        }

        Name = Required(name, 160);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (Description?.Length > 2000)
        {
            throw new ArgumentException("Description is too long.");
        }

        PriceAmountMinor = price.AmountMinor;
        PriceCurrency = string.IsNullOrWhiteSpace(price.Currency) ? "TRY" : price.Currency.Trim().ToUpperInvariant();
        IsActive = isActive;
        SortOrder = sortOrder;
        DailyStartLocal = dailyStartLocal;
        DailyEndLocal = dailyEndLocal;
        DaysOfWeekMask = daysOfWeekMask is 0 or 0b0111_1111 ? null : daysOfWeekMask;
    }

    private static string Required(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.");
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value exceeds {maxLength} characters.");
        }

        return trimmed;
    }
}

public sealed class MenuPackageComponent
{
    private MenuPackageComponent() { }

    public MenuPackageComponent(Guid menuPackageId, Guid menuItemId, string? slotLabel, int sortOrder)
    {
        MenuPackageId = menuPackageId;
        MenuItemId = menuItemId;
        SlotLabel = string.IsNullOrWhiteSpace(slotLabel) ? null : slotLabel.Trim();
        if (SlotLabel?.Length > 80)
        {
            throw new ArgumentException("Slot label is too long.");
        }

        SortOrder = sortOrder;
    }

    public Guid MenuPackageId { get; private set; }
    public Guid MenuItemId { get; private set; }
    public string? SlotLabel { get; private set; }
    public int SortOrder { get; private set; }
    public MenuItem? MenuItem { get; private set; }
}
