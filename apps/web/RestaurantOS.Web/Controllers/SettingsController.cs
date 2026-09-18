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

        var workspace = await api.GetWorkspaceAsync(cancellationToken);
        if (workspace?.CanAccessSettings != true)
        {
            TempData["Error"] = "Ayarlar için yetkiniz yok.";
            return RedirectToAction("Index", "Dashboard");
        }

        var model = new SettingsViewModel { Workspace = workspace };
        try
        {
            var profile = await api.InvokeGetAsync<ProfileApiModel>(
                "/api/v1/management/auth/profile",
                cancellationToken);
            if (profile is not null)
            {
                model.Profile.DisplayName = profile.DisplayName ?? string.Empty;
                model.Profile.Phone = profile.Phone;
            }
        }
        catch (WebApiException)
        {
            // Profil ön doldurma isteğe bağlı; sayfa yine açılsın.
        }

        try
        {
            if (workspace.CanEditMenus)
            {
                var settings = await api.InvokeGetAsync<CustomerMenuSettingsApiModel>(
                    "/api/v1/management/customer-menu/settings",
                    cancellationToken);
                model.CustomerMenu = MenuCatalogFormHelper.FromApi(settings);
            }
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

            if (customerMenu.ClearLogo)
            {
                await api.InvokeDeleteAsync(
                    "/api/v1/management/customer-menu/settings/logo",
                    cancellationToken);
            }
            else if (customerMenu.LogoFile is { Length: > 0 })
            {
                await api.InvokePostMultipartAsync<CustomerMenuSettingsApiModel>(
                    "/api/v1/management/customer-menu/settings/logo",
                    customerMenu.LogoFile,
                    customerMenu.LogoAlt,
                    cancellationToken);
            }

            TempData["Message"] = "Müşteri menü ayarları kaydedildi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(
        [Bind(Prefix = "Profile")] UpdateProfileViewModel profile,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePatchAsync<object>(
                "/api/v1/management/auth/profile",
                new
                {
                    displayName = profile.DisplayName,
                    phone = string.IsNullOrWhiteSpace(profile.Phone) ? null : profile.Phone,
                },
                cancellationToken);
            TempData["Message"] = "Profil güncellendi.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        [Bind(Prefix = "ChangePassword")] ChangePasswordViewModel changePassword,
        CancellationToken cancellationToken)
    {
        if (!api.IsAuthenticated)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await api.InvokePostAsync<object>(
                "/api/v1/management/auth/change-password",
                new
                {
                    currentPassword = changePassword.CurrentPassword,
                    newPassword = changePassword.NewPassword,
                },
                cancellationToken);
            TempData["Message"] = "Şifre güncellendi. Yeniden giriş yapmanız gerekebilir.";
        }
        catch (WebApiException exception)
        {
            TempData["Error"] = exception.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
