namespace RestaurantOS.Domain;

public enum QrCodeStatus
{
    Inactive = 0,
    Active = 1,
    Revoked = 2,
}

public enum OrderStatus
{
    Submitted = 1,
    Accepted = 2,
    Preparing = 3,
    Ready = 4,
    Completed = 5,
    Cancelled = 6,
}

public readonly record struct Money(long AmountMinor, string Currency)
{
    public static Money Try(long amountMinor)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(amountMinor);
        return new Money(amountMinor, "TRY");
    }
}

public static class SupportedLocales
{
    public const string Turkish = "tr";
    public const string English = "en";

    public static string Normalize(string? locale)
    {
        var value = (locale ?? Turkish).Trim().ToLowerInvariant();
        return value is "en" or "en-us" or "en-gb" ? English : Turkish;
    }

    public static bool IsSupported(string? locale)
    {
        var value = (locale ?? string.Empty).Trim().ToLowerInvariant();
        return value is "tr" or "tr-tr" or "en" or "en-us" or "en-gb";
    }
}

public sealed class Tenant
{
    private Tenant() { }
    public Tenant(Guid id, string name) => (Id, Name) = (id, Required(name));
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}

public sealed class Restaurant
{
    private Restaurant() { }
    public Restaurant(Guid id, Guid tenantId, string name) =>
        (Id, TenantId, Name) = (id, tenantId, Required(name));
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = null!;
    public Tenant Tenant { get; private set; } = null!;
    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}

public sealed class Branch
{
    private Branch() { }
    public Branch(Guid id, Guid tenantId, Guid restaurantId, string name) =>
        (Id, TenantId, RestaurantId, Name) = (id, tenantId, restaurantId, Required(name));
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RestaurantId { get; private set; }
    public string Name { get; private set; } = null!;
    public Restaurant Restaurant { get; private set; } = null!;
    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}

public sealed class DiningTable
{
    private DiningTable() { }
    public DiningTable(Guid id, Guid tenantId, Guid branchId, string label) =>
        (Id, TenantId, BranchId, Label, IsActive) = (id, tenantId, branchId, Required(label), true);
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Label { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public Branch Branch { get; private set; } = null!;
    public void Rename(string label) => Label = Required(label);
    public void SetActive(bool isActive) => IsActive = isActive;
    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}

public sealed class InvalidQrCodeTransitionException(QrCodeStatus current, QrCodeStatus next)
    : InvalidOperationException($"QR status cannot transition from {current} to {next}.")
{
    public QrCodeStatus Current { get; } = current;
    public QrCodeStatus Next { get; } = next;
}

public sealed class TableQrCode
{
    private TableQrCode() { }
    public TableQrCode(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        string tokenHash,
        DateTimeOffset createdAtUtc,
        string? protectedToken = null)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        TableId = tableId;
        TokenHash = Required(tokenHash);
        ProtectedToken = string.IsNullOrWhiteSpace(protectedToken) ? null : protectedToken.Trim();
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        Status = QrCodeStatus.Active;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid TableId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public string? ProtectedToken { get; private set; }
    public QrCodeStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public DiningTable Table { get; private set; } = null!;

    public void Activate()
    {
        if (Status == QrCodeStatus.Active)
        {
            return;
        }

        if (Status != QrCodeStatus.Inactive)
        {
            throw new InvalidQrCodeTransitionException(Status, QrCodeStatus.Active);
        }

        Status = QrCodeStatus.Active;
    }

    public void Deactivate()
    {
        if (Status == QrCodeStatus.Inactive)
        {
            return;
        }

        if (Status != QrCodeStatus.Active)
        {
            throw new InvalidQrCodeTransitionException(Status, QrCodeStatus.Inactive);
        }

        Status = QrCodeStatus.Inactive;
    }

    public void Revoke(DateTimeOffset nowUtc)
    {
        if (Status == QrCodeStatus.Revoked)
        {
            return;
        }

        Status = QrCodeStatus.Revoked;
        RevokedAtUtc = nowUtc.ToUniversalTime();
        ProtectedToken = null;
    }

    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.") : value.Trim();
}

public sealed class InvalidMenuStateException(string message) : InvalidOperationException(message);

public sealed class PublishedMenu
{
    private PublishedMenu() { }
    public PublishedMenu(Guid id, Guid tenantId, Guid branchId, string name, DateTimeOffset? publishedAtUtc = null)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        Name = Required(name);
        PublishedAtUtc = publishedAtUtc?.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Name { get; private set; } = null!;
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public ICollection<MenuCategory> Categories { get; private set; } = new List<MenuCategory>();
    public ICollection<MenuItem> Items { get; private set; } = new List<MenuItem>();

    public bool IsArchived => ArchivedAtUtc is not null;
    public bool IsPublished => PublishedAtUtc is not null && !IsArchived;
    public string Lifecycle =>
        IsArchived ? "archived" : IsPublished ? "published" : "draft";

    public void Rename(string name)
    {
        EnsureEditable();
        Name = Required(name);
    }

