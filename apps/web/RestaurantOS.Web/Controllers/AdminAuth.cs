using Microsoft.AspNetCore.Mvc;

namespace RestaurantOS.Web.Controllers;

internal static class AdminAuth
{
    public const string LoginPath = "/admin/account/login";

    public static RedirectResult RedirectToLogin(string? returnUrl = null)
    {
        var path = string.IsNullOrWhiteSpace(returnUrl)
            ? LoginPath
            : $"{LoginPath}?returnUrl={Uri.EscapeDataString(returnUrl)}";
        return new RedirectResult(path);
    }

    public static bool IsSafeAdminReturnUrl(IUrlHelper url, string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && url.IsLocalUrl(returnUrl)
        && (returnUrl.StartsWith("/admin", StringComparison.OrdinalIgnoreCase)
            || returnUrl.StartsWith("/platform", StringComparison.OrdinalIgnoreCase));
}
