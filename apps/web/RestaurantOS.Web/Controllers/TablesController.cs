using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class TablesController(IWebApiExecuter api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Index)) });
        }

        try
        {
            var tables = await api.InvokeGetAsync<List<TableListItemViewModel>>(
                "/api/v1/management/tables",
                cancellationToken) ?? [];
            await ApplyTableQuotaHintsAsync(cancellationToken);
            return View(tables);
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new List<TableListItemViewModel>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        return View(await BuildCreateModelAsync(cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTableViewModel model, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        var blocked = await BuildCreateModelAsync(cancellationToken);
        if (blocked.IsLimitReached)
        {
            model.LimitReachedMessage = blocked.LimitReachedMessage;
            ModelState.AddModelError(string.Empty, blocked.LimitReachedMessage!);
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await api.InvokePostAsync<TableListItemViewModel>(
                "/api/v1/management/tables",
                new { label = model.Label },
                cancellationToken);
            TempData["Message"] = $"«{model.Label}» masası oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateQr(Guid id, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            var generated = await api.InvokePostAsync<QrPrintViewModel>(
                $"/api/v1/management/tables/{id}/qr-codes",
                null,
                cancellationToken);
            if (generated is null)
            {
                TempData["Error"] = "QR üretilemedi.";
                return RedirectToAction(nameof(Index));
            }

            return View("Print", generated);
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Print(Guid id, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            var print = await api.InvokeGetAsync<QrPrintViewModel>(
                $"/api/v1/management/qr-codes/{id}/print",
                cancellationToken);
            if (print is null)
            {
                TempData["Error"] = "Yazdırma verisi alınamadı.";
                return RedirectToAction(nameof(Index));
            }

            return View(print);
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task ApplyTableQuotaHintsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await api.InvokeGetAsync<WorkspaceViewModel>(
                "/api/v1/management/workspace",
                cancellationToken);
            var usage = workspace?.Entitlements;
            if (usage?.MaxTablesPerBranch is int max)
            {
                ViewData["TableQuota"] = $"{usage.TableCount}/{max}";
                ViewData["TableQuotaReached"] = usage.TableCount >= max;
            }
        }
        catch (WebApiException)
        {
            // Quota hint is optional.
        }
    }

    private async Task<CreateTableViewModel> BuildCreateModelAsync(CancellationToken cancellationToken)
    {
        var model = new CreateTableViewModel();
        try
        {
            var workspace = await api.InvokeGetAsync<WorkspaceViewModel>(
                "/api/v1/management/workspace",
                cancellationToken);
            var usage = workspace?.Entitlements;
            if (usage?.MaxTablesPerBranch is int max && usage.TableCount >= max)
            {
                model.LimitReachedMessage =
                    $"Free planda en fazla {max} masa eklenebilir ({usage.TableCount}/{max}). " +
                    "Pro plana geçerek limiti kaldırabilirsiniz.";
            }
        }
        catch (WebApiException)
        {
            // Fall through; API will still enforce on POST.
        }

        return model;
    }
}
