using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;
using RestaurantOS.Domain;

namespace RestaurantOS.Web.Controllers;

public sealed class SubscriptionController(IWebApiExecuter api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        var workspace = await api.GetWorkspaceAsync(cancellationToken);
        var model = new SubscriptionPageViewModel { Workspace = workspace };
        var entitlements = workspace?.Entitlements;
        var highlights = new List<UpgradePromptViewModel>();
        var isPaidPro = entitlements is { PlanCode: "Pro", IsTrial: false };
        if (entitlements is not null && !isPaidPro)
        {
            if (entitlements.IsTrial)
            {
                highlights.Add(MonetizationUi.FromFeature(
                    MonetizationPolicy.TrialExpiringFeature,
                    toneOverride: MonetizationPolicy.TrialUrgency(
                        MonetizationPolicy.TrialDaysRemaining(entitlements.TrialEndsAtUtc, DateTimeOffset.UtcNow))));
            }

            highlights.Add(MonetizationUi.FromFeature(MonetizationPolicy.MenuThemesFeature, compact: true));
            highlights.Add(MonetizationUi.FromFeature(MonetizationPolicy.AnalyticsLookbackFeature, compact: true));
            highlights.Add(MonetizationUi.FromFeature(MonetizationPolicy.MultiBranchFeature, compact: true));
            highlights.Add(MonetizationUi.FromFeature(MonetizationPolicy.ActiveQrFeature, compact: true));
        }

        model.Highlights = highlights;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpgradePro(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<object>(
                "/api/v1/management/subscription/checkout",
                new { planCode = "Pro" },
                cancellationToken);
            api.InvalidateWorkspaceCache();
            TempData["Message"] = "Pro plana geçildi. Dahil 2 şube aktif; ek şubeler ücretlidir.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartTrial(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<object>(
                "/api/v1/management/subscription/start-trial",
                null,
                cancellationToken);
            api.InvalidateWorkspaceCache();
            TempData["Message"] = "30 günlük Pro denemesi başladı. Temalar, kampanya ve çok şube denemede açık.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
