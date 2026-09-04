using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/branches")]
public sealed class ManagementBranchesController(IManagementBranchService branchService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ManagementPolicies.BranchManage)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementBranchResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var branches = await branchService.ListBranchesAsync(tenantId, cancellationToken);
        return Ok(branches.Select(branch => ToResponse(branch, branchId)).ToArray());
    }

    [HttpGet("network-summary")]
    [Authorize(Policy = ManagementPolicies.BranchManage)]
    [ProducesResponseType(typeof(ManagementNetworkSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> NetworkSummaryAsync(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out _))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var to = toUtc ?? DateTimeOffset.UtcNow;
        var from = fromUtc ?? to.AddDays(-7);
        try
        {
            var summary = await branchService.GetNetworkSummaryAsync(tenantId, from, to, cancellationToken);
            return Ok(new ManagementNetworkSummaryResponse(
                summary.GrossSalesMinor,
                summary.CompletedOrderCount,
                summary.CancelledOrderCount,
                summary.OpenOrderCount,
                summary.OpenServiceRequestCount,
                summary.BranchCount,
                summary.Currency,
                summary.Branches.Select(branch => new ManagementNetworkBranchStatResponse(
                    branch.BranchId,
                    branch.BranchName,
                    branch.GrossSalesMinor,
                    branch.CompletedOrderCount,
                    branch.CancelledOrderCount,
                    branch.OpenOrderCount,
                    branch.OpenServiceRequestCount,
                    branch.ActiveTableCount,
                    branch.ActiveMemberCount,
                    branch.AverageTicketMinor)).ToArray()));
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
    }

    [HttpGet("billing-preview")]
    [Authorize(Policy = ManagementPolicies.BranchManage)]
    [ProducesResponseType(typeof(ManagementBranchBillingPreviewResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> BillingPreviewAsync(
        [FromServices] IFeatureEntitlementService entitlements,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out _))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var preview = await entitlements.GetBranchBillingPreviewAsync(tenantId, cancellationToken);
        return Ok(new ManagementBranchBillingPreviewResponse(
            preview.PlanCode,
            preview.PlanDisplayName,
            preview.IsTrial,
            preview.TrialEndsAtUtc,
            preview.IncludedBranches,
            preview.PurchasedBranchAddonCount,
            preview.MaxBranches,
            preview.ActiveBranchCount,
            preview.FrozenBranchCount,
            preview.NextBranchRequiresAddon,
            preview.ExtraBranchMonthlyPriceMinor,
            preview.Currency,
            preview.CanUseMultiBranch,
            preview.Summary));
    }

    [HttpPost]
    [Authorize(Policy = ManagementPolicies.BranchManage)]
    [ProducesResponseType(typeof(ManagementBranchResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ManagementCreateBranchRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var branch = await branchService.CreateBranchAsync(
                userId,
                tenantId,
                branchId,
                request?.Name ?? string.Empty,
                request?.ConfirmAddonPurchase ?? false,
                cancellationToken);
            return Created(
                $"/api/v1/management/branches/{branch.Id}",
                ToResponse(branch, branchId));
        }
        catch (EntitlementException exception)
        {
            var status = exception.Code switch
            {
                "BRANCH_ADDON_REQUIRED" => StatusCodes.Status402PaymentRequired,
                "BRANCH_FROZEN" => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status403Forbidden,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "BRANCH_NOT_FOUND" => StatusCodes.Status404NotFound,
                "BRANCH_NAME_IN_USE" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpPatch("{branchId:guid}")]
    [Authorize(Policy = ManagementPolicies.BranchManage)]
    [ProducesResponseType(typeof(ManagementBranchResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RenameAsync(
        Guid branchId,
        [FromBody] ManagementRenameBranchRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var currentBranchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var branch = await branchService.RenameBranchAsync(
                tenantId,
                branchId,
                request?.Name ?? string.Empty,
                cancellationToken);
            return Ok(ToResponse(branch, currentBranchId));
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "BRANCH_NOT_FOUND" => StatusCodes.Status404NotFound,
                "BRANCH_NAME_IN_USE" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpGet("{branchId:guid}/members")]
    [Authorize(Policy = ManagementPolicies.BranchManage)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementBranchMemberResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMembersAsync(Guid branchId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out _))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var members = await branchService.ListMembersAsync(tenantId, branchId, cancellationToken);
            return Ok(members.Select(ToMemberResponse).ToArray());
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(
                exception.Code == "BRANCH_NOT_FOUND" ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
                exception.Code,
                exception.Message);
        }
    }

    [HttpPost("{branchId:guid}/members")]
    [Authorize(Policy = ManagementPolicies.BranchManage)]
    [ProducesResponseType(typeof(ManagementBranchMemberResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> InviteMemberAsync(
        Guid branchId,
        [FromBody] ManagementInviteBranchMemberRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out _))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var member = await branchService.InviteMemberAsync(
                userId,
                tenantId,
                branchId,
                request?.Email ?? string.Empty,
                request?.Password,
                request?.RoleKey ?? "manager",
                cancellationToken);
            return Created(
                $"/api/v1/management/branches/{branchId}/members/{member.MembershipId}",
                ToMemberResponse(member));
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "BRANCH_NOT_FOUND" => StatusCodes.Status404NotFound,
                "MEMBERSHIP_EXISTS" => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpPost("{branchId:guid}/members/{membershipId:guid}/deactivate")]
    [Authorize(Policy = ManagementPolicies.BranchManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeactivateMemberAsync(
        Guid branchId,
        Guid membershipId,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out _))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            await branchService.DeactivateMemberAsync(
                userId,
                tenantId,
                branchId,
                membershipId,
                cancellationToken);
            return NoContent();
        }
        catch (CustomerExperienceException exception)
        {
            var status = exception.Code switch
            {
                "BRANCH_NOT_FOUND" or "MEMBERSHIP_NOT_FOUND" => StatusCodes.Status404NotFound,
                "CANNOT_DEACTIVATE_SELF" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    private static ManagementBranchResponse ToResponse(ManagementBranchResult branch, Guid currentBranchId) =>
        new(
            branch.Id,
            branch.RestaurantId,
            branch.Name,
            branch.ActiveMemberCount,
            branch.ActiveTableCount,
            branch.Id == currentBranchId,
            branch.IsFrozen);

    private static ManagementBranchMemberResponse ToMemberResponse(ManagementBranchMemberResult member) =>
        new(
            member.MembershipId,
            member.UserId,
            member.Email,
            member.RoleName,
            member.RoleKey,
            member.IsActive,
            member.LastLoginAtUtc);
}
