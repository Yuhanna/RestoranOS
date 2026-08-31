using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/platform/auth")]
public sealed class PlatformAuthController(IManagementAuthService authService) : ControllerBase
{
    private const string RefreshCookieName = "__Secure-restaurantos-platform-refresh";

    [HttpPost("login")]
    [EnableRateLimiting("management-login")]
    [ProducesResponseType(typeof(ManagementAccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] ManagementPlatformLoginRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrEmpty(request.Password))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_CREDENTIALS",
                "Email or password is invalid.");
        }

        try
        {
            var result = await authService.LoginPlatformAsync(
                request.Email,
                request.Password,
                cancellationToken);
            SetRefreshCookie(result);
            return Ok(ToAccessTokenResponse(result));
        }
        catch (ManagementAuthException exception)
        {
            var status = exception.Code switch
            {
                "PLATFORM_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status401Unauthorized,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpPost("logout")]
    [EnableRateLimiting("management-refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await authService.RevokeAsync(
            Request.Cookies[RefreshCookieName] ?? string.Empty,
            cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    private static ManagementAccessTokenResponse ToAccessTokenResponse(ManagementTokenResult result) =>
        new(
            result.AccessToken,
            result.AccessTokenExpiresAtUtc,
            result.UserId,
            result.TenantId,
            result.BranchId);

    private void SetRefreshCookie(ManagementTokenResult result) =>
        Response.Cookies.Append(
            RefreshCookieName,
            result.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/platform/auth",
                Expires = result.RefreshTokenExpiresAtUtc,
                IsEssential = true,
            });

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(
            RefreshCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/platform/auth",
            });
}

[ApiController]
[Route("api/v1/platform")]
public sealed class PlatformAccessController : ControllerBase
{
    [HttpGet("access")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult AccessCheck() => Ok(new { ok = true });
}

[ApiController]
[Route("api/v1/platform/notifications")]
public sealed class PlatformNotificationsController(INotificationManagementService notifications) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementManagedNotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var items = await notifications.ListPlatformAsync(cancellationToken);
        return Ok(items.Select(PlatformNotificationMapper.ToResponse).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementManagedNotificationResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ManagementCreatePlatformNotificationRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Title and body are required.");
        }

        try
        {
            var created = await notifications.CreatePlatformAsync(
                new CreatePlatformNotificationCommand(
                    request.Audience,
                    request.Title,
                    request.Body,
                    request.StartsAtUtc,
                    request.EndsAtUtc,
                    request.ActionUrl,
                    request.IsActive),
                cancellationToken);
            return Created($"/api/v1/platform/notifications/{created.Id}", PlatformNotificationMapper.ToResponse(created));
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
    }

    [HttpPost("{notificationId:guid}/dispatch")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementNotificationDispatchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DispatchAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await notifications.DispatchPlatformAsync(notificationId, cancellationToken);
            return Ok(new ManagementNotificationDispatchResponse(
                result.EmailSentCount,
                result.PushSentCount,
                result.RecipientEmails));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code == "NOTIFICATION_NOT_FOUND"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }
}

[ApiController]
[Route("api/v1/platform/subscription-offers")]
public sealed class PlatformSubscriptionOffersController(IPlatformSubscriptionOfferService offers) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementSubscriptionOfferAdminResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        var items = await offers.ListAsync(cancellationToken);
        return Ok(items.Select(PlatformOfferMapper.ToResponse).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementSubscriptionOfferAdminResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ManagementCreateSubscriptionOfferRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.Title)
            || string.IsNullOrWhiteSpace(request.Body))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Title and body are required.");
        }

        try
        {
            var created = await offers.CreateAsync(
                new CreateSubscriptionOfferCommand(
                    request.Audience,
                    request.TargetPlanCode,
                    request.DiscountPercent,
                    request.DurationMonths,
                    request.Title,
                    request.Body,
                    request.StartsAtUtc,
                    request.EndsAtUtc,
                    request.IsActive),
                cancellationToken);
            return Created(
                $"/api/v1/platform/subscription-offers/{created.Id}",
                PlatformOfferMapper.ToResponse(created));
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
    }

    [HttpPatch("{offerId:guid}")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementSubscriptionOfferAdminResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActiveAsync(
        Guid offerId,
        [FromBody] ManagementSetSubscriptionOfferActiveRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.IsActive is not bool isActive)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "isActive is required.");
        }

        try
        {
            var updated = await offers.SetActiveAsync(offerId, isActive, cancellationToken);
            return Ok(PlatformOfferMapper.ToResponse(updated));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code == "OFFER_NOT_FOUND"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }
}

file static class PlatformNotificationMapper
{
    public static ManagementManagedNotificationResponse ToResponse(ManagedNotificationResult notification) =>
        new(
            notification.Id,
            notification.TenantId,
            notification.Audience,
            notification.Title,
            notification.Body,
            notification.ActionUrl,
            notification.StartsAtUtc,
            notification.EndsAtUtc,
            notification.IsActive,
            notification.LastDispatchedAtUtc);
}

file static class PlatformOfferMapper
{
    public static ManagementSubscriptionOfferAdminResponse ToResponse(ManagedSubscriptionOfferResult offer) =>
        new(
            offer.Id,
            offer.Audience,
            offer.TargetPlanCode,
            offer.DiscountPercent,
            offer.DurationMonths,
            offer.Title,
            offer.Body,
            offer.StartsAtUtc,
            offer.EndsAtUtc,
            offer.IsActive);
}
