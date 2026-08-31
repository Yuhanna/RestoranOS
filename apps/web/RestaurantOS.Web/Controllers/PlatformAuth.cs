using Microsoft.AspNetCore.Mvc;

namespace RestaurantOS.Web.Controllers;

internal static class PlatformAuth
{
    public static RedirectResult RedirectToLogin(string? returnUrl = null)
    {
        var path = string.IsNullOrWhiteSpace(returnUrl)
            ? "/platform/account/login"
            : $"/platform/account/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        return new RedirectResult(path);
    }

    public static bool IsSafePlatformReturnUrl(IUrlHelper url, string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && url.IsLocalUrl(returnUrl)
        && returnUrl.StartsWith("/platform", StringComparison.OrdinalIgnoreCase);
}
