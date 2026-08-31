using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Web.Data;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Controllers;

[Route("Account/[action]")]
public sealed class AccountController(IWebApiExecuter api) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (api.IsAuthenticated)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        LoginViewModel model,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await api.LoginAsync(model.Email, model.Password, cancellationToken: cancellationToken);
            return RedirectToLocal(returnUrl);
        }
        catch (WebApiException exception)
        {
            model.ErrorMessage = MapAuthError(exception, isRegister: false);
            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (api.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (api.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await api.RegisterAsync(
                model.Email,
                model.Password,
                model.RestaurantName,
                model.BranchName,
                cancellationToken);
            TempData["Message"] = "Hesabınız oluşturuldu. Kurulumu tamamlamak için checklist’i izleyin.";
            return RedirectToAction("Index", "Home");
        }
        catch (WebApiException exception)
        {
            model.ErrorMessage = MapAuthError(exception, isRegister: true);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await api.LogoutAsync(cancellationToken);
        return RedirectToAction(nameof(Login));
    }

    private static string MapAuthError(WebApiException exception, bool isRegister) =>
        exception.Error?.Code switch
        {
            "EMAIL_IN_USE" => "Bu e-posta ile zaten bir hesap var. Giriş yapmayı deneyin.",
            "VALIDATION_ERROR" => string.IsNullOrWhiteSpace(exception.Message)
                ? "Bilgileri kontrol edip tekrar deneyin."
                : exception.Message,
            "INVALID_CREDENTIALS" =>
                "E-posta veya parola geçersiz.",
            "ACCOUNT_LOCKED" => "Hesap geçici olarak kilitlendi. Kısa süre sonra tekrar deneyin.",
            _ => string.IsNullOrWhiteSpace(exception.Message)
                ? (isRegister
                    ? "Kayıt tamamlanamadı. API’nin çalıştığından emin olun."
                    : "Giriş tamamlanamadı. API’nin çalıştığından emin olun.")
                : exception.Message,
        };

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (IsSafeRestaurantReturnUrl(returnUrl))
        {
            return Redirect(returnUrl!);
        }

        return RedirectToAction("Index", "Home");
    }

    private bool IsSafeRestaurantReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && Url.IsLocalUrl(returnUrl)
        && !returnUrl.StartsWith("/platform", StringComparison.OrdinalIgnoreCase);
}
