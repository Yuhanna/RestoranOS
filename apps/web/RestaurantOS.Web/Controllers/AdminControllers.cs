using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

[Route("admin")]
[Route("platform")]
public sealed class AdminHomeController(IPlatformWebApiExecuter api) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        if (!api.IsAuthenticated)
        {
            return Redirect(AdminAuth.LoginPath);
        }

        return View();
    }
}

[Route("admin/account")]
[Route("platform/account")]
public sealed class AdminAccountController(IPlatformWebApiExecuter api) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (api.IsAuthenticated)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new AdminLoginViewModel());
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        AdminLoginViewModel model,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await api.LoginPlatformAsync(model.Email, model.Password, cancellationToken);
            return RedirectToLocal(returnUrl);
        }
        catch (WebApiException exception)
        {
            await api.LogoutAsync(cancellationToken);
            model.ErrorMessage = exception.Error?.Code switch
            {
                "PLATFORM_ACCESS_DENIED" =>
                    "Bu hesap restoran operatörüdür; platform yönetimi yetkisi yok. " +
                    "Site sahibi girişi için platform@local.test kullanın. " +
                    "Restoran paneli için /Account/Login adresine gidin.",
                "INVALID_CREDENTIALS" =>
                    "E-posta veya parola geçersiz. Geliştirmede site sahibi hesabı: " +
                    "platform@local.test / Local-Dev-Pass!42. API yeniden başlatıldığında hesap otomatik oluşturulur.",
                _ => exception.StatusCode == 403
                    ? "Platform yönetimi yetkisi yok."
                    : MapAuthError(exception),
            };
            return View(model);
        }
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await api.LogoutAsync(cancellationToken);
        return Redirect(AdminAuth.LoginPath);
    }

    private static string MapAuthError(WebApiException exception) =>
        exception.Error?.Code switch
        {
            "INVALID_CREDENTIALS" => "E-posta veya parola geçersiz.",
            "ACCOUNT_LOCKED" => "Hesap geçici olarak kilitlendi.",
            _ => string.IsNullOrWhiteSpace(exception.Message)
                ? "Giriş tamamlanamadı."
                : exception.Message,
        };

    private RedirectResult RedirectToLocal(string? returnUrl) =>
        AdminAuth.IsSafeAdminReturnUrl(Url, returnUrl)
            ? Redirect(returnUrl!)
            : Redirect("/admin");
}

[Route("admin/notifications")]
[Route("platform/notifications")]
public sealed class AdminNotificationsController(IPlatformWebApiExecuter api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return AdminAuth.RedirectToLogin("/admin/notifications");
        }

        try
        {
            var notifications = await api.InvokeGetAsync<List<ManagedNotificationListItemViewModel>>(
                "/api/v1/platform/notifications",
                cancellationToken) ?? [];
            return View(new AdminNotificationsPageViewModel
            {
                Notifications = notifications,
                Create = new CreateAdminNotificationViewModel(),
                LastDispatchSummary = TempData["DispatchSummary"] as string,
            });
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new AdminNotificationsPageViewModel());
        }
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreateAdminNotificationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return AdminAuth.RedirectToLogin("/admin/notifications");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }

        try
        {
            await api.InvokePostAsync<ManagedNotificationListItemViewModel>(
                "/api/v1/platform/notifications",
                new
                {
                    audience = model.Audience,
                    title = model.Title.Trim(),
                    body = model.Body.Trim(),
                    startsAtUtc = DateTimeOffset.UtcNow,
                    actionUrl = string.IsNullOrWhiteSpace(model.ActionUrl) ? null : model.ActionUrl.Trim(),
                    isActive = true,
                },
                cancellationToken);
            TempData["Message"] = "Platform bildirimi oluşturuldu. Göndermek için listeden «E-posta + push gönder» seçin.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }
    }

    [HttpPost("dispatch")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispatch(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return AdminAuth.RedirectToLogin("/admin/notifications");
        }

        try
        {
            var result = await api.InvokePostAsync<NotificationDispatchResultViewModel>(
                $"/api/v1/platform/notifications/{id}/dispatch",
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
                TempData["Message"] = "Platform bildirimi iletildi.";
            }
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminNotificationsPageViewModel> BuildPageModelAsync(
        CreateAdminNotificationViewModel create,
        CancellationToken cancellationToken)
    {
        var notifications = await api.InvokeGetAsync<List<ManagedNotificationListItemViewModel>>(
            "/api/v1/platform/notifications",
            cancellationToken) ?? [];
        return new AdminNotificationsPageViewModel
        {
            Notifications = notifications,
            Create = create,
        };
    }

    private bool EnsureAuthenticated() => api.IsAuthenticated;
}

[Route("admin/subscription-offers")]
[Route("platform/subscription-offers")]
public sealed class AdminSubscriptionOffersController(IPlatformWebApiExecuter api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return AdminAuth.RedirectToLogin("/admin/subscription-offers");
        }

        try
        {
            return View(await BuildPageModelAsync(new CreateAdminSubscriptionOfferViewModel(), cancellationToken));
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new AdminSubscriptionOffersPageViewModel());
        }
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreateAdminSubscriptionOfferViewModel model,
        CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return AdminAuth.RedirectToLogin("/admin/subscription-offers");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }

        try
        {
            await api.InvokePostAsync<AdminSubscriptionOfferListItemViewModel>(
                "/api/v1/platform/subscription-offers",
                new
                {
                    audience = model.Audience,
                    targetPlanCode = model.TargetPlanCode,
                    discountPercent = model.DiscountPercent,
                    durationMonths = model.DurationMonths,
                    title = model.Title.Trim(),
                    body = model.Body.Trim(),
                    startsAtUtc = DateTimeOffset.UtcNow,
                    endsAtUtc = model.EndsAtLocal?.ToUniversalTime(),
                    isActive = true,
                },
                cancellationToken);
            TempData["Message"] = "Üyelik kampanyası oluşturuldu.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }
    }

    [HttpPost("toggle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return AdminAuth.RedirectToLogin("/admin/subscription-offers");
        }

        try
        {
            await api.InvokePatchAsync<AdminSubscriptionOfferListItemViewModel>(
                $"/api/v1/platform/subscription-offers/{id}",
                new { isActive = !isActive },
                cancellationToken);
            TempData["Message"] = isActive ? "Kampanya durduruldu." : "Kampanya etkinleştirildi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<AdminSubscriptionOffersPageViewModel> BuildPageModelAsync(
        CreateAdminSubscriptionOfferViewModel create,
        CancellationToken cancellationToken)
    {
        var offers = await api.InvokeGetAsync<List<AdminSubscriptionOfferListItemViewModel>>(
            "/api/v1/platform/subscription-offers",
            cancellationToken) ?? [];
        return new AdminSubscriptionOffersPageViewModel
        {
            Offers = offers,
            Create = create,
        };
    }

    private bool EnsureAuthenticated() => api.IsAuthenticated;
}
