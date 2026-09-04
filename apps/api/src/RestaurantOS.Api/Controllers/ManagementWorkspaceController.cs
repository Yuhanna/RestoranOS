using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management")]
public sealed class ManagementWorkspaceController(
    RestaurantOsDbContext dbContext,
    IFeatureEntitlementService entitlements) : ControllerBase
{
    /// <summary>
    /// Returns only the restaurant/branch bound to the caller's JWT scope.
    /// Client-supplied restaurant or branch ids are never trusted.
    /// </summary>
    [HttpGet("workspace")]
    [Authorize(Policy = ManagementPolicies.OrderView)]
    [ProducesResponseType(typeof(ManagementWorkspaceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetWorkspaceAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        var workspace = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.Id == branchId && branch.TenantId == tenantId)
            .Select(branch => new
            {
                branch.TenantId,
                branch.RestaurantId,
                RestaurantName = branch.Restaurant.Name,
                BranchId = branch.Id,
                BranchName = branch.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (workspace is null)
        {
            return ApiProblem.Create(
                StatusCodes.Status404NotFound,
                "WORKSPACE_NOT_FOUND",
                "Workspace was not found for the current scope.");
        }

        var usage = await entitlements.GetUsageAsync(tenantId, branchId, cancellationToken);
        var permissions = await dbContext.ManagementMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == userId
                && membership.TenantId == tenantId
                && membership.BranchId == branchId
                && membership.IsActive)
            .SelectMany(membership => dbContext.ManagementRolePermissions
                .Where(grant => grant.RoleId == membership.RoleId)
                .Select(grant => grant.Permission))
            .Distinct()
            .OrderBy(permission => permission)
            .ToListAsync(cancellationToken);
        var canViewFinancialAnalytics = permissions.Contains(ManagementPermissions.AnalyticsFinancialView);
        return Ok(new ManagementWorkspaceResponse(
            workspace.TenantId,
            workspace.RestaurantId,
            workspace.RestaurantName,
            workspace.BranchId,
            workspace.BranchName,
            new ManagementEntitlementUsageResponse(
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
                canViewFinancialAnalytics,
                usage.IncludedBranches,
                usage.PurchasedBranchAddonCount,
                usage.FrozenBranchCount,
                usage.ActiveBranchCount,
                usage.ExtraBranchMonthlyPriceMinor,
                usage.BillingCurrency,
                usage.NextBranchRequiresAddon),
            (usage.Notifications ?? [])
                .Select(x => new ManagementAudienceNotificationResponse(x.Id, x.Title, x.Body, x.ActionUrl))
                .ToArray(),
            (usage.SubscriptionOffers ?? [])
                .Select(x => new ManagementSubscriptionOfferResponse(
                    x.Id,
                    x.TargetPlanCode,
                    x.DiscountPercent,
                    x.DurationMonths,
                    x.Title,
                    x.Body))
                .ToArray(),
            permissions));
    }
}
