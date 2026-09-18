using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Admin.Data;
using RestaurantOS.Admin.Filters;
using RestaurantOS.Admin.Models;

namespace RestaurantOS.Admin.Controllers;

[RequirePlatformSession]
public sealed class NotificationsController(IPlatformApi api) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        try
        {
            return View(await BuildPageAsync(new CreateNotificationViewModel(), cancellationToken));
        }
        catch (WebApiException exception)
        {
            if (exception.StatusCode == StatusCodes.Status401Unauthorized)
            {
                return RedirectToAction("Login", "Account");
            }

            TempData["Error"] = exception.Message;
            return View(new NotificationsPageViewModel { RoleCode = api.CurrentToken?.RoleCode });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreateNotificationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanWriteNotifications(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Bu rol duyuru yazamaz.";
            return RedirectToAction(nameof(Index));
        }

        if (!TryResolveEndsAt(model.EndsAtLocal, out var endsAtUtc, out var endsError))
        {
            ModelState.AddModelError("Create.EndsAtLocal", endsError);
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageAsync(model, cancellationToken));
        }

        try
        {
            var created = await api.PostAsync<NotificationResponse>(
                "/api/v1/platform/notifications",
                new
                {
                    audience = model.Audience,
                    title = model.Title.Trim(),
                    body = model.Body.Trim(),
                    startsAtUtc = DateTimeOffset.UtcNow,
                    endsAtUtc,
                    actionUrl = string.IsNullOrWhiteSpace(model.ActionUrl) ? null : model.ActionUrl.Trim(),
                    isActive = true,
                },
                cancellationToken);
            if (model.DispatchNow && created is not null)
            {
                TempData["Message"] = await DispatchCoreAsync(created.Id, cancellationToken);
            }
            else
            {
                TempData["Message"] = "Duyuru taslak olarak kaydedildi. Gönder ile restoran panellerine düşer.";
            }

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
    public async Task<IActionResult> Dispatch(Guid id, CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanWriteNotifications(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Bu rol duyuru gönderemez.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            TempData["Message"] = await DispatchCoreAsync(id, cancellationToken);
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
    public async Task<IActionResult> SetActive(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (!AdminAccess.CanWriteNotifications(api.CurrentToken?.RoleCode))
        {
            TempData["Error"] = "Bu rol duyuru durumunu değiştiremez.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await api.PatchAsync<NotificationResponse>(
                $"/api/v1/platform/notifications/{id}",
                new { isActive },
                cancellationToken);
            TempData["Message"] = isActive ? "Duyuru yeniden açıldı." : "Duyuru kapatıldı; restoran paneline düşmez.";
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

    private async Task<string> DispatchCoreAsync(Guid id, CancellationToken cancellationToken)
    {
        var result = await api.PostAsync<NotificationDispatchResponse>(
            $"/api/v1/platform/notifications/{id}/dispatch",
            null,
            cancellationToken);
        if (result is null)
        {
            return "Duyuru gönderildi.";
        }

        if (result.PushSentCount == 0 && result.EmailSentCount == 0)
        {
            return "Gönderim tamam; hedef kitlede şu an restoran yok.";
        }

        return $"Duyuru iletildi: {result.PushSentCount} restoran paneli, {result.EmailSentCount} e-posta.";
    }

    private async Task<NotificationsPageViewModel> BuildPageAsync(
        CreateNotificationViewModel create,
        CancellationToken cancellationToken)
    {
        var items = await api.GetAsync<List<NotificationResponse>>("/api/v1/platform/notifications", cancellationToken)
            ?? [];
        return new NotificationsPageViewModel
        {
            RoleCode = api.CurrentToken?.RoleCode,
            Create = create,
            Items = items.Select(item => new NotificationViewModel
            {
                Id = item.Id,
                Audience = item.Audience,
                Title = item.Title,
                Body = item.Body,
                ActionUrl = item.ActionUrl,
                IsActive = item.IsActive,
                StartsAtUtc = item.StartsAtUtc,
                EndsAtUtc = item.EndsAtUtc,
                LastDispatchedAtUtc = item.LastDispatchedAtUtc,
            }).ToArray(),
        };
    }

    private static bool TryResolveEndsAt(string? localText, out DateTimeOffset? endsAtUtc, out string error)
    {
        endsAtUtc = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(localText))
        {
            return true;
        }

        if (!DateTime.TryParse(localText, out var local))
        {
            error = "Bitiş zamanı geçersiz.";
            return false;
        }

        var localOffset = new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local.GetUtcOffset(local));
        if (localOffset <= DateTimeOffset.UtcNow)
        {
            error = "Bitiş zamanı gelecekte olmalı.";
            return false;
        }

        endsAtUtc = localOffset.ToUniversalTime();
        return true;
    }
}
