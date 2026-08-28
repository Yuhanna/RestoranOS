using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/orders")]
public sealed class ManagementOrdersController(IManagementOrderService orderService) : ControllerBase
{
    [HttpGet("active")]
    [Authorize(Policy = ManagementPolicies.OrderView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetActiveOrdersAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var orders = await orderService.GetActiveOrdersAsync(
            userId,
            tenantId,
            branchId,
            cancellationToken);
        return Ok(orders.Select(ToOrderResponse).ToArray());
    }

    [HttpPut("{orderId:guid}/status")]
    [Authorize(Policy = ManagementPolicies.OrderModify)]
    [ProducesResponseType(typeof(ManagementOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangeOrderStatusAsync(
        Guid orderId,
        [FromBody] ManagementChangeOrderStatusRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.Status)
            || request.ExpectedStatusChangedAtUtc == default)
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                "Order status and expected status-change time are required.");
        }

        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var result = await orderService.ChangeOrderStatusAsync(
                userId,
                tenantId,
                branchId,
                orderId,
                request.Status,
                request.ExpectedStatusChangedAtUtc,
                cancellationToken,
                request.EstimatedReadyAtUtc);
            return Ok(new ManagementOrderResponse(
                result.Id,
                result.DisplayNumber,
                result.Status,
                result.CreatedAtUtc,
                result.StatusChangedAtUtc,
                result.EstimatedReadyAtUtc,
                result.AmountMinor,
                result.Currency));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "ORDER_NOT_FOUND" => StatusCodes.Status404NotFound,
                "ORDER_CONCURRENCY_CONFLICT" or "INVALID_STATUS_TRANSITION" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    private static ManagementOrderResponse ToOrderResponse(ManagementOrderResult order) =>
        new(
            order.Id,
            order.DisplayNumber,
            order.Status,
            order.CreatedAtUtc,
            order.StatusChangedAtUtc,
            order.EstimatedReadyAtUtc,
            order.AmountMinor,
            order.Currency);
}
