using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/lunch-packages")]
public sealed class ManagementLunchPackagesController(IMenuPackageManagementService packages) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = ManagementPolicies.MenuView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementLunchPackageResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        var items = await packages.ListAsync(tenantId, branchId, cancellationToken);
        return Ok(items.Select(ToResponse).ToArray());
    }

    [HttpPost]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementLunchPackageResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ManagementUpsertLunchPackageRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Name is required.");
        }

        try
        {
            var created = await packages.CreateAsync(tenantId, branchId, ToCommand(request), cancellationToken);
            return Created($"/api/v1/management/lunch-packages/{created.Id}", ToResponse(created));
        }
        catch (CustomerExperienceException exception)
        {
            return MapException(exception);
        }
    }

    [HttpPut("{packageId:guid}")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementLunchPackageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAsync(
        Guid packageId,
        [FromBody] ManagementUpsertLunchPackageRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Name))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Name is required.");
        }

        try
        {
            var updated = await packages.UpdateAsync(
                tenantId,
                branchId,
                packageId,
                ToCommand(request),
                cancellationToken);
            return Ok(ToResponse(updated));
        }
        catch (CustomerExperienceException exception)
        {
            return MapException(exception);
        }
    }

    [HttpPatch("{packageId:guid}/active")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementLunchPackageResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetActiveAsync(
        Guid packageId,
        [FromBody] ManagementSetLunchPackageActiveRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        if (request is null)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Body is required.");
        }

        try
        {
            var updated = await packages.SetActiveAsync(
                tenantId,
                branchId,
                packageId,
                request.IsActive,
                cancellationToken);
            return Ok(ToResponse(updated));
        }
        catch (CustomerExperienceException exception)
        {
            return MapException(exception);
        }
    }

    [HttpDelete("{packageId:guid}")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAsync(Guid packageId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out _, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_ACCESS_TOKEN",
                "Access token is invalid.");
        }

        try
        {
            await packages.DeleteAsync(tenantId, branchId, packageId, cancellationToken);
            return NoContent();
        }
        catch (CustomerExperienceException exception)
        {
            return MapException(exception);
        }
    }

    private static ObjectResult MapException(CustomerExperienceException exception)
    {
        var status = exception.Code switch
        {
            "PACKAGE_NOT_FOUND" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest,
        };
        return ApiProblem.Create(status, exception.Code, exception.Message);
    }

    private static UpsertMenuPackageCommand ToCommand(ManagementUpsertLunchPackageRequest request) =>
        new(
            request.Name,
            request.Description,
            request.PriceAmountMinor,
            request.Currency ?? "TRY",
            request.IsActive,
            request.SortOrder,
            ParseTime(request.DailyStartLocal),
            ParseTime(request.DailyEndLocal),
            request.DaysOfWeekMask,
            (request.Components ?? [])
                .Select((c, index) => new MenuPackageComponentInput(
                    c.MenuItemId,
                    c.SlotLabel,
                    c.SortOrder == 0 ? index : c.SortOrder))
                .ToArray());

    private static TimeOnly? ParseTime(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : TimeOnly.Parse(value, CultureInfo.InvariantCulture);

    private static ManagementLunchPackageResponse ToResponse(MenuPackageResult package) =>
        new(
            package.Id,
            package.Name,
            package.Description,
            package.PriceAmountMinor,
            package.Currency,
            package.IsActive,
            package.SortOrder,
            package.DailyStartLocal?.ToString("HH\\:mm\\:ss", CultureInfo.InvariantCulture),
            package.DailyEndLocal?.ToString("HH\\:mm\\:ss", CultureInfo.InvariantCulture),
            package.DaysOfWeekMask,
            package.ComponentsListTotalMinor,
            package.Components.Select(c => new ManagementLunchPackageComponentResponse(
                c.Id,
                c.MenuItemId,
                c.MenuItemName,
                c.SlotLabel,
                c.SortOrder,
                c.ListAmountMinor)).ToArray());
}
