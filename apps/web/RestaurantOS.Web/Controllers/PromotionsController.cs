using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class PromotionsController(IWebApiExecuter api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        try
        {
            return View(await BuildPageModelAsync(new CreateMenuPromotionViewModel(), cancellationToken));
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new PromotionsPageViewModel());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreateMenuPromotionViewModel model,
        CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }

        try
        {
            var discountValue = ParseDiscountValue(model.DiscountKind, model.DiscountValue);
            await api.InvokePostAsync<MenuPromotionListItemViewModel>(
                "/api/v1/management/promotions/menu",
                new
                {
                    name = model.Name.Trim(),
                    scope = model.Scope,
                    discountKind = model.DiscountKind,
                    discountValue,
                    startsAtUtc = DateTimeOffset.UtcNow,
                    endsAtUtc = model.EndsAtLocal?.ToUniversalTime(),
                    dailyStartLocal = ParseTime(model.DailyStartLocal),
                    dailyEndLocal = ParseTime(model.DailyEndLocal),
                    categoryId = model.Scope == "category" ? model.CategoryId : null,
                    menuItemId = model.Scope == "product" ? model.MenuItemId : null,
                    isActive = true,
                },
                cancellationToken);
            TempData["Message"] = $"«{model.Name}» kampanyası oluşturuldu. QR menüde otomatik uygulanır.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        try
        {
            await api.InvokePatchAsync<MenuPromotionListItemViewModel>(
                $"/api/v1/management/promotions/menu/{id}",
                new { isActive = !isActive },
                cancellationToken);
            TempData["Message"] = isActive ? "Kampanya durduruldu." : "Kampanya yeniden etkinleştirildi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<PromotionsPageViewModel> BuildPageModelAsync(
        CreateMenuPromotionViewModel create,
        CancellationToken cancellationToken)
    {
        var promotions = await api.InvokeGetAsync<List<MenuPromotionListItemViewModel>>(
            "/api/v1/management/promotions/menu",
            cancellationToken) ?? [];

        MenuDetailViewModel? publishedMenu = null;
        var menus = await api.InvokeGetAsync<List<MenuSummaryViewModel>>(
            "/api/v1/management/menus",
            cancellationToken) ?? [];
        var published = menus.FirstOrDefault(menu => menu.Lifecycle == "published") ?? menus.FirstOrDefault();
        if (published is not null)
        {
            publishedMenu = await api.InvokeGetAsync<MenuDetailViewModel>(
                $"/api/v1/management/menus/{published.Id}",
                cancellationToken);
        }

        return new PromotionsPageViewModel
        {
            Promotions = promotions,
            Create = create,
            PublishedMenu = publishedMenu,
        };
    }

    private static int ParseDiscountValue(string kind, string raw)
    {
        if (kind == "fixed_minor")
        {
            var amount = decimal.Parse(raw.Replace(',', '.'), CultureInfo.InvariantCulture);
            return (int)Math.Round(amount * 100, MidpointRounding.AwayFromZero);
        }

        return int.Parse(raw, CultureInfo.InvariantCulture);
    }

    private static string? ParseTime(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private bool EnsureAuthenticated() => api.IsAuthenticated;

    private RedirectToActionResult ChallengeLogin() =>
        RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Index)) });
}
