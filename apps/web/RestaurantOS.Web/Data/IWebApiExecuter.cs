using Microsoft.AspNetCore.Http;
using RestaurantOS.Web.Models;

namespace RestaurantOS.Web.Data;

public interface IWebApiExecuter
{
    Task<WorkspaceViewModel?> GetWorkspaceAsync(CancellationToken cancellationToken = default);

    void InvalidateWorkspaceCache();

    Task<T?> InvokeGetAsync<T>(string relativePath, CancellationToken cancellationToken = default);

    Task<T?> InvokePostAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default);

    Task<T?> InvokePutAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default);

    Task<T?> InvokePatchAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default);

    Task InvokeDeleteAsync(string relativePath, CancellationToken cancellationToken = default);

    Task<T?> InvokePostMultipartAsync<T>(
        string relativePath,
        IFormFile file,
        string? imageAlt,
        CancellationToken cancellationToken = default);

    Task LoginAsync(
        string email,
        string password,
        Guid? tenantId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default);

    Task RegisterAsync(
        string email,
        string password,
        string restaurantName,
        string branchName,
        CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);

    bool IsAuthenticated { get; }

    JwtToken? CurrentToken { get; }

    string? CurrentRestaurantName { get; }

    string? CurrentBranchName { get; }
}
