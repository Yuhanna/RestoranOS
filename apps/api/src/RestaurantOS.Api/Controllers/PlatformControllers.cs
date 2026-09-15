using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/platform/auth")]
public sealed class PlatformAuthController(
    IManagementAuthService authService,
    RestaurantOsDbContext dbContext) : ControllerBase
{
    private const string RefreshCookiePath = "/api/v1/platform/auth";

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

    [HttpPost("refresh")]
    [EnableRateLimiting("management-refresh")]
    [ProducesResponseType(typeof(ManagementAccessTokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.RefreshAsync(
                AuthRefreshCookie.Read(Request, platform: true) ?? string.Empty,
                AuthRealms.Platform,
                cancellationToken);
            if (!string.Equals(result.Realm, AuthRealms.Platform, StringComparison.Ordinal))
            {
                DeleteRefreshCookie();
                return ApiProblem.Create(
                    StatusCodes.Status401Unauthorized,
                    "INVALID_REFRESH_TOKEN",
                    "Platform session is invalid.");
            }

            SetRefreshCookie(result);
            return Ok(ToAccessTokenResponse(result));
        }
        catch (ManagementAuthException exception)
        {
            DeleteRefreshCookie();
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, exception.Code, exception.Message);
        }
    }

    [HttpGet("session")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(PlatformSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SessionAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetPlatformActor(User, out var userId, out _))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        var user = await dbContext.ManagementUsers.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        var staff = await dbContext.PlatformStaff.AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId && x.IsActive, cancellationToken);
        if (user is null || staff is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "PLATFORM_ACCESS_DENIED",
                "This account is not authorized for platform management.");
        }

        return Ok(new PlatformSessionResponse(user.Id, user.Email, staff.RoleCode, AuthRealms.Platform));
    }

    [HttpPost("logout")]
    [EnableRateLimiting("management-refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await authService.RevokeAsync(
            AuthRefreshCookie.Read(Request, platform: true) ?? string.Empty,
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
            result.BranchId,
            result.Realm,
            result.RoleCode,
            result.Email);

    private void SetRefreshCookie(ManagementTokenResult result) =>
        AuthRefreshCookie.Append(
            Response,
            Request,
            platform: true,
            RefreshCookiePath,
            result.RefreshToken,
            result.RefreshTokenExpiresAtUtc);

    private void DeleteRefreshCookie() =>
        AuthRefreshCookie.Delete(Response, Request, platform: true, RefreshCookiePath);
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
public sealed class PlatformNotificationsController(
    INotificationManagementService notifications,
    RestaurantOsDbContext dbContext) : ControllerBase
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

        if (await PlatformGate.ForbidCampaignWriteAsync(User, dbContext, cancellationToken) is { } denied)
        {
            return denied;
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
        if (await PlatformGate.ForbidCampaignWriteAsync(User, dbContext, cancellationToken) is { } denied)
        {
            return denied;
        }

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
public sealed class PlatformSubscriptionOffersController(
    IPlatformSubscriptionOfferService offers,
    RestaurantOsDbContext dbContext) : ControllerBase
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

        if (await PlatformGate.ForbidCampaignWriteAsync(User, dbContext, cancellationToken) is { } denied)
        {
            return denied;
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

        if (await PlatformGate.ForbidCampaignWriteAsync(User, dbContext, cancellationToken) is { } denied)
        {
            return denied;
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

[ApiController]
[Route("api/v1/platform/catalog")]
public sealed class PlatformCatalogController(IPlatformCatalogService catalog) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementCatalogResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
    {
        var snapshot = await catalog.GetCatalogAsync(cancellationToken);
        return Ok(PlatformCatalogMapper.ToResponse(snapshot));
    }

    [HttpPost("prices")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementPlanPriceResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateDraftAsync(
        [FromBody] ManagementCreatePlanPriceRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetPlatformActor(User, out var userId, out var role))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        if (request is null)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Request body is required.");
        }

        try
        {
            var created = await catalog.CreateDraftAsync(
                userId,
                role,
                new CreatePlanPriceCommand(
                    request.ProductCode,
                    request.Interval,
                    request.AmountMinor,
                    request.Currency,
                    request.TaxInclusive),
                cancellationToken);
            return Created($"/api/v1/platform/catalog/prices/{created.Id}", PlatformCatalogMapper.ToPrice(created));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code is "PLATFORM_ROLE_DENIED" or "PLATFORM_ACCESS_DENIED"
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status400BadRequest;
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpPost("prices/{priceId:guid}/publish")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementPlanPriceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> PublishAsync(Guid priceId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetPlatformActor(User, out var userId, out var role))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var published = await catalog.PublishAsync(userId, role, priceId, cancellationToken);
            return Ok(PlatformCatalogMapper.ToPrice(published));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "PLATFORM_ROLE_DENIED" or "PLATFORM_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
                "PRICE_NOT_FOUND" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpPost("prices/{priceId:guid}/archive")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementPlanPriceResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ArchiveAsync(Guid priceId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetPlatformActor(User, out var userId, out var role))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var archived = await catalog.ArchiveAsync(userId, role, priceId, cancellationToken);
            return Ok(PlatformCatalogMapper.ToPrice(archived));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "PLATFORM_ROLE_DENIED" or "PLATFORM_ACCESS_DENIED" => StatusCodes.Status403Forbidden,
                "PRICE_NOT_FOUND" => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }
}

file static class PlatformGate
{
    public static async Task<IActionResult?> ForbidCampaignWriteAsync(
        System.Security.Claims.ClaimsPrincipal user,
        RestaurantOsDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var actor = await PermissionAuthorizationHandler.TryGetLivePlatformActorAsync(
            user,
            dbContext,
            cancellationToken);
        if (actor is null || !PlatformStaffRoles.CanManageCampaigns(actor.Value.RoleCode))
        {
            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "PLATFORM_ROLE_DENIED",
                "Kampanya yazmak için Owner, Billing veya Support rolü gerekir.");
        }

        return null;
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

file static class PlatformCatalogMapper
{
    public static ManagementCatalogResponse ToResponse(PlatformCatalogSnapshot snapshot) =>
        new(
            snapshot.Products.Select(ToProduct).ToArray(),
            snapshot.Note);

    public static ManagementPlanPriceResponse ToPrice(ManagedPlanPriceResult price) =>
        new(
            price.Id,
            price.ProductCode,
            price.ProductKind,
            price.DisplayName,
            price.Interval,
            price.Currency,
            price.AmountMinor,
            price.TaxInclusive,
            price.Status,
            price.CreatedAtUtc,
            price.PublishedAtUtc,
            price.ArchivedAtUtc);

    private static ManagementCatalogProductResponse ToProduct(PlatformCatalogPlanResult product) =>
        new(
            product.ProductCode,
            product.ProductKind,
            product.DisplayName,
            product.Entitlements?.MaxBranches,
            product.Entitlements?.MaxTablesPerBranch,
            product.Entitlements?.MaxActiveUsers,
            product.Entitlements?.CanUseLiveOrderPanel,
            product.Entitlements?.CanUseMultiBranch,
            product.Entitlements?.HasPrioritySupport,
            product.Prices.Select(ToPrice).ToArray());
}
