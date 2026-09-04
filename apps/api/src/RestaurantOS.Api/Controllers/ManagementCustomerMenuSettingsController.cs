using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/customer-menu")]
public sealed class ManagementCustomerMenuSettingsController(ICustomerMenuSettingsService settings) : ControllerBase
{
    [HttpGet("settings")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementCustomerMenuSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettingsAsync(CancellationToken cancellationToken)
    {
        if (!TryScope(out _, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        var result = await settings.GetAsync(tenantId, branchId, cancellationToken);
        return Ok(MenuCatalogMapper.ToManagementSettingsResponse(result));
    }

    [HttpPut("settings")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementCustomerMenuSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSettingsAsync(
        [FromBody] ManagementUpdateCustomerMenuSettingsRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        if (request is null)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Settings payload is required.");
        }

        var updated = await settings.UpdateAsync(
            userId,
            tenantId,
            branchId,
            MenuCatalogMapper.ToSettingsData(request),
            cancellationToken);
        return Ok(MenuCatalogMapper.ToManagementSettingsResponse(updated));
    }

    private static ObjectResult UnauthorizedToken() =>
        ApiProblem.Create(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Access token is invalid.");

    private bool TryScope(out Guid userId, out Guid tenantId, out Guid branchId) =>
        PermissionAuthorizationHandler.TryGetScope(User, out userId, out tenantId, out branchId);
}
