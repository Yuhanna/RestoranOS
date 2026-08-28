using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/service-requests")]
public sealed class ManagementServiceRequestsController(
    IManagementServiceRequestService serviceRequests) : ControllerBase
{
    [HttpGet("open")]
    [Authorize(Policy = ManagementPolicies.OrderView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementServiceRequestResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListOpenAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var items = await serviceRequests.ListOpenAsync(userId, tenantId, branchId, cancellationToken);
            return Ok(items.Select(ToResponse).ToArray());
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    [HttpPost("{requestId:guid}/complete")]
    [Authorize(Policy = ManagementPolicies.OrderModify)]
    [ProducesResponseType(typeof(ManagementServiceRequestResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteAsync(Guid requestId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var item = await serviceRequests.CompleteAsync(userId, tenantId, branchId, requestId, cancellationToken);
            return Ok(ToResponse(item));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "SERVICE_REQUEST_NOT_FOUND" => StatusCodes.Status404NotFound,
                "SERVICE_REQUEST_CLOSED" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    private static ManagementServiceRequestResponse ToResponse(ServiceRequestResult item) =>
        new(
            item.Id,
            item.TableId,
            item.TableLabel,
            item.Type,
            item.Status,
            item.Note,
            item.CreatedAtUtc,
            item.CompletedAtUtc);
}
