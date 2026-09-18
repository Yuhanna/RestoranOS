using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RestaurantOS.Admin.Data;

namespace RestaurantOS.Admin.Filters;

public sealed class RequirePlatformSessionAttribute : ActionFilterAttribute
{
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            await next();
            return;
        }

        var api = context.HttpContext.RequestServices.GetRequiredService<IPlatformApi>();
        if (!api.IsAuthenticated)
        {
            context.Result = RedirectToLogin(context);
            return;
        }

        var executed = await next();
        if (executed.Exception is WebApiException { StatusCode: StatusCodes.Status401Unauthorized })
        {
            executed.ExceptionHandled = true;
            context.Result = RedirectToLogin(context);
        }
    }

    private static RedirectToActionResult RedirectToLogin(ActionExecutingContext context) =>
        new(
            "Login",
            "Account",
            new { returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString });
}
