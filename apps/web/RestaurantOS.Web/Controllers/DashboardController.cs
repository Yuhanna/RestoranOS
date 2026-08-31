using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class DashboardController(IWebApiExecuter api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            var model = await api.InvokeGetAsync<DashboardTodayViewModel>(
                "/api/v1/management/dashboard/today",
                cancellationToken);
            return View(model ?? new DashboardTodayViewModel());
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new DashboardTodayViewModel());
        }
    }
}
