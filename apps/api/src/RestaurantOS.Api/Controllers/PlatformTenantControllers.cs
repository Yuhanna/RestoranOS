using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/platform/tenants")]
public sealed class PlatformTenantsController(
    IPlatformTenantBillingService tenants,
    RestaurantOsDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementPlatformTenantListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? q,
        [FromQuery] string? plan,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await tenants.ListTenantsAsync(q, plan, skip, take, cancellationToken);
        return Ok(new ManagementPlatformTenantListResponse(
            result.Items.Select(ToListItem).ToArray(),
            result.Total,
            result.Skip,
            result.Take));
    }

    [HttpGet("{tenantId:guid}")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementPlatformTenantDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetPlatformActor(User, out var actorUserId, out _))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            return Ok(ToDetail(await tenants.GetTenantAsync(actorUserId, tenantId, cancellationToken)));
        }
        catch (CustomerExperienceException exception)
        {
            return TenantProblem(exception);
        }
    }

    [HttpPatch("{tenantId:guid}/subscription")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementPlatformTenantDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSubscriptionAsync(
        Guid tenantId,
        [FromBody] ManagementUpdatePlatformTenantSubscriptionRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.PlanCode))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "planCode is required.");
        }

        var actor = await PermissionAuthorizationHandler.TryGetLivePlatformActorAsync(User, dbContext, cancellationToken);
        if (actor is null)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        if (!PlatformStaffRoles.CanWriteTenants(actor.Value.RoleCode))
        {
            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "PLATFORM_ROLE_DENIED",
                "Üye aboneliğini yalnızca Owner veya Billing değiştirir.");
        }

        try
        {
            var updated = await tenants.UpdateSubscriptionAsync(
                actor.Value.UserId,
                actor.Value.RoleCode,
                tenantId,
                new UpdatePlatformTenantSubscriptionCommand(
                    request.PlanCode,
                    request.ExpiresAtUtc,
                    request.PurchasedBranchAddonCount,
                    request.OverrideMaxBranches,
                    request.OverrideMaxActiveUsers,
                    request.OverrideMaxOrderHistoryHours,
                    request.OverrideMaxActiveQrCodes,
                    request.ContractNote),
                cancellationToken);
            return Ok(ToDetail(updated));
        }
        catch (CustomerExperienceException exception)
        {
            return TenantProblem(exception);
        }
    }

    private static ManagementPlatformTenantListItemResponse ToListItem(PlatformTenantListItemResult item) =>
        new(
            item.TenantId,
            item.TenantName,
            item.RestaurantName,
            item.PlanCode,
            item.IsTrial,
            item.ExpiresAtUtc,
            item.ActiveBranchCount,
            item.MaxBranches,
            item.OpenQuoteCount);

    private static ManagementPlatformTenantDetailResponse ToDetail(PlatformTenantDetailResult item) =>
        new(
            item.TenantId,
            item.TenantName,
            item.RestaurantName,
            item.BillingEmail,
            item.PlanCode,
            item.IsTrial,
            item.StartedAtUtc,
            item.ExpiresAtUtc,
            item.PurchasedBranchAddonCount,
            item.ActiveBranchCount,
            item.FrozenBranchCount,
            item.ActiveUserCount,
            item.MaxBranches,
            item.MaxActiveUsers,
            item.MaxOrderHistoryHours,
            item.MaxActiveQrCodes,
            item.OverrideMaxBranches,
            item.OverrideMaxActiveUsers,
            item.OverrideMaxOrderHistoryHours,
            item.OverrideMaxActiveQrCodes,
            item.ContractNote);

    private static ObjectResult TenantProblem(CustomerExperienceException exception)
    {
        var status = exception.Code switch
        {
            "TENANT_NOT_FOUND" => StatusCodes.Status404NotFound,
            "PLATFORM_ROLE_DENIED" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        return ApiProblem.Create(status, exception.Code, exception.Message);
    }
}

