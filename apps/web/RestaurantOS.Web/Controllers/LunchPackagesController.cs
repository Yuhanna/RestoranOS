using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class LunchPackagesController(IWebApiExecuter api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        var workspace = await api.GetWorkspaceAsync(cancellationToken);
        if (workspace?.CanEditMenus != true)
        {
            TempData["Error"] = "Günün menüsü için menü düzenleme yetkiniz yok.";
            return RedirectToAction("Index", "Dashboard");
        }

        try
        {
            return View(await BuildPageModelAsync(new CreateLunchPackageViewModel(), cancellationToken));
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new LunchPackagesPageViewModel());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreateLunchPackageViewModel model,
        CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        if (model.ComponentMenuItemIds is null || model.ComponentMenuItemIds.Length < 2)
        {
            ModelState.AddModelError(string.Empty, "En az 2 ürün seçin (ör. ana yemek + pilav + içecek).");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }

        try
        {
            var priceMinor = (long)Math.Round(
                decimal.Parse(model.PriceLira.Replace(',', '.'), CultureInfo.InvariantCulture) * 100,
                MidpointRounding.AwayFromZero);
            var components = model.ComponentMenuItemIds!
                .Select((id, index) => new
                {
                    menuItemId = id,
                    slotLabel = model.ComponentSlotLabels?.ElementAtOrDefault(index),
                    sortOrder = index,
                })
                .ToArray();

            await api.InvokePostAsync<LunchPackageListItemViewModel>(
                "/api/v1/management/lunch-packages",
                new
                {
                    name = model.Name.Trim(),
                    description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                    priceAmountMinor = priceMinor,
                    currency = "TRY",
                    isActive = model.IsActive,
                    sortOrder = model.SortOrder,
                    dailyStartLocal = string.IsNullOrWhiteSpace(model.DailyStartLocal) ? null : model.DailyStartLocal,
                    dailyEndLocal = string.IsNullOrWhiteSpace(model.DailyEndLocal) ? null : model.DailyEndLocal,
                    daysOfWeekMask = ToDaysOfWeekMask(model.WeekdayBits),
                    components,
                },
                cancellationToken);
            TempData["Message"] =
                $"«{model.Name}» günün menüsü kaydedildi. Aktif ve saat aralığındayken QR menüde görünür.";
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
            await api.InvokePatchAsync<LunchPackageListItemViewModel>(
                $"/api/v1/management/lunch-packages/{id}/active",
                new { isActive = !isActive },
                cancellationToken);
            TempData["Message"] = isActive ? "Menü pasifleştirildi." : "Menü etkinleştirildi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        try
        {
            await api.InvokeDeleteAsync($"/api/v1/management/lunch-packages/{id}", cancellationToken);
            TempData["Message"] = "Günün menüsü silindi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<LunchPackagesPageViewModel> BuildPageModelAsync(
        CreateLunchPackageViewModel create,
        CancellationToken cancellationToken)
    {
        var packagesTask = api.InvokeGetAsync<List<LunchPackageListItemViewModel>>(
            "/api/v1/management/lunch-packages",
            cancellationToken);
        var menusTask = api.InvokeGetAsync<List<MenuSummaryViewModel>>(
            "/api/v1/management/menus",
            cancellationToken);
        await Task.WhenAll(packagesTask, menusTask);

        MenuDetailViewModel? publishedMenu = null;
        var menus = await menusTask ?? [];
        var published = menus.FirstOrDefault(menu => menu.Lifecycle == "published") ?? menus.FirstOrDefault();
        if (published is not null)
        {
            publishedMenu = await api.InvokeGetAsync<MenuDetailViewModel>(
                $"/api/v1/management/menus/{published.Id}",
                cancellationToken);
        }

        return new LunchPackagesPageViewModel
        {
            Packages = await packagesTask ?? [],
            Create = create,
            PublishedMenu = publishedMenu,
        };
    }

    private static byte? ToDaysOfWeekMask(int[]? bits)
    {
        if (bits is null || bits.Length == 0 || bits.Length >= 7)
        {
            return null;
        }

        byte mask = 0;
        foreach (var bit in bits.Distinct())
        {
            if (bit is >= 0 and <= 6)
            {
                mask |= (byte)(1 << bit);
            }
        }

        return mask is 0 or 0b0111_1111 ? null : mask;
    }

    private bool EnsureAuthenticated() => api.IsAuthenticated;

    private RedirectToActionResult ChallengeLogin() =>
        RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Index)) });
}
