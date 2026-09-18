using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Admin.Data;
using RestaurantOS.Admin.Filters;
using RestaurantOS.Admin.Models;

namespace RestaurantOS.Admin.Controllers;

[RequirePlatformSession]
public sealed class HomeController(IPlatformApi api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            var catalog = await api.GetAsync<CatalogResponse>("/api/v1/platform/catalog", cancellationToken);
            var offers = await api.GetAsync<List<CampaignResponse>>("/api/v1/platform/subscription-offers", cancellationToken)
                ?? [];
            var notifications = await api.GetAsync<List<NotificationResponse>>("/api/v1/platform/notifications", cancellationToken)
                ?? [];
            var staff = await TryGetAsync(() =>
                api.GetAsync<List<StaffResponse>>("/api/v1/platform/staff", cancellationToken))
                ?? [];
            var openQuotes = await TryGetAsync(() =>
                api.GetAsync<List<QuoteListItemResponse>>(
                    "/api/v1/platform/quotes?status=Open",
                    cancellationToken))
                ?? [];
            var published = catalog?.Products
                .SelectMany(product => product.Prices)
                .Count(price => string.Equals(price.Status, "published", StringComparison.OrdinalIgnoreCase)) ?? 0;

            return View(new OverviewViewModel
            {
                Email = api.CurrentToken?.Email,
                RoleCode = api.CurrentToken?.RoleCode,
                PublishedPriceCount = published,
                OfferCount = offers.Count(offer => offer.IsActive),
                ActiveNotificationCount = notifications.Count(item => item.IsActive),
                OperatorCount = staff.Count(member => member.IsActive),
                OpenQuoteCount = openQuotes.Count,
            });
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View(new OverviewViewModel
            {
                Email = api.CurrentToken?.Email,
                RoleCode = api.CurrentToken?.RoleCode,
            });
        }
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View();

    private async Task<T?> TryGetAsync<T>(Func<Task<T?>> load)
    {
        try
        {
            return await load();
        }
        catch (WebApiException exception) when (exception.StatusCode != StatusCodes.Status401Unauthorized)
        {
            TempData["Error"] ??= exception.Message;
            return default;
        }
    }
}
