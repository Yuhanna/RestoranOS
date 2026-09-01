using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class AnalyticsController(IWebApiExecuter api) : Controller
{
    public async Task<IActionResult> Index(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        days = Math.Clamp(days, 1, 366);
        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-days);
        var model = new AnalyticsDashboardViewModel { Days = days };

        try
        {
            var query = $"?fromUtc={Uri.EscapeDataString(from.ToString("O"))}&toUtc={Uri.EscapeDataString(to.ToString("O"))}";
            var summaryTask = api.InvokeGetAsync<AnalyticsSummaryViewModel>(
                $"/api/v1/management/analytics/summary{query}",
                cancellationToken);
            var periodsTask = api.InvokeGetAsync<List<AnalyticsPeriodViewModel>>(
                $"/api/v1/management/analytics/sales-by-period{query}&granularity={(days <= 2 ? "hour" : "day")}",
                cancellationToken);
            var topItemsTask = api.InvokeGetAsync<List<AnalyticsTopItemViewModel>>(
                $"/api/v1/management/analytics/top-items{query}&limit=10",
                cancellationToken);
            await Task.WhenAll(summaryTask, periodsTask, topItemsTask);

            model.Summary = await summaryTask;
            model.CanViewFinancials = model.Summary?.CanViewFinancials ?? false;
            model.Periods = await periodsTask ?? [];
            model.TopItems = await topItemsTask ?? [];
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Checkout(string planCode, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<object>(
                "/api/v1/management/subscription/checkout",
                new { planCode },
                cancellationToken);
            api.InvalidateWorkspaceCache();
            TempData["Message"] = $"{planCode} planına yükseltildi (demo ödeme).";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
