using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace RestaurantOS.Web.Data;

public sealed class PlatformWebApiExecuter : WebApiExecuter, IPlatformWebApiExecuter
{
    public PlatformWebApiExecuter(
        IHttpContextAccessor httpContextAccessor,
        IHostEnvironment hostEnvironment,
        IOptions<ApiOptions> apiOptions,
        ApiCookieJarStore cookieJarStore)
        : base(httpContextAccessor, hostEnvironment, apiOptions, cookieJarStore, ApiSessionScope.Platform)
    {
    }

    protected override string LogoutRelativePath => "/api/v1/platform/auth/logout";

    public Task LoginPlatformAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default) =>
        LoginCoreAsync(
            "/api/v1/platform/auth/login",
            new { email, password },
            cancellationToken);
}