    public void Publish(DateTimeOffset nowUtc)
    {
        EnsureEditable();
        PublishedAtUtc = nowUtc.ToUniversalTime();
        ArchivedAtUtc = null;
    }

    public void Unpublish()
    {
        EnsureEditable();
        PublishedAtUtc = null;
    }

    public void Archive(DateTimeOffset nowUtc)
    {
        if (IsArchived)
        {
            return;
        }

        ArchivedAtUtc = nowUtc.ToUniversalTime();
        PublishedAtUtc = null;
    }

    public void EnsureEditable()
    {
        if (IsArchived)
        {
            throw new InvalidMenuStateException("An archived menu cannot be changed.");
        }
    }

    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Name is required.") : value.Trim();
}

public sealed class MenuCategory
{
    private MenuCategory() { }
    public MenuCategory(Guid id, Guid tenantId, Guid branchId, Guid menuId, string name, int sortOrder) =>
        (Id, TenantId, BranchId, MenuId, Name, SortOrder) =
            (id, tenantId, branchId, menuId, Required(name), sortOrder);
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid MenuId { get; private set; }
    public string Name { get; private set; } = null!;
    public int SortOrder { get; private set; }
    public void Rename(string name) => Name = Required(name);
    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder;
    private static string Required(string value) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Name is required.") : value.Trim();
}

public sealed class MenuItem
{
    private MenuItem() { }
    public MenuItem(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid menuId,
        Guid categoryId,
        string name,
        string description,
        Money price,
        bool isAvailable,
        int sortOrder,
        string? imageUrl = null,
        string? imageAlt = null,
        int? prepTimeSeconds = null)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        MenuId = menuId;
        CategoryId = categoryId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.") : name.Trim();
        Description = description.Trim();
        PriceAmountMinor = price.AmountMinor;
        PriceCurrency = price.Currency;
        IsAvailable = isAvailable;
        SortOrder = sortOrder;
        SetImage(imageUrl, imageAlt);
        SetPrepTimeSeconds(prepTimeSeconds);
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid MenuId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public long PriceAmountMinor { get; private set; }
    public string PriceCurrency { get; private set; } = null!;
    public Money Price => new(PriceAmountMinor, PriceCurrency);
    public bool IsAvailable { get; private set; }
    public int SortOrder { get; private set; }
    public string ImageUrl { get; private set; } = string.Empty;
    public string ImageAlt { get; private set; } = string.Empty;
    /// <summary>Optional kitchen prep duration; null means use the order default ETA.</summary>
    public int? PrepTimeSeconds { get; private set; }

    public void Update(
        string name,
        string description,
        Money price,
        bool isAvailable,
        int sortOrder,
        Guid categoryId,
        string? imageUrl = null,
        string? imageAlt = null,
        int? prepTimeSeconds = null,
        bool updatePrepTime = false)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.") : name.Trim();
        Description = description.Trim();
        PriceAmountMinor = price.AmountMinor;
        PriceCurrency = price.Currency;
        IsAvailable = isAvailable;
        SortOrder = sortOrder;
        CategoryId = categoryId;
        if (imageUrl is not null || imageAlt is not null)
        {
            SetImage(imageUrl ?? ImageUrl, imageAlt ?? ImageAlt);
        }

        if (updatePrepTime)
        {
            SetPrepTimeSeconds(prepTimeSeconds);
        }
    }

    public void SetPrepTimeSeconds(int? prepTimeSeconds)
    {
        if (prepTimeSeconds is null)
        {
            PrepTimeSeconds = null;
            return;
        }

        if (prepTimeSeconds is < 1 or > 86_400)
        {
            throw new ArgumentOutOfRangeException(nameof(prepTimeSeconds), "Prep time must be between 1 second and 24 hours.");
        }

        PrepTimeSeconds = prepTimeSeconds;
    }

    public void SetImage(string? imageUrl, string? imageAlt)
    {
        ImageUrl = NormalizeImageUrl(imageUrl);
        ImageAlt = (imageAlt ?? string.Empty).Trim();
        if (ImageAlt.Length > 200)
        {
            throw new ArgumentException("Image alt text is too long.");
        }
    }

    private static string NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        var value = imageUrl.Trim();
        if (value.Length > 2048)
        {
            throw new ArgumentException("Image URL is too long.");
        }

        if (value.StartsWith('/'))
        {
            return value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Image URL must be absolute http(s) or a rooted path.");
        }

        return uri.AbsoluteUri;
    }
}

public sealed class MenuCategoryTranslation
{
    private MenuCategoryTranslation() { }

    public MenuCategoryTranslation(Guid categoryId, Guid tenantId, Guid branchId, string locale, string name)
    {
        CategoryId = categoryId;
        TenantId = tenantId;
        BranchId = branchId;
        Locale = SupportedLocales.Normalize(locale);
        SetName(name);
    }

