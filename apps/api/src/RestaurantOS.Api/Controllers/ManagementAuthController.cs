using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/auth")]
public sealed class ManagementAuthController(IManagementAuthService authService) : ControllerBase
{
    private const string RefreshCookiePath = "/api/v1/management/auth";

    [HttpPost("register")]
    [EnableRateLimiting("management-login")]
    [ProducesResponseType(typeof(ManagementAccessTokenResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RegisterAsync(
        [FromBody] ManagementRegisterRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrEmpty(request.Password)
            || string.IsNullOrWhiteSpace(request.RestaurantName))
        {
            return ApiProblem.Create(
                StatusCodes.Status400BadRequest,
                "VALIDATION_ERROR",
                "Email, password, and restaurant name are required.");
        }

        try
        {
            var result = await authService.RegisterAsync(
                request.Email,
                request.Password,
                request.RestaurantName,
                request.BranchName ?? string.Empty,
                cancellationToken);
            SetRefreshCookie(result);
            return Created("/api/v1/management/auth/login", ToAccessTokenResponse(result));
        }
        catch (ManagementAuthException exception)
        {
            var status = exception.Code switch
            {
                "EMAIL_IN_USE" => StatusCodes.Status409Conflict,
                "VALIDATION_ERROR" => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest,
            };
            return ApiProblem.Create(status, exception.Code, exception.Message);
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("management-login")]
    [ProducesResponseType(typeof(ManagementAccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LoginAsync(
        [FromBody] ManagementLoginRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrEmpty(request.Password))
        {
            return ApiProblem.Create(
                StatusCodes.Status401Unauthorized,
                "INVALID_CREDENTIALS",
                "Email, password, or management scope is invalid.");
        }

        try
        {
            var result = await authService.LoginAsync(
                request.Email,
                request.Password,
                request.TenantId,
                request.BranchId,
                cancellationToken);
            SetRefreshCookie(result);
            return Ok(ToAccessTokenResponse(result));
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, exception.Code, exception.Message);
        }
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("management-refresh")]
    [ProducesResponseType(typeof(ManagementAccessTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await authService.RefreshAsync(
                AuthRefreshCookie.Read(Request, platform: false) ?? string.Empty,
                AuthRealms.Management,
                cancellationToken);
            SetRefreshCookie(result);
            return Ok(ToAccessTokenResponse(result));
        }
        catch (ManagementAuthException exception)
        {
            DeleteRefreshCookie();
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, exception.Code, exception.Message);
        }
    }

    [HttpPost("logout")]
    [EnableRateLimiting("management-refresh")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        await authService.RevokeAsync(
            AuthRefreshCookie.Read(Request, platform: false) ?? string.Empty,
            cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpGet("memberships")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<ManagementMembershipScopeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListMembershipsAsync(CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out _, out _))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        var memberships = await authService.ListMembershipsAsync(userId, cancellationToken);
        return Ok(memberships.Select(item => new ManagementMembershipScopeResponse(
            item.MembershipId,
            item.TenantId,
            item.RestaurantId,
            item.RestaurantName,
            item.BranchId,
            item.BranchName,
            item.RoleName,
            item.CanManageBranches)).ToArray());
    }

    [HttpPost("switch-branch")]
    [Authorize]
    [ProducesResponseType(typeof(ManagementAccessTokenResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SwitchBranchAsync(
        [FromBody] ManagementSwitchBranchRequest? request,
        CancellationToken cancellationToken)
    {
        if (!PermissionAuthorizationHandler.TryGetScope(User, out var userId, out var tenantId, out _))
        {
            return ApiProblem.Create(StatusCodes.Status401Unauthorized, "INVALID_ACCESS_TOKEN", "Access token is invalid.");
        }

        if (request is null || request.BranchId == Guid.Empty)
        {
            return ApiProblem.Create(StatusCodes.Status400BadRequest, "VALIDATION_ERROR", "BranchId is required.");
        }

        try
        {
            var result = await authService.SwitchBranchAsync(
                userId,
                tenantId,
                request.BranchId,
                cancellationToken);
            SetRefreshCookie(result);
            return Ok(ToAccessTokenResponse(result));
        }
        catch (ManagementAuthException exception)
        {
            return ApiProblem.Create(StatusCodes.Status403Forbidden, exception.Code, exception.Message);
        }
    }

    private static ManagementAccessTokenResponse ToAccessTokenResponse(ManagementTokenResult result) =>
        new(
            result.AccessToken,
            result.AccessTokenExpiresAtUtc,
            result.UserId,
            result.TenantId,
            result.BranchId);

    private void SetRefreshCookie(ManagementTokenResult result) =>
        AuthRefreshCookie.Append(
            Response,
            Request,
            platform: false,
            RefreshCookiePath,
            result.RefreshToken,
            result.RefreshTokenExpiresAtUtc);

    private void DeleteRefreshCookie() =>
        AuthRefreshCookie.Delete(Response, Request, platform: false, RefreshCookiePath);
}
