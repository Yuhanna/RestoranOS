using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management")]
public sealed class ManagementMenusController(IManagementMenuService menus) : ControllerBase
{
    [HttpGet("menus")]
    [Authorize(Policy = ManagementPolicies.MenuView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementMenuSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var result = await menus.ListMenusAsync(userId, tenantId, branchId, cancellationToken);
            return Ok(result.Select(ToSummary).ToArray());
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpPost("menus")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementMenuSummaryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] ManagementCreateMenuRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var created = await menus.CreateMenuAsync(
                userId,
                tenantId,
                branchId,
                request?.Name ?? string.Empty,
                cancellationToken);
            return Created($"/api/v1/management/menus/{created.Id}", ToSummary(created));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpGet("menus/{menuId:guid}")]
    [Authorize(Policy = ManagementPolicies.MenuView)]
    [ProducesResponseType(typeof(ManagementMenuDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(Guid menuId, CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var detail = await menus.GetMenuAsync(userId, tenantId, branchId, menuId, cancellationToken);
            return Ok(new ManagementMenuDetailResponse(
                detail.Id,
                detail.Name,
                detail.Lifecycle,
                detail.PublishedAtUtc,
                detail.Categories.Select(ToCategory).ToArray(),
                detail.Items.Select(ToItem).ToArray()));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpPatch("menus/{menuId:guid}")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementMenuSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RenameAsync(
        Guid menuId,
        [FromBody] ManagementRenameMenuRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var result = await menus.RenameMenuAsync(
                userId,
                tenantId,
                branchId,
                menuId,
                request?.Name ?? string.Empty,
                cancellationToken);
            return Ok(ToSummary(result));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpPost("menus/{menuId:guid}/publish")]
    [Authorize(Policy = ManagementPolicies.MenuPublish)]
    [ProducesResponseType(typeof(ManagementMenuSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> PublishAsync(Guid menuId, CancellationToken cancellationToken) =>
        MutateAsync(menuId, (service, userId, tenantId, branchId, id, token) =>
            service.PublishMenuAsync(userId, tenantId, branchId, id, token), cancellationToken);

    [HttpPost("menus/{menuId:guid}/unpublish")]
    [Authorize(Policy = ManagementPolicies.MenuPublish)]
    [ProducesResponseType(typeof(ManagementMenuSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> UnpublishAsync(Guid menuId, CancellationToken cancellationToken) =>
        MutateAsync(menuId, (service, userId, tenantId, branchId, id, token) =>
            service.UnpublishMenuAsync(userId, tenantId, branchId, id, token), cancellationToken);

    [HttpPost("menus/{menuId:guid}/archive")]
    [Authorize(Policy = ManagementPolicies.MenuPublish)]
    [ProducesResponseType(typeof(ManagementMenuSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> ArchiveAsync(Guid menuId, CancellationToken cancellationToken) =>
        MutateAsync(menuId, (service, userId, tenantId, branchId, id, token) =>
            service.ArchiveMenuAsync(userId, tenantId, branchId, id, token), cancellationToken);

    [HttpPost("menus/{menuId:guid}/categories")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementMenuCategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddCategoryAsync(
        Guid menuId,
        [FromBody] ManagementCreateCategoryRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var category = await menus.AddCategoryAsync(
                userId,
                tenantId,
                branchId,
                menuId,
                request?.Name ?? string.Empty,
                request?.SortOrder ?? 0,
                cancellationToken);
            return Created(
                $"/api/v1/management/menu-categories/{category.Id}",
                ToCategory(category));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpPatch("menu-categories/{categoryId:guid}")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementMenuCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateCategoryAsync(
        Guid categoryId,
        [FromBody] ManagementUpdateCategoryRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var category = await menus.UpdateCategoryAsync(
                userId,
                tenantId,
                branchId,
                categoryId,
                request?.Name,
                request?.SortOrder,
                cancellationToken);
            return Ok(ToCategory(category));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpPost("menus/{menuId:guid}/items")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementMenuItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddItemAsync(
        Guid menuId,
        [FromBody] ManagementCreateMenuItemRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        if (request is null || request.CategoryId == Guid.Empty)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Category, name, and price are required.");
        }

        try
        {
            var item = await menus.AddItemAsync(
                userId,
                tenantId,
                branchId,
                menuId,
                request.CategoryId,
                request.Name,
                request.Description ?? string.Empty,
                request.AmountMinor,
                request.IsAvailable,
                request.SortOrder,
                request.ImageUrl,
                request.ImageAlt,
                cancellationToken,
                request.PrepTimeSeconds);
            return Created($"/api/v1/management/menu-items/{item.Id}", ToItem(item));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpPatch("menu-items/{itemId:guid}")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(ManagementMenuItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateItemAsync(
        Guid itemId,
        [FromBody] ManagementUpdateMenuItemRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var item = await menus.UpdateItemAsync(
                userId,
                tenantId,
                branchId,
                itemId,
                request?.CategoryId,
                request?.Name,
                request?.Description,
                request?.AmountMinor,
                request?.IsAvailable,
                request?.SortOrder,
                request?.ImageUrl,
                request?.ImageAlt,
                cancellationToken,
                request?.PrepTimeSeconds,
                request?.UpdatePrepTime == true);
            return Ok(ToItem(item));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpPost("menu-items/{itemId:guid}/image")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [RequestSizeLimit(2_500_000)]
    [ProducesResponseType(typeof(ManagementMenuItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadItemImageAsync(
        Guid itemId,
        IFormFile? file,
        [FromForm] string? imageAlt,
        [FromServices] IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        if (file is null || file.Length == 0)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "An image file is required.");
        }

        if (file.Length > 2_000_000)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "Image must be 2 MB or smaller.");
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
            var relativeFolder = Path.Combine("media", "menu-items", tenantId.ToString("N"));
            var absoluteFolder = Path.Combine(environment.ContentRootPath, relativeFolder);
            Directory.CreateDirectory(absoluteFolder);
            var fileName = $"{itemId:N}{extension}";
            var absolutePath = Path.Combine(absoluteFolder, fileName);
            await using (var stream = System.IO.File.Create(absolutePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var publicPath = $"/media/menu-items/{tenantId:N}/{fileName}";
            var item = await menus.SetItemImageAsync(
                userId,
                tenantId,
                branchId,
                itemId,
                publicPath,
                imageAlt,
                cancellationToken);
            return Ok(ToItem(item));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpPut("menu-categories/{categoryId:guid}/translations/{locale}")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(MenuTextTranslationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertCategoryTranslationAsync(
        Guid categoryId,
        string locale,
        [FromBody] ManagementUpsertTranslationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var translation = await menus.UpsertCategoryTranslationAsync(
                userId,
                tenantId,
                branchId,
                categoryId,
                locale,
                request?.Name ?? string.Empty,
                cancellationToken);
            return Ok(ToTranslation(translation));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    [HttpPut("menu-items/{itemId:guid}/translations/{locale}")]
    [Authorize(Policy = ManagementPolicies.MenuEdit)]
    [ProducesResponseType(typeof(MenuTextTranslationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpsertItemTranslationAsync(
        Guid itemId,
        string locale,
        [FromBody] ManagementUpsertTranslationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var translation = await menus.UpsertItemTranslationAsync(
                userId,
                tenantId,
                branchId,
                itemId,
                locale,
                request?.Name ?? string.Empty,
                request?.Description ?? string.Empty,
                cancellationToken);
            return Ok(ToTranslation(translation));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    private async Task<IActionResult> MutateAsync(
        Guid menuId,
        Func<IManagementMenuService, Guid, Guid, Guid, Guid, CancellationToken, Task<ManagementMenuSummaryResult>> action,
        CancellationToken cancellationToken)
    {
        if (!TryScope(out var userId, out var tenantId, out var branchId))
        {
            return UnauthorizedToken();
        }

        try
        {
            var result = await action(menus, userId, tenantId, branchId, menuId, cancellationToken);
            return Ok(ToSummary(result));
        }
        catch (CustomerExperienceException exception)
        {
            return MenuProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return EntitlementDenied(exception);
        }
        catch (ManagementAuthException exception)
        {
            return Forbidden(exception);
        }
    }

    private bool TryScope(out Guid userId, out Guid tenantId, out Guid branchId) =>
        PermissionAuthorizationHandler.TryGetScope(User, out userId, out tenantId, out branchId);

    private static ManagementMenuSummaryResponse ToSummary(ManagementMenuSummaryResult menu) =>
        new(menu.Id, menu.Name, menu.Lifecycle, menu.PublishedAtUtc, menu.CategoryCount, menu.ItemCount);

    private static ManagementMenuCategoryResponse ToCategory(ManagementMenuCategoryResult category) =>
        new(category.Id, category.MenuId, category.Name, category.SortOrder, category.Translations.Select(ToTranslation).ToArray());

    private static ManagementMenuItemResponse ToItem(ManagementMenuItemResult item) =>
        new(
            item.Id,
            item.MenuId,
            item.CategoryId,
            item.Name,
            item.Description,
            item.AmountMinor,
            item.Currency,
            item.IsAvailable,
            item.SortOrder,
            item.ImageUrl,
            item.ImageAlt,
            item.Translations.Select(ToTranslation).ToArray(),
            item.PrepTimeSeconds);

    private static MenuTextTranslationResponse ToTranslation(MenuTextTranslationResult translation) =>
        new(translation.Locale, translation.Name, translation.Description);

    private static ObjectResult UnauthorizedToken() =>
        ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");

    private static ObjectResult Forbidden(ManagementAuthException exception) =>
        ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);

    private static ObjectResult EntitlementDenied(EntitlementException exception) =>
        ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);

    private static ObjectResult MenuProblem(CustomerExperienceException exception)
    {
        var status = exception.Code switch
        {
            "MENU_NOT_FOUND" or "CATEGORY_NOT_FOUND" or "PRODUCT_NOT_FOUND" => StatusCodes.Status404NotFound,
            "MENU_ARCHIVED" or "MENU_EMPTY" => StatusCodes.Status409Conflict,
            "UNSUPPORTED_LOCALE" => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status400BadRequest,
        };
        return ApiProblem.Create(status, exception.Code, exception.Message);
    }
}
