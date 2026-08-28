using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class MenusController(IWebApiExecuter api, IOptions<ApiOptions> apiOptions) : Controller
{
    private readonly string _apiBaseUrl = apiOptions.Value.BaseUrl.TrimEnd('/');

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Index)) });
        }

        try
        {
            var menus = await api.InvokeGetAsync<List<MenuSummaryViewModel>>(
                "/api/v1/management/menus",
                cancellationToken) ?? [];
            return View(menus);
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new List<MenuSummaryViewModel>());
        }
    }

    [HttpGet]
    public IActionResult Create()
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        return View(new CreateMenuViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateMenuViewModel model, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var created = await api.InvokePostAsync<MenuSummaryViewModel>(
                "/api/v1/management/menus",
                new { name = model.Name },
                cancellationToken);
            if (created is null)
            {
                TempData["Error"] = "Menü oluşturulamadı.";
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Details), new { id = created.Id });
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            var detail = await LoadDetailAsync(id, cancellationToken);
            if (detail is null)
            {
                return NotFound();
            }

            await ApplyEntitlementFlagsAsync(cancellationToken);
            return View(BuildEditor(detail));
        }
        catch (WebApiException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
        {
            return NotFound();
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<MenuSummaryViewModel>(
                $"/api/v1/management/menus/{id}/publish",
                null,
                cancellationToken);
            TempData["Message"] = "Menü yayınlandı.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<MenuSummaryViewModel>(
                $"/api/v1/management/menus/{id}/unpublish",
                null,
                cancellationToken);
            TempData["Message"] = "Menü yayından alındı.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<MenuSummaryViewModel>(
                $"/api/v1/management/menus/{id}/archive",
                null,
                cancellationToken);
            TempData["Message"] = "Menü arşivlendi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(
        [Bind(Prefix = nameof(MenuEditorViewModel.NewCategory))] AddCategoryViewModel model,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Kategori bilgilerini kontrol edin.";
            return RedirectToAction(nameof(Details), new { id = model.MenuId });
        }

        try
        {
            await api.InvokePostAsync<object>(
                $"/api/v1/management/menus/{model.MenuId}/categories",
                new { name = model.Name.Trim(), sortOrder = model.SortOrder },
                cancellationToken);
            TempData["Message"] = "Kategori eklendi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.MenuId });
    }

    [HttpGet]
    public async Task<IActionResult> EditCategory(Guid id, Guid menuId, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        var detail = await LoadDetailAsync(menuId, cancellationToken);
        var category = detail?.Categories.FirstOrDefault(c => c.Id == id);
        if (category is null)
        {
            return NotFound();
        }

        return View(new EditCategoryViewModel
        {
            Id = category.Id,
            MenuId = menuId,
            Name = category.Name,
            SortOrder = category.SortOrder,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(EditCategoryViewModel model, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await api.InvokePatchAsync<object>(
                $"/api/v1/management/menu-categories/{model.Id}",
                new { name = model.Name.Trim(), sortOrder = model.SortOrder },
                cancellationToken);
            TempData["Message"] = "Kategori güncellendi.";
            return RedirectToAction(nameof(Details), new { id = model.MenuId });
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3_000_000)]
    public async Task<IActionResult> AddItem(
        [Bind(Prefix = nameof(MenuEditorViewModel.NewItem))] AddMenuItemViewModel model,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Ürün bilgilerini kontrol edin.";
            return RedirectToAction(nameof(Details), new { id = model.MenuId });
        }

        var amountMinor = (long)Math.Round(model.PriceTry * 100m, MidpointRounding.AwayFromZero);

        try
        {
            await ApplyEntitlementFlagsAsync(cancellationToken);
            var canUseImages = ViewData["CanUseProductImages"] as bool? == true;
            var created = await api.InvokePostAsync<MenuItemViewModel>(
                $"/api/v1/management/menus/{model.MenuId}/items",
                new
                {
                    categoryId = model.CategoryId,
                    name = model.Name.Trim(),
                    description = model.Description?.Trim() ?? string.Empty,
                    amountMinor,
                    isAvailable = model.IsAvailable,
                    sortOrder = model.SortOrder,
                    imageUrl = canUseImages && !string.IsNullOrWhiteSpace(model.ImageUrl)
                        ? model.ImageUrl.Trim()
                        : null,
                    imageAlt = canUseImages ? model.ImageAlt?.Trim() : null,
                    prepTimeSeconds = ToPrepTimeSeconds(model.PrepMinutes, model.PrepSeconds),
                },
                cancellationToken);

            if (canUseImages && created is not null && model.Photo is { Length: > 0 })
            {
                await api.InvokePostMultipartAsync<object>(
                    $"/api/v1/management/menu-items/{created.Id}/image",
                    model.Photo,
                    model.ImageAlt,
                    cancellationToken);
            }

            TempData["Message"] = "Ürün eklendi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Details), new { id = model.MenuId });
    }

    [HttpGet]
    public async Task<IActionResult> EditItem(Guid id, Guid menuId, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        var detail = await LoadDetailAsync(menuId, cancellationToken);
        var item = detail?.Items.FirstOrDefault(i => i.Id == id);
        if (detail is null || item is null)
        {
            return NotFound();
        }

        await ApplyEntitlementFlagsAsync(cancellationToken);
        return View(new EditMenuItemViewModel
        {
            Id = item.Id,
            MenuId = menuId,
            CategoryId = item.CategoryId,
            Name = item.Name,
            Description = item.Description,
            PriceTry = item.AmountMinor / 100m,
            IsAvailable = item.IsAvailable,
            SortOrder = item.SortOrder,
            ImageUrl = item.ImageUrl,
            ImageAlt = item.ImageAlt,
            PrepMinutes = item.PrepTimeSeconds is int s && s > 0 ? s / 60 : null,
            PrepSeconds = item.PrepTimeSeconds is int rem && rem > 0 ? rem % 60 : null,
            PreviewImageUrl = ResolveImageUrl(item.ImageUrl),
            Categories = detail.Categories.OrderBy(c => c.SortOrder).ToList(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(3_000_000)]
    public async Task<IActionResult> EditItem(EditMenuItemViewModel model, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        var detail = await LoadDetailAsync(model.MenuId, cancellationToken);
        model.Categories = detail?.Categories.OrderBy(c => c.SortOrder).ToList() ?? [];
        model.PreviewImageUrl = ResolveImageUrl(model.ImageUrl);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var amountMinor = (long)Math.Round(model.PriceTry * 100m, MidpointRounding.AwayFromZero);

        try
        {
            await ApplyEntitlementFlagsAsync(cancellationToken);
            var canUseImages = ViewData["CanUseProductImages"] as bool? == true;
            await api.InvokePatchAsync<object>(
                $"/api/v1/management/menu-items/{model.Id}",
                new
                {
                    categoryId = model.CategoryId,
                    name = model.Name.Trim(),
                    description = model.Description?.Trim() ?? string.Empty,
                    amountMinor,
                    isAvailable = model.IsAvailable,
                    sortOrder = model.SortOrder,
                    imageUrl = canUseImages ? model.ImageUrl?.Trim() ?? string.Empty : null,
                    imageAlt = canUseImages ? model.ImageAlt?.Trim() ?? string.Empty : null,
                    prepTimeSeconds = ToPrepTimeSeconds(model.PrepMinutes, model.PrepSeconds),
                    updatePrepTime = true,
                },
                cancellationToken);

            if (canUseImages && model.Photo is { Length: > 0 })
            {
                await api.InvokePostMultipartAsync<object>(
                    $"/api/v1/management/menu-items/{model.Id}/image",
                    model.Photo,
                    model.ImageAlt,
                    cancellationToken);
            }

            TempData["Message"] = "Ürün güncellendi.";
            return RedirectToAction(nameof(Details), new { id = model.MenuId });
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            await ApplyEntitlementFlagsAsync(cancellationToken);
            return View(model);
        }
    }

    private async Task ApplyEntitlementFlagsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await api.InvokeGetAsync<WorkspaceViewModel>(
                "/api/v1/management/workspace",
                cancellationToken);
            ViewData["CanUseProductImages"] = workspace?.Entitlements?.CanUseProductImages ?? false;
            ViewData["PlanDisplayName"] = workspace?.Entitlements?.PlanDisplayName ?? "Free";
        }
        catch (WebApiException)
        {
            ViewData["CanUseProductImages"] = false;
            ViewData["PlanDisplayName"] = "Free";
        }
    }

    private async Task<MenuDetailViewModel?> LoadDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var detail = await api.InvokeGetAsync<MenuDetailViewModel>(
            $"/api/v1/management/menus/{id}",
            cancellationToken);
        if (detail is null)
        {
            return null;
        }

        foreach (var item in detail.Items)
        {
            item.DisplayImageUrl = ResolveImageUrl(item.ImageUrl);
        }

        return detail;
    }

    private string ResolveImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
        {
            return imageUrl;
        }

        return imageUrl.StartsWith('/')
            ? $"{_apiBaseUrl}{imageUrl}"
            : imageUrl;
    }

    private static int? ToPrepTimeSeconds(int? minutes, int? seconds)
    {
        if (minutes is null && seconds is null)
        {
            return null;
        }

        var total = checked((minutes ?? 0) * 60 + (seconds ?? 0));
        return total > 0 ? total : null;
    }

    private static MenuEditorViewModel BuildEditor(MenuDetailViewModel detail)
    {
        var nextCategoryOrder = detail.Categories.Count == 0
            ? 1
            : detail.Categories.Max(c => c.SortOrder) + 1;
        var nextItemOrder = detail.Items.Count == 0
            ? 1
            : detail.Items.Max(i => i.SortOrder) + 1;

        return new MenuEditorViewModel
        {
            Menu = detail,
            NewCategory = new AddCategoryViewModel
            {
                MenuId = detail.Id,
                SortOrder = nextCategoryOrder,
            },
            NewItem = new AddMenuItemViewModel
            {
                MenuId = detail.Id,
                CategoryId = detail.Categories.OrderBy(c => c.SortOrder).FirstOrDefault()?.Id ?? Guid.Empty,
                SortOrder = nextItemOrder,
                IsAvailable = true,
            },
        };
    }
}
