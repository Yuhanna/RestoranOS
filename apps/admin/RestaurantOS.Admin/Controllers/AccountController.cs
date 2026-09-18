using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Admin.Data;
using RestaurantOS.Admin.Models;

namespace RestaurantOS.Admin.Controllers;

public sealed class AccountController(IPlatformApi api, IHostEnvironment hostEnvironment) : Controller
{
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (api.IsAuthenticated)
        {
            return RedirectToLocal(returnUrl);
        }

        ViewData["IsDevelopment"] = hostEnvironment.IsDevelopment();
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        ViewData["IsDevelopment"] = hostEnvironment.IsDevelopment();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await api.LoginAsync(model.Email, model.Password, cancellationToken);
            return RedirectToLocal(returnUrl);
        }
        catch (WebApiException exception)
        {
            await api.LogoutAsync(cancellationToken);
            model.ErrorMessage = exception.Error?.Code switch
            {
                "PLATFORM_ACCESS_DENIED" =>
                    "Bu hesap restoran paneli içindir. Pasa Admin için platform operatörü girişi kullanın.",
                "INVALID_CREDENTIALS" =>
                    "E-posta veya parola geçersiz. Geliştirmede: platform@local.test / Local-Dev-Pass!42",
                _ => exception.StatusCode == 403
                    ? "Platform yönetimi yetkisi yok."
                    : string.IsNullOrWhiteSpace(exception.Message) ? "Giriş tamamlanamadı." : exception.Message,
            };
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

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }
}
