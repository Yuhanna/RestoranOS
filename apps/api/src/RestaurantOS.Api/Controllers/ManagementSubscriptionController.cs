using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/subscription")]
public sealed class ManagementSubscriptionController(IFeatureEntitlementService entitlements) : ControllerBase
{
    [HttpPost("checkout")]
    [Authorize(Policy = ManagementPolicies.SubscriptionManage)]
    [ProducesResponseType(typeof(ManagementEntitlementUsageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckoutAsync(
        [FromBody] ManagementSubscriptionCheckoutRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.PlanCode))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Plan code is required.");
        }

        try
        {
            var usage = await entitlements.ConvertToPaidPlanAsync(
                tenantId,
                branchId,
                request.PlanCode,
                cancellationToken);
            return Ok(new ManagementEntitlementUsageResponse(
                usage.PlanCode,
                usage.PlanDisplayName,
                usage.BranchCount,
                usage.MaxBranches,
                usage.TableCount,
                usage.MaxTablesPerBranch,
                usage.ActiveUserCount,
                usage.MaxActiveUsers,
                usage.CanUseProductImages,
                usage.CanUseMenuTranslations,
                usage.CanManageAdditionalRoles,
                usage.CanUseLiveOrderPanel,
                usage.CanUseMultiBranch,
                usage.HasPrioritySupport,
                usage.Warnings,
                usage.IsTrial,
                usage.TrialEndsAtUtc));
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
    }
}
