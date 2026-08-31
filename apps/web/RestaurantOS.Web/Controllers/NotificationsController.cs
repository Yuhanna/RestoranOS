using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

public sealed class NotificationsController(IWebApiExecuter api) : Controller
{
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        try
        {
            var notifications = await api.InvokeGetAsync<List<ManagedNotificationListItemViewModel>>(
                "/api/v1/management/notifications",
                cancellationToken) ?? [];
            return View(new NotificationsPageViewModel
            {
                Notifications = notifications,
                Create = new CreateNotificationViewModel(),
                LastDispatchSummary = TempData["DispatchSummary"] as string,
            });
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new NotificationsPageViewModel());
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreateNotificationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        if (!ModelState.IsValid)
        {
            var notifications = await api.InvokeGetAsync<List<ManagedNotificationListItemViewModel>>(
                "/api/v1/management/notifications",
                cancellationToken) ?? [];
            return View("Index", new NotificationsPageViewModel
            {
                Notifications = notifications,
                Create = model,
            });
        }

        try
        {
            await api.InvokePostAsync<ManagedNotificationListItemViewModel>(
                "/api/v1/management/notifications",
                new
                {
                    audience = model.Audience,
                    title = model.Title.Trim(),
                    body = model.Body.Trim(),
                    startsAtUtc = DateTimeOffset.UtcNow,
                    actionUrl = string.IsNullOrWhiteSpace(model.ActionUrl) ? null : model.ActionUrl.Trim(),
                    isActive = true,
                    broadcastToAllTenants = false,
                },
                cancellationToken);
            TempData["Message"] = "Bildirim oluşturuldu. Göndermek için listeden «E-posta + push gönder» seçin.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            var notifications = await api.InvokeGetAsync<List<ManagedNotificationListItemViewModel>>(
                "/api/v1/management/notifications",
                cancellationToken) ?? [];
            return View("Index", new NotificationsPageViewModel
            {
                Notifications = notifications,
                Create = model,
            });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispatch(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return ChallengeLogin();
        }

        try
        {
            var result = await api.InvokePostAsync<NotificationDispatchResultViewModel>(
                $"/api/v1/management/notifications/{id}/dispatch",
                null,
                cancellationToken);
            if (result is null)
            {
                TempData["Error"] = "Gönderim yanıtı alınamadı.";
            }
            else
            {
                TempData["DispatchSummary"] =
                    $"{result.EmailSentCount} e-posta, {result.PushSentCount} anlık bildirim gönderildi.";
                TempData["Message"] = "Bildirim iletildi.";
            }
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private bool EnsureAuthenticated() => api.IsAuthenticated;

    private RedirectToActionResult ChallengeLogin() =>
        RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Index)) });
}
