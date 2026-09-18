using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/analytics")]
public sealed class ManagementAnalyticsController(
    IManagementAnalyticsService analytics,
    IFeatureEntitlementService entitlements,
    RestaurantOsDbContext dbContext) : ControllerBase
{
    [HttpGet("summary")]
    [Authorize(Policy = ManagementPolicies.AnalyticsView)]
    [ProducesResponseType(typeof(ManagementAnalyticsSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummaryAsync(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var canViewFinancials = await PermissionAuthorizationHandler.HasPermissionAsync(
            dbContext,
            userId,
            tenantId,
            branchId,
            ManagementPermissions.AnalyticsFinancialView,
            cancellationToken);

        try
        {
            await entitlements.EnsureCanUseAnalyticsAsync(tenantId, cancellationToken);
            var (from, to) = await ResolveRangeAsync(tenantId, branchId, fromUtc, toUtc, cancellationToken);
            var summary = await analytics.GetSummaryAsync(tenantId, branchId, from, to, cancellationToken);
            return Ok(new ManagementAnalyticsSummaryResponse(
                summary.GrossSalesMinor,
                canViewFinancials ? summary.EstimatedCostMinor : null,
                canViewFinancials ? summary.GrossProfitMinor : null,
                summary.CancelledSalesMinor,
                summary.CompletedOrderCount,
                summary.CancelledOrderCount,
                summary.OpenServiceRequestCount,
                summary.AverageTicketMinor,
                summary.Currency,
                canViewFinancials));
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
    }

    [HttpGet("sales-by-period")]
    [Authorize(Policy = ManagementPolicies.AnalyticsView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementSalesPeriodResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesByPeriodAsync(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] string granularity = "day",
        CancellationToken cancellationToken = default)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var canViewFinancials = await PermissionAuthorizationHandler.HasPermissionAsync(
            dbContext,
            userId,
            tenantId,
            branchId,
            ManagementPermissions.AnalyticsFinancialView,
            cancellationToken);

        try
        {
            await entitlements.EnsureCanUseAnalyticsAsync(tenantId, cancellationToken);
            var (from, to) = await ResolveRangeAsync(tenantId, branchId, fromUtc, toUtc, cancellationToken);
            var rows = await analytics.GetSalesByPeriodAsync(
                tenantId,
                branchId,
                from,
                to,
                granularity,
                cancellationToken);
            return Ok(rows.Select(row => new ManagementSalesPeriodResponse(
                row.PeriodStartUtc,
                row.GrossSalesMinor,
                canViewFinancials ? row.GrossProfitMinor : null,
                row.OrderCount)).ToArray());
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
    }

    [HttpGet("top-items")]
    [Authorize(Policy = ManagementPolicies.AnalyticsView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementTopItemResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopItemsAsync(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var canViewFinancials = await PermissionAuthorizationHandler.HasPermissionAsync(
            dbContext,
            userId,
            tenantId,
            branchId,
            ManagementPermissions.AnalyticsFinancialView,
            cancellationToken);

        try
        {
            await entitlements.EnsureCanUseAnalyticsAsync(tenantId, cancellationToken);
            var (from, to) = await ResolveRangeAsync(tenantId, branchId, fromUtc, toUtc, cancellationToken);
            var rows = await analytics.GetTopItemsAsync(tenantId, branchId, from, to, limit, cancellationToken);
            return Ok(rows.Select(row => new ManagementTopItemResponse(
                row.MenuItemId,
                row.Name,
                row.QuantitySold,
                row.RevenueMinor,
                canViewFinancials ? row.EstimatedCostMinor : null,
                canViewFinancials ? row.GrossProfitMinor : null)).ToArray());
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, exception.Code, exception.Message);
        }
    }

    private async Task<(DateTimeOffset From, DateTimeOffset To)> ResolveRangeAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        var usage = await entitlements.GetUsageAsync(tenantId, branchId, cancellationToken);
        var maxHours = Math.Max(1, usage.MaxOrderHistoryHours);
        var to = (toUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var defaultDays = MonetizationPolicy.ResolveMaxAnalyticsDays(maxHours);
        var requestedFrom = (fromUtc ?? to.AddDays(-defaultDays)).ToUniversalTime();
        if (requestedFrom > to)
        {
            requestedFrom = to.AddHours(-Math.Min(24, maxHours));
        }

        var span = to - requestedFrom;
        if (span > TimeSpan.FromHours(maxHours))
        {
            requestedFrom = to.AddHours(-maxHours);
        }

        return (requestedFrom, to);
    }
}
