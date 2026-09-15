using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

[Route("platform")]
public sealed class PlatformHomeController(IPlatformWebApiExecuter api) : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        if (!api.IsAuthenticated)
        {
            return Redirect("/platform/account/login");
        }

        return View();
    }
}

[Route("platform/account")]
public sealed class PlatformAccountController(IPlatformWebApiExecuter api) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null)
    {
        if (api.IsAuthenticated)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new PlatformLoginViewModel());
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        PlatformLoginViewModel model,
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
        return Redirect("/platform/account/login");
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
        PlatformAuth.IsSafePlatformReturnUrl(Url, returnUrl)
            ? Redirect(returnUrl!)
            : Redirect("/platform");
}

[Route("platform/notifications")]
public sealed class PlatformNotificationsController(IPlatformWebApiExecuter api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return PlatformAuth.RedirectToLogin("/platform/notifications");
        }

        try
        {
            var notifications = await api.InvokeGetAsync<List<ManagedNotificationListItemViewModel>>(
                "/api/v1/platform/notifications",
                cancellationToken) ?? [];
            return View(new PlatformNotificationsPageViewModel
            {
                Notifications = notifications,
                Create = new CreatePlatformNotificationViewModel(),
                LastDispatchSummary = TempData["DispatchSummary"] as string,
            });
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new PlatformNotificationsPageViewModel());
        }
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreatePlatformNotificationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return PlatformAuth.RedirectToLogin("/platform/notifications");
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
            return PlatformAuth.RedirectToLogin("/platform/notifications");
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

    private async Task<PlatformNotificationsPageViewModel> BuildPageModelAsync(
        CreatePlatformNotificationViewModel create,
        CancellationToken cancellationToken)
    {
        var notifications = await api.InvokeGetAsync<List<ManagedNotificationListItemViewModel>>(
            "/api/v1/platform/notifications",
            cancellationToken) ?? [];
        return new PlatformNotificationsPageViewModel
        {
            Notifications = notifications,
            Create = create,
        };
    }

    private bool EnsureAuthenticated() => api.IsAuthenticated;
}

[Route("platform/subscription-offers")]
public sealed class PlatformSubscriptionOffersController(IPlatformWebApiExecuter api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return PlatformAuth.RedirectToLogin("/platform/subscription-offers");
        }

        try
        {
            return View(await BuildPageModelAsync(new CreatePlatformSubscriptionOfferViewModel(), cancellationToken));
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new PlatformSubscriptionOffersPageViewModel());
        }
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreatePlatformSubscriptionOfferViewModel model,
        CancellationToken cancellationToken)
    {
        if (!EnsureAuthenticated())
        {
            return PlatformAuth.RedirectToLogin("/platform/subscription-offers");
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }

        try
        {
            await api.InvokePostAsync<PlatformSubscriptionOfferListItemViewModel>(
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
            return PlatformAuth.RedirectToLogin("/platform/subscription-offers");
        }

        try
        {
            await api.InvokePatchAsync<PlatformSubscriptionOfferListItemViewModel>(
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

    private async Task<PlatformSubscriptionOffersPageViewModel> BuildPageModelAsync(
        CreatePlatformSubscriptionOfferViewModel create,
        CancellationToken cancellationToken)
    {
        var offers = await api.InvokeGetAsync<List<PlatformSubscriptionOfferListItemViewModel>>(
            "/api/v1/platform/subscription-offers",
            cancellationToken) ?? [];
        return new PlatformSubscriptionOffersPageViewModel
        {
            Offers = offers,
            Create = create,
        };
    }

    private bool EnsureAuthenticated() => api.IsAuthenticated;
}

[Route("platform/catalog")]
public sealed class PlatformCatalogController(IPlatformWebApiExecuter api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return PlatformAuth.RedirectToLogin("/platform/catalog");
        }

        try
        {
            return View(await BuildPageModelAsync(new CreatePlatformPlanPriceViewModel(), cancellationToken));
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
            return View(new PlatformCatalogPageViewModel());
        }
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind(Prefix = "Create")] CreatePlatformPlanPriceViewModel model,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return PlatformAuth.RedirectToLogin("/platform/catalog");
        }

        if (!TryParseAmountMinor(model, out var amountMinor, out var amountError))
        {
            ModelState.AddModelError("Create.Amount", amountError);
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }

        try
        {
            await api.InvokePostAsync<PlatformPlanPriceViewModel>(
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
            TempData["Message"] = "Fiyat taslağı oluşturuldu. Yayınlamak için listeden «Yayınla» seçin.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebApiException exception)
        {
            ModelState.AddModelError(string.Empty, exception.Message);
            return View("Index", await BuildPageModelAsync(model, cancellationToken));
        }
    }

    [HttpPost("publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return PlatformAuth.RedirectToLogin("/platform/catalog");
        }

        try
        {
            await api.InvokePostAsync<PlatformPlanPriceViewModel>(
                $"/api/v1/platform/catalog/prices/{id}/publish",
                null,
                cancellationToken);
            TempData["Message"] = "Fiyat yayınlandı. Yeni yükseltmeler bu tutarı kullanır.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return PlatformAuth.RedirectToLogin("/platform/catalog");
        }

        try
        {
            await api.InvokePostAsync<PlatformPlanPriceViewModel>(
                $"/api/v1/platform/catalog/prices/{id}/archive",
                null,
                cancellationToken);
            TempData["Message"] = "Fiyat arşivlendi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<PlatformCatalogPageViewModel> BuildPageModelAsync(
        CreatePlatformPlanPriceViewModel create,
        CancellationToken cancellationToken)
    {
        var catalog = await api.InvokeGetAsync<PlatformCatalogPageViewModel>(
            "/api/v1/platform/catalog",
            cancellationToken) ?? new PlatformCatalogPageViewModel();
        catalog.Create = create;
        return catalog;
    }

    private static bool TryParseAmountMinor(
        CreatePlatformPlanPriceViewModel model,
        out long amountMinor,
        out string error)
    {
        amountMinor = 0;
        error = "Geçerli bir tutar girin.";
        if (string.Equals(model.ProductCode, "Free", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!decimal.TryParse(
                model.Amount.Replace(",", ".", StringComparison.Ordinal),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out var lira)
            || lira < 0)
        {
            return false;
        }

        amountMinor = (long)Math.Round(lira * 100m, MidpointRounding.AwayFromZero);
        return true;
    }
}
