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
            return Ok(ToUsageResponse(usage));
        }
        catch (EntitlementException exception)
        {
            var status = exception.Code == "ENTERPRISE_QUOTE_REQUIRED"
                ? StatusCodes.Status402PaymentRequired
                : StatusCodes.Status400BadRequest;
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpPost("enterprise-quote")]
    [Authorize(Policy = ManagementPolicies.SubscriptionManage)]
    [ProducesResponseType(typeof(ManagementEnterpriseQuoteResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> RequestEnterpriseQuoteAsync(
        [FromBody] ManagementEnterpriseQuoteRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out _))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        if (request is null)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Request body is required.");
        }

        try
        {
            var result = await entitlements.RequestEnterpriseQuoteAsync(
                tenantId,
                userId,
                request.ContactName,
                request.Email,
                request.Phone,
                request.EstimatedBranchCount,
                request.Note,
                cancellationToken);
            return Created(
                $"/api/v1/management/subscription/enterprise-quote/{result.Id}",
                new ManagementEnterpriseQuoteResponse(result.Id, result.CreatedAtUtc, result.Message));
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
    }

    private static ManagementEntitlementUsageResponse ToUsageResponse(TenantEntitlementUsageResult usage) =>
        new(
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
            usage.TrialEndsAtUtc,
            IncludedBranches: usage.IncludedBranches,
            PurchasedBranchAddonCount: usage.PurchasedBranchAddonCount,
            FrozenBranchCount: usage.FrozenBranchCount,
            ActiveBranchCount: usage.ActiveBranchCount,
            ExtraBranchMonthlyPriceMinor: usage.ExtraBranchMonthlyPriceMinor,
            BillingCurrency: usage.BillingCurrency,
            NextBranchRequiresAddon: usage.NextBranchRequiresAddon);
}
