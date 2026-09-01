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
}
