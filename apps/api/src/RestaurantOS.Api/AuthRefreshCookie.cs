namespace RestaurantOS.Api;

/// <summary>
/// Refresh cookies use the __Secure- prefix only on HTTPS. Development HTTP (LAN / localhost)
/// cannot store Secure cookies, which previously caused silent logout after access-token expiry.
/// </summary>
public static class AuthRefreshCookie
{
    public const string ManagementSecureName = "__Secure-restaurantos-refresh";
    public const string ManagementDevName = "restaurantos-refresh";
    public const string PlatformSecureName = "__Secure-restaurantos-platform-refresh";
    public const string PlatformDevName = "restaurantos-platform-refresh";

    public static string Name(HttpRequest request, bool platform) =>
        request.IsHttps
            ? (platform ? PlatformSecureName : ManagementSecureName)
            : (platform ? PlatformDevName : ManagementDevName);

    public static string? Read(HttpRequest request, bool platform)
    {
        if (platform)
        {
            return request.Cookies[PlatformSecureName] ?? request.Cookies[PlatformDevName];
        }

        return request.Cookies[ManagementSecureName] ?? request.Cookies[ManagementDevName];
    }

    public static CookieOptions CreateOptions(
        HttpRequest request,
        string path,
        DateTimeOffset? expires = null) =>
        new()
        {
            HttpOnly = true,
            Secure = request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path = path,
            Expires = expires,
            IsEssential = true,
        };

    public static void Append(
        HttpResponse response,
        HttpRequest request,
        bool platform,
        string path,
        string refreshToken,
        DateTimeOffset expiresAtUtc)
    {
        response.Cookies.Append(
            Name(request, platform),
            refreshToken,
            CreateOptions(request, path, expiresAtUtc));
    }

    public static void Delete(HttpResponse response, HttpRequest request, bool platform, string path)
    {
        var options = CreateOptions(request, path);
        response.Cookies.Delete(Name(request, platform), options);
        // Clear both names so switching between HTTP/HTTPS mid-session does not leave orphans.
        response.Cookies.Delete(
            platform ? PlatformSecureName : ManagementSecureName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = path,
                IsEssential = true,
            });
        response.Cookies.Delete(
            platform ? PlatformDevName : ManagementDevName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Strict,
                Path = path,
                IsEssential = true,
            });
    }
}