    public Guid CategoryId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Locale { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public void SetName(string name) =>
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.") : name.Trim();
}

public sealed class MenuItemTranslation
{
    private MenuItemTranslation() { }

    public MenuItemTranslation(
        Guid itemId,
        Guid tenantId,
        Guid branchId,
        string locale,
        string name,
        string description)
    {
        ItemId = itemId;
        TenantId = tenantId;
        BranchId = branchId;
        Locale = SupportedLocales.Normalize(locale);
        SetText(name, description);
    }

    public Guid ItemId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public string Locale { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;

    public void SetText(string name, string description)
    {
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.") : name.Trim();
        Description = (description ?? string.Empty).Trim();
    }
}

public sealed class CustomerSession
{
    private CustomerSession() { }
    public CustomerSession(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        string tokenHash,
        string locale,
        DateTimeOffset createdAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Expiry must be after creation.");
        }

        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        TableId = tableId;
        TokenHash = string.IsNullOrWhiteSpace(tokenHash) ? throw new ArgumentException("Token hash is required.") : tokenHash;
        Locale = SupportedLocales.Normalize(locale);
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        ExpiresAtUtc = expiresAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid TableId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public string Locale { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
}

public sealed class CustomerOrder
{
    private CustomerOrder() { }
    public CustomerOrder(
        Guid id,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        Guid customerSessionId,
        string idempotencyKey,
        string requestHash,
        string displayNumber,
        Money total,
        DateTimeOffset createdAtUtc,
        DateTimeOffset estimatedReadyAtUtc)
    {
        Id = id;
        TenantId = tenantId;
        BranchId = branchId;
        TableId = tableId;
        CustomerSessionId = customerSessionId;
        IdempotencyKey = idempotencyKey;
        RequestHash = requestHash;
        DisplayNumber = displayNumber;
        TotalAmountMinor = total.AmountMinor;
        TotalCurrency = total.Currency;
        Status = OrderStatus.Submitted;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        StatusChangedAtUtc = CreatedAtUtc;
        EstimatedReadyAtUtc = estimatedReadyAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid TableId { get; private set; }
    public Guid CustomerSessionId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public string RequestHash { get; private set; } = null!;
    public string DisplayNumber { get; private set; } = null!;
    public long TotalAmountMinor { get; private set; }
    public string TotalCurrency { get; private set; } = null!;
    public Money Total => new(TotalAmountMinor, TotalCurrency);
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset StatusChangedAtUtc { get; private set; }
    public DateTimeOffset EstimatedReadyAtUtc { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public ICollection<CustomerOrderItem> Items { get; private set; } = new List<CustomerOrderItem>();

    public void ChangeStatus(OrderStatus nextStatus, DateTimeOffset changedAtUtc)
    {
        if (!CanTransition(Status, nextStatus))
        {
            throw new InvalidOrderStatusTransitionException(Status, nextStatus);
        }

        Status = nextStatus;
        StatusChangedAtUtc = changedAtUtc.ToUniversalTime();
    }

    public void SetEstimatedReadyAt(DateTimeOffset estimatedReadyAtUtc) =>
        EstimatedReadyAtUtc = estimatedReadyAtUtc.ToUniversalTime();

    /// <summary>
    /// Forward jumps are allowed (skipped steps count as done). Backward moves are not.
    /// </summary>
    public static bool CanTransition(OrderStatus current, OrderStatus next)
    {
        if (current == next)
        {
            return false;
        }

        if (current is OrderStatus.Completed or OrderStatus.Cancelled)
        {
            return false;
        }

        if (next == OrderStatus.Cancelled)
        {
            return current is OrderStatus.Submitted or OrderStatus.Accepted or OrderStatus.Preparing;
        }

        if (next is OrderStatus.Accepted or OrderStatus.Preparing or OrderStatus.Ready or OrderStatus.Completed)
        {
            return (int)next > (int)current;
        }

        return false;
    }
}

public sealed class InvalidOrderStatusTransitionException(OrderStatus current, OrderStatus next)
    : InvalidOperationException($"Order status cannot transition from {current} to {next}.")
{
    public OrderStatus Current { get; } = current;
    public OrderStatus Next { get; } = next;
}

public sealed class CustomerOrderItem
{
    private CustomerOrderItem() { }
    public CustomerOrderItem(Guid id, Guid orderId, Guid menuItemId, string name, Money unitPrice, int quantity, string? note)
    {
        if (quantity is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        Id = id;
        OrderId = orderId;
        MenuItemId = menuItemId;
        Name = name;
        UnitPriceAmountMinor = unitPrice.AmountMinor;
        UnitPriceCurrency = unitPrice.Currency;
        Quantity = quantity;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid MenuItemId { get; private set; }
    public string Name { get; private set; } = null!;
    public long UnitPriceAmountMinor { get; private set; }
    public string UnitPriceCurrency { get; private set; } = null!;
    public Money UnitPrice => new(UnitPriceAmountMinor, UnitPriceCurrency);
    public int Quantity { get; private set; }
    public string? Note { get; private set; }
}
