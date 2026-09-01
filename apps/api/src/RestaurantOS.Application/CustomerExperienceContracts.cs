namespace RestaurantOS.Application;

public interface ICustomerExperienceService
{
    Task<CustomerSessionResult> ResolveQrAsync(
        string qrToken,
        string locale,
        CancellationToken cancellationToken,
        CustomerClientContext? client = null);

    Task<CustomerOrderResult> CreateOrderAsync(
        string sessionToken,
        string idempotencyKey,
        IReadOnlyCollection<CreateOrderLine> lines,
        CancellationToken cancellationToken,
        CustomerClientContext? client = null);

    Task<CustomerOrderResult> GetOrderAsync(
        string sessionToken,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<CustomerOrderResult> ChangeOrderStatusAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        string status,
        CancellationToken cancellationToken);

    Task<CustomerOrderResult> ChangeOrderStatusAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        string status,
        Guid actorUserId,
        CancellationToken cancellationToken);

    Task<CustomerOrderResult> ChangeOrderStatusAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        string status,
        DateTimeOffset expectedStatusChangedAtUtc,
        Guid actorUserId,
        CancellationToken cancellationToken,
        DateTimeOffset? estimatedReadyAtUtc = null);

    Task<bool> CanAccessOrderAsync(
        string sessionToken,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<ServiceRequestResult> CreateServiceRequestAsync(
        string sessionToken,
        string type,
        string? note,
        CancellationToken cancellationToken);
}

public interface IOrderStatusNotifier
{
    Task NotifyAsync(CustomerOrderResult order, CancellationToken cancellationToken);
}

public sealed record CustomerSessionResult(
    string SessionToken,
    string RestaurantName,
    string BranchName,
    string TableLabel,
    string Locale,
    IReadOnlyList<MenuCategoryResult> Categories,
    IReadOnlyList<MenuItemResult> Products,
    IReadOnlyList<string> OpenServiceRequestTypes,
    IReadOnlyList<CustomerOrderResult> ActiveOrders,
    CustomerMenuSettingsData CustomerMenuSettings);

public sealed record MenuCategoryResult(Guid Id, string Name);

public sealed record MenuItemResult(
    Guid Id,
    Guid CategoryId,
    string Name,
    string Description,
    long AmountMinor,
    string Currency,
    bool Available,
    string ImageUrl = "",
    string ImageAlt = "",
    long ListAmountMinor = 0,
    long DiscountAmountMinor = 0,
    string? PromotionLabel = null,
    MenuItemCatalogData Catalog = default!);

public sealed record CreateOrderLine(
    Guid ProductId,
    int Quantity,
    string? Note,
    IReadOnlyList<string> ModifierOptionIds);

public sealed record CustomerOrderResult(
    Guid Id,
    string DisplayNumber,
    string Status,
    DateTimeOffset StatusChangedAtUtc,
    DateTimeOffset EstimatedReadyAtUtc,
    long AmountMinor,
    string Currency,
    DateTimeOffset? CreatedAtUtc = null,
    long SubtotalAmountMinor = 0,
    long DiscountAmountMinor = 0);

public sealed record ServiceRequestResult(
    Guid Id,
    Guid TableId,
    string TableLabel,
    string Type,
    string Status,
    string? Note,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed class CustomerExperienceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
