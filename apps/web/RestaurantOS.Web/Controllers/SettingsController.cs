using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class SettingsController(IWebApiExecuter api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = new SettingsViewModel();
        try
        {
            var workspaceTask = api.GetWorkspaceAsync(cancellationToken);
            var settingsTask = api.InvokeGetAsync<CustomerMenuSettingsApiModel>(
                "/api/v1/management/customer-menu/settings",
                cancellationToken);
            await Task.WhenAll(workspaceTask, settingsTask);

            model.Workspace = await workspaceTask;
            var settings = await settingsTask;
            model.CustomerMenu = MenuCatalogFormHelper.FromApi(settings);
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCustomerMenu(
        CustomerMenuSettingsViewModel customerMenu,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePutAsync<CustomerMenuSettingsApiModel>(
                "/api/v1/management/customer-menu/settings",
                MenuCatalogFormHelper.ToApi(customerMenu),
                cancellationToken);
            TempData["Message"] = "Müşteri menü ayarları kaydedildi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
