namespace RestaurantOS.Api.Models.Dto;

public sealed record ResolveQrRequest(string QrToken, string Locale = "tr");

public sealed record CustomerSessionResponse(
    string SessionToken,
    string RestaurantName,
    string BranchName,
    string TableLabel,
    string Locale,
    IReadOnlyList<MenuCategoryResponse> Categories,
    IReadOnlyList<MenuProductResponse> Products,
    IReadOnlyList<string> OpenServiceRequestTypes,
    IReadOnlyList<CustomerOrderResponse> ActiveOrders);

public sealed record MenuCategoryResponse(string Id, string Name);

public sealed record MoneyResponse(long AmountMinor, string Currency);

public sealed record PriceBreakdownResponse(
    MoneyResponse List,
    MoneyResponse Discount,
    MoneyResponse Final);

public sealed record MenuProductResponse(
    string Id,
    string CategoryId,
    string Name,
    string Description,
    MoneyResponse Price,
    string ImageUrl,
    string ImageAlt,
    bool Available,
    string[] DietaryTags,
    string[] Allergens,
    object[] ModifierGroups,
    PriceBreakdownResponse? Pricing = null,
    string? PromotionLabel = null);

public sealed record CreateCustomerOrderRequest(
    string SessionToken,
    IReadOnlyList<CreateCustomerOrderLineRequest> Lines);

public sealed record CreateCustomerOrderLineRequest(
    string ProductId,
    int Quantity,
    IReadOnlyList<string>? ModifierOptionIds,
    string? Note);

public sealed record CustomerOrderResponse(
    string Id,
    string DisplayNumber,
    string Status,
    DateTimeOffset StatusChangedAt,
    DateTimeOffset EstimatedReadyAt,
    MoneyResponse Total,
    MoneyResponse? Subtotal = null,
    MoneyResponse? Discount = null);

public sealed record CreateServiceRequestRequest(string SessionToken, string Type, string? Note = null);

public sealed record CustomerServiceRequestResponse(
    string Id,
    string TableId,
    string TableLabel,
    string Type,
    string Status,
    string? Note,
    DateTimeOffset CreatedAtUtc);
