using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/promotions")]
public sealed class ManagementPromotionsController(IPromotionManagementService promotions) : ControllerBase
{
    [HttpGet("menu")]
    [Authorize(Policy = ManagementPolicies.MenuView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementMenuPromotionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMenuPromotionsAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        var items = await promotions.ListMenuPromotionsAsync(tenantId, branchId, cancellationToken);
        return Ok(items.Select(ToResponse).ToArray());
    }

    [HttpPost("menu")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementMenuPromotionResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMenuPromotionAsync(
        [FromBody] ManagementCreateMenuPromotionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Promotion name is required.");
        }

        try
        {
            var created = await promotions.CreateMenuPromotionAsync(
                tenantId,
                branchId,
                new CreateMenuPromotionCommand(
                    request.Name,
                    request.Scope,
                    request.DiscountKind,
                    request.DiscountValue,
                    request.StartsAtUtc,
                    request.EndsAtUtc,
                    request.DailyStartLocal,
                    request.DailyEndLocal,
                    request.CategoryId,
                    request.MenuItemId,
                    request.IsActive,
                    request.DaysOfWeekMask),
                cancellationToken);
            return Created($"/api/v1/management/promotions/menu/{created.Id}", ToResponse(created));
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    [HttpPatch("menu/{promotionId:guid}")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementMenuPromotionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMenuPromotionAsync(
        Guid promotionId,
        [FromBody] ManagementUpdateMenuPromotionRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        try
        {
            var updated = await promotions.UpdateMenuPromotionAsync(
                tenantId,
                branchId,
                promotionId,
                new UpdateMenuPromotionCommand(request?.Name, request?.IsActive, request?.EndsAtUtc),
                cancellationToken);
            return Ok(ToResponse(updated));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code == "PROMOTION_NOT_FOUND"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    private static ManagementMenuPromotionResponse ToResponse(MenuPromotionResult promotion) =>
        new(
            promotion.Id,
            promotion.Name,
            promotion.Scope,
            promotion.DiscountKind,
            promotion.DiscountValue,
            promotion.StartsAtUtc,
            promotion.EndsAtUtc,
            promotion.DailyStartLocal,
            promotion.DailyEndLocal,
            promotion.CategoryId,
            promotion.MenuItemId,
            promotion.IsActive,
            promotion.DaysOfWeekMask);
}
