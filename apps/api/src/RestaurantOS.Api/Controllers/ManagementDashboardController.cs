using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/dashboard")]
public sealed class ManagementDashboardController(
    IManagementDashboardService dashboard,
    RestaurantOsDbContext dbContext) : ControllerBase
{
    [HttpGet("today")]
    [Authorize(Policy = ManagementPolicies.OrderView)]
    [ProducesResponseType(typeof(ManagementTodayDashboardResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTodayAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var summary = await dashboard.GetTodayAsync(tenantId, branchId, cancellationToken);
        var canViewFinancials = await PermissionAuthorizationHandler.HasPermissionAsync(
            dbContext,
            userId,
            tenantId,
            branchId,
            ManagementPermissions.AnalyticsFinancialView,
            cancellationToken);
        return Ok(new ManagementTodayDashboardResponse(
            summary.TodaysOrderCount,
            canViewFinancials ? summary.TodaysRevenueMinor : 0,
            summary.OpenTablesCount,
            summary.PendingOrdersCount,
            summary.CompletedOrdersTodayCount,
            summary.Currency,
            summary.DayStartUtc,
            summary.DayEndUtc,
            canViewFinancials));
    }
}
