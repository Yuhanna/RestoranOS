using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class HomeController(IWebApiExecuter api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewData["IsAuthenticated"] = api.IsAuthenticated;
        if (!api.IsAuthenticated)
        {
            return View(model: null);
        }

        var checklist = new SetupChecklistViewModel();
        try
        {
            var workspace = await api.InvokeGetAsync<WorkspaceViewModel>(
                "/api/v1/management/workspace",
                cancellationToken);
            if (workspace is not null)
            {
                checklist.RestaurantName = workspace.RestaurantName;
                checklist.BranchName = workspace.BranchName;
                ViewData["RestaurantName"] = workspace.RestaurantName;
                ViewData["BranchName"] = workspace.BranchName;
                if (workspace.Entitlements is not null)
                {
                    checklist.PlanCode = workspace.Entitlements.PlanCode;
                    checklist.PlanDisplayName = workspace.Entitlements.PlanDisplayName;
                    checklist.IsTrial = workspace.Entitlements.IsTrial;
                    checklist.TrialEndsAtUtc = workspace.Entitlements.TrialEndsAtUtc;
                    checklist.MaxTablesPerBranch = workspace.Entitlements.MaxTablesPerBranch;
                    checklist.CanUseProductImages = workspace.Entitlements.CanUseProductImages;
                    checklist.CanUseMenuTranslations = workspace.Entitlements.CanUseMenuTranslations;
                    checklist.CanUseLiveOrderPanel = workspace.Entitlements.CanUseLiveOrderPanel;
                    ViewData["PlanDisplayName"] = workspace.Entitlements.PlanDisplayName;
                    ViewData["CanUseProductImages"] = workspace.Entitlements.CanUseProductImages;
                }
            }

            var tables = await api.InvokeGetAsync<List<TableListItemViewModel>>(
                "/api/v1/management/tables",
                cancellationToken) ?? [];
            var menus = await api.InvokeGetAsync<List<MenuSummaryViewModel>>(
                "/api/v1/management/menus",
                cancellationToken) ?? [];
            checklist.TableCount = tables.Count;
            checklist.ActiveQrCount = tables.Sum(t => t.ActiveQrCount);
            checklist.MenuCount = menus.Count;
            checklist.PublishedMenuCount = menus.Count(m =>
                string.Equals(m.Lifecycle, "published", StringComparison.OrdinalIgnoreCase));
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return View(checklist);
    }

    public IActionResult Error() => View();
}
