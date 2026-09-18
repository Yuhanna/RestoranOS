using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/customer-menu")]
public sealed class ManagementCustomerMenuSettingsController(
    ICustomerMenuSettingsService settings,
    IWebHostEnvironment environment) : ControllerBase
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

        try
        {
            var updated = await settings.UpdateAsync(
                userId,
                tenantId,
                branchId,
                MenuCatalogMapper.ToSettingsData(request),
                cancellationToken);
            return Ok(MenuCatalogMapper.ToManagementSettingsResponse(updated));
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, exception.Code, exception.Message);
        }
    }

    [HttpPost("settings/logo")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [RequestSizeLimit(2_500_000)]
    [ProducesResponseType(typeof(ManagementCustomerMenuSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadLogoAsync(
        IFormFile? file,
        [FromForm] string? logoAlt,
        [FromForm] string? imageAlt,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        var resolvedAlt = string.IsNullOrWhiteSpace(logoAlt) ? imageAlt : logoAlt;

        if (file is null || file.Length == 0)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "A logo file is required.");
        }

        if (file.Length > 2_000_000)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Logo must be 2 MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp"))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Only JPG, PNG, or WebP images are allowed.");
        }

        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Invalid image content type.");
        }

        try
        {
            var relativeFolder = Path.Combine("media", "branding", tenantId.ToString("N"));
            var absoluteFolder = Path.Combine(environment.ContentRootPath, relativeFolder);
            Directory.CreateDirectory(absoluteFolder);
            var fileName = $"{branchId:N}{extension}";
            var absolutePath = Path.Combine(absoluteFolder, fileName);
            await using (var stream = System.IO.File.Create(absolutePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var updated = await settings.SetLogoAsync(
                userId,
                tenantId,
                branchId,
                $"/media/branding/{tenantId:N}/{fileName}",
                resolvedAlt,
                cancellationToken);
            return Ok(MenuCatalogMapper.ToManagementSettingsResponse(updated));
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, exception.Code, exception.Message);
        }
    }

    [HttpDelete("settings/logo")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementCustomerMenuSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteLogoAsync(CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var updated = await settings.SetLogoAsync(
                userId,
                tenantId,
                branchId,
                logoUrl: null,
                logoAlt: null,
                cancellationToken);
            return Ok(MenuCatalogMapper.ToManagementSettingsResponse(updated));
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
        catch (CustomerExperienceException exception)
        {
            return ApiProblem.Create(StatusCodes.Status404NotFound, exception.Code, exception.Message);
        }
    }

    private static ObjectResult UnauthorizedToken() =>
        ApiProblem.Create(StatusCodes.Status401Unauthorized, "UNAUTHORIZED", "Access token is invalid.");

    private bool TryScope(out Guid userId, out Guid tenantId, out Guid branchId) =>
        PermissionAuthorizationHandler.TryGetScope(User, out userId, out tenantId, out branchId);
}
