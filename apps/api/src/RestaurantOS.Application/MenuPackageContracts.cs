namespace RestaurantOS.Application;

public interface IMenuPackageManagementService
{
    Task<IReadOnlyList<MenuPackageResult>> ListAsync(
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<MenuPackageResult> CreateAsync(
        Guid tenantId,
        Guid branchId,
        UpsertMenuPackageCommand command,
        CancellationToken cancellationToken);

    Task<MenuPackageResult> UpdateAsync(
        Guid tenantId,
        Guid branchId,
        Guid packageId,
        UpsertMenuPackageCommand command,
        CancellationToken cancellationToken);

    Task<MenuPackageResult> SetActiveAsync(
        Guid tenantId,
        Guid branchId,
        Guid packageId,
        bool isActive,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid tenantId,
        Guid branchId,
        Guid packageId,
        CancellationToken cancellationToken);
}

public sealed record MenuPackageComponentInput(Guid MenuItemId, string? SlotLabel, int SortOrder);

public sealed record UpsertMenuPackageCommand(
    string Name,
    string? Description,
    long PriceAmountMinor,
    string Currency,
    bool IsActive,
    int SortOrder,
    TimeOnly? DailyStartLocal,
    TimeOnly? DailyEndLocal,
    byte? DaysOfWeekMask,
    IReadOnlyList<MenuPackageComponentInput> Components);

public sealed record MenuPackageComponentResult(
    Guid Id,
    Guid MenuItemId,
    string MenuItemName,
    string? SlotLabel,
    int SortOrder,
    long ListAmountMinor);

public sealed record MenuPackageResult(
    Guid Id,
    string Name,
    string? Description,
    long PriceAmountMinor,
    string Currency,
    bool IsActive,
    int SortOrder,
    TimeOnly? DailyStartLocal,
    TimeOnly? DailyEndLocal,
    byte? DaysOfWeekMask,
    long ComponentsListTotalMinor,
    IReadOnlyList<MenuPackageComponentResult> Components);
