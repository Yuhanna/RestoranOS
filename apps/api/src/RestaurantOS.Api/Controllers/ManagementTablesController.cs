using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management")]
public sealed class ManagementTablesController(IManagementTableService tableService) : ControllerBase
{
    [HttpGet("tables")]
    [Authorize(Policy = ManagementPolicies.TableView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementTableResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListTablesAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var tables = await tableService.ListTablesAsync(userId, tenantId, branchId, cancellationToken);
            return Ok(tables.Select(ToTableResponse).ToArray());
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    [HttpPost("tables")]
    [Authorize(Policy = ManagementPolicies.TableEdit)]
    [ProducesResponseType(typeof(ManagementTableResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTableAsync(
        [FromBody] ManagementCreateTableRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var table = await tableService.CreateTableAsync(
                userId,
                tenantId,
                branchId,
                request?.Label ?? string.Empty,
                cancellationToken);
            return Created($"/api/v1/management/tables/{table.Id}", ToTableResponse(table));
        }
        catch (CustomerExperienceException exception)
        {
            return TableProblem(exception);
        }
        catch (EntitlementException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    [HttpPatch("tables/{tableId:guid}")]
    [Authorize(Policy = ManagementPolicies.TableEdit)]
    [ProducesResponseType(typeof(ManagementTableResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTableAsync(
        Guid tableId,
        [FromBody] ManagementUpdateTableRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var table = await tableService.UpdateTableAsync(
                userId,
                tenantId,
                branchId,
                tableId,
                request?.Label,
                request?.IsActive,
                cancellationToken);
            return Ok(ToTableResponse(table));
        }
        catch (CustomerExperienceException exception)
        {
            return TableProblem(exception);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    [HttpGet("tables/{tableId:guid}/qr-codes")]
    [Authorize(Policy = ManagementPolicies.TableView)]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementQrCodeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListQrCodesAsync(Guid tableId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var codes = await tableService.ListQrCodesAsync(userId, tenantId, branchId, tableId, cancellationToken);
            return Ok(codes.Select(ToQrResponse).ToArray());
        }
        catch (CustomerExperienceException exception)
        {
            return TableProblem(exception);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    [HttpPost("tables/{tableId:guid}/qr-codes")]
    [Authorize(Policy = ManagementPolicies.TableEdit)]
    [ProducesResponseType(typeof(ManagementGeneratedQrResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateQrAsync(Guid tableId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var generated = await tableService.GenerateQrAsync(userId, tenantId, branchId, tableId, cancellationToken);
            return Created(
                $"/api/v1/management/qr-codes/{generated.Id}/print",
                new ManagementGeneratedQrResponse(
                    generated.Id,
                    generated.TableId,
                    generated.TableLabel,
                    generated.Status,
                    generated.Token,
                    generated.EntryUrl,
                    generated.SvgMarkup,
                    generated.CreatedAtUtc));
        }
        catch (CustomerExperienceException exception)
        {
            return TableProblem(exception);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    [HttpPost("qr-codes/{qrCodeId:guid}/activate")]
    [Authorize(Policy = ManagementPolicies.TableEdit)]
    [ProducesResponseType(typeof(ManagementQrCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> ActivateQrAsync(Guid qrCodeId, CancellationToken cancellationToken) =>
        ChangeQrStatusAsync(qrCodeId, QrCodeStatus.Active, cancellationToken);

    [HttpPost("qr-codes/{qrCodeId:guid}/deactivate")]
    [Authorize(Policy = ManagementPolicies.TableEdit)]
    [ProducesResponseType(typeof(ManagementQrCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> DeactivateQrAsync(Guid qrCodeId, CancellationToken cancellationToken) =>
        ChangeQrStatusAsync(qrCodeId, QrCodeStatus.Inactive, cancellationToken);

    [HttpPost("qr-codes/{qrCodeId:guid}/revoke")]
    [Authorize(Policy = ManagementPolicies.TableEdit)]
    [ProducesResponseType(typeof(ManagementQrCodeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> RevokeQrAsync(Guid qrCodeId, CancellationToken cancellationToken) =>
        ChangeQrStatusAsync(qrCodeId, QrCodeStatus.Revoked, cancellationToken);

    [HttpGet("qr-codes/{qrCodeId:guid}/print")]
    [Authorize(Policy = ManagementPolicies.TableView)]
    [ProducesResponseType(typeof(ManagementQrPrintResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PrintQrAsync(Guid qrCodeId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var print = await tableService.GetPrintPayloadAsync(
                userId,
                tenantId,
                branchId,
                qrCodeId,
                cancellationToken);
            return Ok(new ManagementQrPrintResponse(
                print.Id,
                print.TableId,
                print.TableLabel,
                print.Status,
                print.EntryUrl,
                print.SvgMarkup));
        }
        catch (CustomerExperienceException exception)
        {
            return TableProblem(exception);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    [HttpPost("tables/{tableId:guid}/release")]
    [Authorize(Policy = ManagementPolicies.OrderModify)]
    [ProducesResponseType(typeof(ManagementTableResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReleaseTableAsync(Guid tableId, CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var table = await tableService.ReleaseTableAsync(userId, tenantId, branchId, tableId, cancellationToken);
            return Ok(ToTableResponse(table));
        }
        catch (CustomerExperienceException exception)
        {
            return TableProblem(exception);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    private async Task<IActionResult> ChangeQrStatusAsync(
        Guid qrCodeId,
        QrCodeStatus nextStatus,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out var branchId))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        try
        {
            var result = await tableService.ChangeQrStatusAsync(
                userId,
                tenantId,
                branchId,
                qrCodeId,
                nextStatus,
                cancellationToken);
            return Ok(ToQrResponse(result));
        }
        catch (CustomerExperienceException exception)
        {
            return TableProblem(exception);
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    private static ManagementTableResponse ToTableResponse(ManagementTableResult table) =>
        new(table.Id, table.Label, table.IsActive, table.ActiveQrCount, table.OperationalStatus);

    private static ManagementQrCodeResponse ToQrResponse(ManagementQrCodeResult qr) =>
        new(qr.Id, qr.TableId, qr.Status, qr.CreatedAtUtc, qr.RevokedAtUtc);

    private static ObjectResult TableProblem(CustomerExperienceException exception)
    {
        var status = exception.Code switch
        {
            "TABLE_NOT_FOUND" or "QR_NOT_FOUND" => StatusCodes.Status404NotFound,
            "TABLE_LABEL_CONFLICT" or "INVALID_QR_TRANSITION" or "QR_REVOKED" or "QR_TOKEN_UNAVAILABLE" =>
                StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };
        return ApiProblem.Create(status, exception.Code, exception.Message);
    }
}
