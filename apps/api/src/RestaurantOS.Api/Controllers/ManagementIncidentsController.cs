using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/incidents")]
public sealed class ManagementIncidentsController(IIncidentQueryService incidents) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ManagementPolicies.OrderView)]
    [ProducesResponseType(typeof(ManagementIncidentResponse[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> QueryAsync(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] string? code,
        [FromQuery] string? channel,
        [FromQuery] string? severity,
        [FromQuery] Guid? tableId,
        [FromQuery] Guid? orderId,
        [FromQuery] string? correlationId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var rows = await incidents.QueryAsync(
                userId,
                tenantId,
                branchId,
                fromUtc,
                toUtc,
                code,
                channel,
                severity,
                tableId,
                orderId,
                correlationId,
                limit,
                cancellationToken);
            return Ok(rows.Select(ToResponse).ToArray());
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    private static ManagementIncidentResponse ToResponse(IncidentEventResult row) =>
        new(
            row.Id,
            row.OccurredAtUtc,
            row.ExpiresAtUtc,
            row.TenantId,
            row.BranchId,
            row.RestaurantId,
            row.Channel,
            row.Severity,
            row.Code,
            row.HttpStatus,
            row.Message,
            row.CorrelationId,
            row.ActorType,
            row.ActorUserId,
            row.CustomerSessionId,
            row.GuestSessionId,
            row.TableId,
            row.OrderId,
            row.RequestMethod,
            row.RequestPath,
            row.DetailJson);
}
