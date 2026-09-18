using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

public sealed partial class CustomerController
{
    private MenuProductResponse ToProductResponse(MenuItemResult item) =>
        MenuCatalogMapper.ToProductResponse(item, MenuCatalogMapper.ToCustomerMediaUrl);

    private static CustomerMenuSettingsResponse ToSettingsResponse(CustomerMenuSettingsData settings) =>
        MenuCatalogMapper.ToSettingsResponse(settings);

    private static MenuPackageResponse ToPackageResponse(CustomerMenuPackageResult package)
    {
        var currency = string.IsNullOrWhiteSpace(package.Currency) ? "TRY" : package.Currency;
        return new MenuPackageResponse(
            package.Id.ToString(),
            package.Name,
            package.Description,
            new MoneyResponse(package.PriceAmountMinor, currency),
            new MoneyResponse(package.ListTotalAmountMinor, currency),
            new MoneyResponse(package.DiscountAmountMinor, currency),
            package.Components.Select(component => new MenuPackageComponentResponse(
                component.MenuItemId.ToString(),
                component.Name,
                component.SlotLabel,
                component.ListAmountMinor,
                MenuCatalogMapper.ToCustomerMediaUrl(component.ImageUrl),
                component.ImageAlt,
                component.Description)).ToArray(),
            package.DailyStartLocal,
            package.DailyEndLocal);
    }
}