[ApiController]
[Route("api/v1/platform/quotes")]
public sealed class PlatformQuotesController(
    IPlatformTenantBillingService tenants,
    RestaurantOsDbContext dbContext) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementPlatformQuoteListItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var items = await tenants.ListQuotesAsync(status, cancellationToken);
        return Ok(items.Select(ToListItem).ToArray());
    }

    [HttpGet("{quoteId:guid}")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementPlatformQuoteDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAsync(Guid quoteId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(ToDetail(await tenants.GetQuoteAsync(quoteId, cancellationToken)));
        }
        catch (CustomerExperienceException exception)
        {
            return QuoteProblem(exception);
        }
    }

    [HttpPost("{quoteId:guid}/accept")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementPlatformQuoteDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptAsync(
        Guid quoteId,
        [FromBody] ManagementAcceptPlatformQuoteRequest? request,
        CancellationToken cancellationToken)
    {
        var actor = await PermissionAuthorizationHandler.TryGetLivePlatformActorAsync(User, dbContext, cancellationToken);
        if (actor is null)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        if (!PlatformStaffRoles.CanAcceptQuotes(actor.Value.RoleCode))
        {
            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "PLATFORM_ROLE_DENIED",
                "Enterprise sözleşmesini yalnızca Owner veya Billing onaylar.");
        }

        try
        {
            var accepted = await tenants.AcceptQuoteAsync(
                actor.Value.UserId,
                actor.Value.RoleCode,
                quoteId,
                new AcceptPlatformQuoteCommand(
                    request?.OverrideMaxBranches,
                    request?.OverrideMaxActiveUsers,
                    request?.OverrideMaxOrderHistoryHours,
                    request?.OverrideMaxActiveQrCodes,
                    request?.ContractNote,
                    request?.ExpiresAtUtc),
                cancellationToken);
            return Ok(ToDetail(accepted));
        }
        catch (CustomerExperienceException exception)
        {
            return QuoteProblem(exception);
        }
    }

    [HttpPost("{quoteId:guid}/reject")]
    [Authorize(Policy = ManagementPolicies.PlatformManage)]
    [ProducesResponseType(typeof(ManagementPlatformQuoteDetailResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectAsync(
        Guid quoteId,
        [FromBody] ManagementRejectPlatformQuoteRequest? request,
        CancellationToken cancellationToken)
    {
        var actor = await PermissionAuthorizationHandler.TryGetLivePlatformActorAsync(User, dbContext, cancellationToken);
        if (actor is null)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        if (!PlatformStaffRoles.CanRejectQuotes(actor.Value.RoleCode))
        {
            return ApiProblem.Create(
                StatusCodes.Status403Forbidden,
                "PLATFORM_ROLE_DENIED",
                "Teklifi Owner, Billing veya Support reddedebilir.");
        }

        try
        {
            var rejected = await tenants.RejectQuoteAsync(
                actor.Value.UserId,
                actor.Value.RoleCode,
                quoteId,
                request?.DecisionNote,
                cancellationToken);
            return Ok(ToDetail(rejected));
        }
        catch (CustomerExperienceException exception)
        {
            return QuoteProblem(exception);
        }
    }

    private static ManagementPlatformQuoteListItemResponse ToListItem(PlatformQuoteListItemResult item) =>
        new(item.Id, item.TenantId, item.TenantName, item.EstimatedBranchCount, item.Status, item.CreatedAtUtc);

    private static ManagementPlatformQuoteDetailResponse ToDetail(PlatformQuoteDetailResult item) =>
        new(
            item.Id,
            item.TenantId,
            item.TenantName,
            item.ContactName,
            item.Email,
            item.Phone,
            item.EstimatedBranchCount,
            item.Note,
            item.Status,
            item.CreatedAtUtc,
            item.ReviewedByUserId,
            item.ReviewedAtUtc,
            item.DecisionNote);

    private static ObjectResult QuoteProblem(CustomerExperienceException exception)
    {
        var status = exception.Code switch
        {
            "QUOTE_NOT_FOUND" => StatusCodes.Status404NotFound,
            "QUOTE_NOT_OPEN" or "PLATFORM_ROLE_DENIED" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        return ApiProblem.Create(status, exception.Code, exception.Message);
    }
}
