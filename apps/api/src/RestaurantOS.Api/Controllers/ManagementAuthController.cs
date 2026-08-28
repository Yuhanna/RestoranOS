using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api.Controllers;

[ApiController]
[Route("api/v1/management/auth")]
public sealed class ManagementAuthController(IManagementAuthService authService) : ControllerBase
{
    private const string RefreshCookieName = "__Secure-restaurantos-refresh";

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
                Request.Cookies[RefreshCookieName] ?? string.Empty,
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
            Request.Cookies[RefreshCookieName] ?? string.Empty,
            cancellationToken);
        DeleteRefreshCookie();
        return NoContent();
    }

    private static ManagementAccessTokenResponse ToAccessTokenResponse(ManagementTokenResult result) =>
        new(
            result.AccessToken,
            result.AccessTokenExpiresAtUtc,
            result.UserId,
            result.TenantId,
            result.BranchId);

    private void SetRefreshCookie(ManagementTokenResult result) =>
        Response.Cookies.Append(
            RefreshCookieName,
            result.RefreshToken,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/management/auth",
                Expires = result.RefreshTokenExpiresAtUtc,
                IsEssential = true,
            });

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(
            RefreshCookieName,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/api/v1/management/auth",
            });
}
