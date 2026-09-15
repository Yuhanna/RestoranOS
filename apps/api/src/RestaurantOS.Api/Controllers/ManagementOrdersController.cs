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

    [HttpGet("history")]
    [Authorize(Policy = ManagementPolicies.OrderView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistoryOrdersAsync(
        [FromQuery] int hours = 24,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var orders = await orderService.GetHistoryOrdersAsync(
            userId,
            tenantId,
            branchId,
            hours,
            take,
            cancellationToken);
        return Ok(orders.Select(ToOrderResponse).ToArray());
    }

    [HttpGet("{orderId:guid}")]
    [Authorize(Policy = ManagementPolicies.OrderView)]
    [ProducesResponseType(typeof(ManagementOrderDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var detail = await orderService.GetOrderByIdAsync(
            userId,
            tenantId,
            branchId,
            orderId,
            cancellationToken);
        if (detail is null)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, "ORDER_NOT_FOUND", "Order was not found.");
        }

        return Ok(ToOrderDetailResponse(detail));
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
                result.Currency,
                Guid.Empty,
                string.Empty));
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
            order.Currency,
            order.TableId,
            order.TableLabel);

    private static ManagementOrderDetailResponse ToOrderDetailResponse(ManagementOrderDetailResult detail) =>
        new(
            detail.Id,
            detail.DisplayNumber,
            detail.Status,
            detail.TableId,
            detail.TableLabel,
            detail.CreatedAtUtc,
            detail.StatusChangedAtUtc,
            detail.EstimatedReadyAtUtc,
            detail.SubtotalAmountMinor,
            detail.DiscountAmountMinor,
            detail.TotalAmountMinor,
            detail.Currency,
            detail.Items.Select(item => new ManagementOrderLineResponse(
                item.Id,
                item.MenuItemId,
                item.Name,
                item.Quantity,
                item.ListUnitPriceAmountMinor,
                item.DiscountUnitAmountMinor,
                item.UnitPriceAmountMinor,
                item.Currency,
                item.Note)).ToArray(),
            detail.StatusHistory.Select(entry => new ManagementOrderStatusHistoryResponse(
                entry.Status,
                entry.ChangedAtUtc,
                entry.ChangedByUserId)).ToArray());
}
