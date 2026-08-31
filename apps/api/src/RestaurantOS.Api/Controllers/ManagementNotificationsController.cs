using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/notifications")]
public sealed class ManagementNotificationsController(INotificationManagementService notifications) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ManagementPolicies.SubscriptionManage)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementManagedNotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out _))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        var items = await notifications.ListAsync(tenantId, cancellationToken);
        return Ok(items.Select(ToResponse).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = ManagementPolicies.SubscriptionManage)]
    [ProducesResponseType(typeof(ManagementManagedNotificationResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ManagementCreateNotificationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out _))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Title and body are required.");
        }

        if (request.BroadcastToAllTenants)
        {
            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "PLATFORM_SCOPE_REQUIRED",
                "Platform-wide notifications must be created from the platform console.");
        }

        try
        {
            var created = await notifications.CreateAsync(
                tenantId,
                new CreateManagedNotificationCommand(
                    request.Audience,
                    request.Title,
                    request.Body,
                    request.StartsAtUtc,
                    request.EndsAtUtc,
                    request.ActionUrl,
                    request.IsActive,
                    false),
                cancellationToken);
            return Created($"/api/v1/management/notifications/{created.Id}", ToResponse(created));
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
    }

    [HttpPost("{notificationId:guid}/dispatch")]
    [Authorize(Policy = ManagementPolicies.SubscriptionManage)]
    [ProducesResponseType(typeof(ManagementNotificationDispatchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DispatchAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out _))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        try
        {
            var result = await notifications.DispatchAsync(tenantId, notificationId, cancellationToken);
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

    private static ManagementManagedNotificationResponse ToResponse(ManagedNotificationResult notification) =>
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
