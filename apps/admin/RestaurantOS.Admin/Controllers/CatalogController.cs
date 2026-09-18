using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Admin.Data;
using RestaurantOS.Admin.Filters;
using RestaurantOS.Admin.Models;

namespace RestaurantOS.Admin.Controllers;

[RequirePlatformSession]
public sealed class CatalogController(IPlatformApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            return View(await BuildPageAsync(new CreatePriceViewModel(), cancellationToken));
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View(new CatalogPageViewModel { RoleCode = api.CurrentToken?.RoleCode });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreatePriceViewModel model,
        CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanWriteCatalog(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Bu rol katalog yazamaz.";
            return RedirectToAction(nameof(Index));
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageAsync(model, cancellationToken));
        }

        if (!AdminMoney.TryToMinorUnits(model.ProductCode, model.Amount, out var amountMinor, out var amountError))
        {
            TempData["Error"] = amountError;
            return View("Index", await BuildPageAsync(model, cancellationToken));
        }

        try
        {
            await api.PostAsync<PlanPriceResponse>(
                "/api/v1/platform/catalog/prices",
                new
                {
                    productCode = model.ProductCode,
                    interval = model.Interval,
                    amountMinor,
                    currency = "TRY",
                    taxInclusive = true,
                },
                cancellationToken);
            TempData["Message"] = "Taslak fiyat oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View("Index", await BuildPageAsync(model, cancellationToken));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanPublishCatalog(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Yalnızca Owner yayınlayabilir.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.PostAsync<object>($"/api/v1/platform/catalog/prices/{id}/publish", null, cancellationToken);
            TempData["Message"] = "Fiyat yayınlandı.";
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, string status, CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanArchiveCatalog(api.CurrentToken?.RoleCode, status))
        {
            TempData["Error"] = "Bu fiyat arşivlenemez.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.PostAsync<object>($"/api/v1/platform/catalog/prices/{id}/archive", null, cancellationToken);
            TempData["Message"] = "Fiyat arşivlendi.";
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<CatalogPageViewModel> BuildPageAsync(CreatePriceViewModel create, CancellationToken cancellationToken)
    {
        var catalog = await api.GetAsync<CatalogResponse>("/api/v1/platform/catalog", cancellationToken);
        return new CatalogPageViewModel
        {
            Note = catalog?.Note,
            RoleCode = api.CurrentToken?.RoleCode,
            Email = api.CurrentToken?.Email,
            Create = create,
            Products = catalog?.Products.Select(product => new CatalogProductViewModel
            {
                ProductCode = product.ProductCode,
                ProductKind = product.ProductKind,
                DisplayName = product.DisplayName,
                Prices = product.Prices.Select(price => new PlanPriceViewModel
                {
                    Id = price.Id,
                    Interval = price.Interval,
                    Currency = price.Currency,
                    AmountMinor = price.AmountMinor,
                    Status = price.Status,
                }).ToArray(),
            }).ToArray() ?? [],
        };
    }
}
