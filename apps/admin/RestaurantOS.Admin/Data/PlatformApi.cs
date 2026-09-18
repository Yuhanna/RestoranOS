using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace RestaurantOS.Admin.Data;

public interface IPlatformApi
{
    bool IsAuthenticated { get; }
    SessionToken? CurrentToken { get; }
    Task LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<T?> GetAsync<T>(string relativePath, CancellationToken cancellationToken = default);
    Task<T?> PostAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default);
    Task<T?> PatchAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default);
}

public sealed class PlatformApi(
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment hostEnvironment,
    IOptions<ApiOptions> apiOptions,
    ApiCookieJarStore cookieJarStore) : IPlatformApi, IDisposable
{
    private const string AccessTokenKey = "RestaurantOS.Admin.AccessToken";
    private const string JarIdKey = "RestaurantOS.Admin.ApiJarId";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromMinutes(1);
    private readonly ApiOptions _apiOptions = apiOptions.Value;
    private HttpClient? _client;

    public bool IsAuthenticated => CurrentToken is not null;

    public SessionToken? CurrentToken
    {
        get
        {
            var json = httpContextAccessor.HttpContext?.Session.GetString(AccessTokenKey);
            return string.IsNullOrEmpty(json)
                ? null
                : JsonSerializer.Deserialize<SessionToken>(json, JsonOptions);
        }
    }

    public async Task LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        EnsureSession();
        ClearAccessToken();
        cookieJarStore.Remove(GetSessionId());

        var token = await SendAsync<SessionToken>(
            HttpMethod.Post,
            "/api/v1/platform/auth/login",
            new { email, password },
            allowRetry: false,
            attachBearer: false,
            cancellationToken);

        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new WebApiException(StatusCodes.Status401Unauthorized, new ErrorResponse
            {
                Code = "INVALID_CREDENTIALS",
                Detail = "Giriş erişim jetonu döndürmedi.",
            });
        }

        StoreAccessToken(token);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync<object>(
                HttpMethod.Post,
                "/api/v1/platform/auth/logout",
                null,
                allowRetry: false,
                attachBearer: false,
                cancellationToken);
        }
        catch (WebApiException)
        {
            // Best-effort logout.
        }
        finally
        {
            ClearAccessToken();
            cookieJarStore.Remove(GetSessionId());
        }
    }

    public Task<T?> GetAsync<T>(string relativePath, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, relativePath, null, allowRetry: true, attachBearer: true, cancellationToken);

    public Task<T?> PostAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, relativePath, body, allowRetry: true, attachBearer: true, cancellationToken);

    public Task<T?> PatchAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Patch, relativePath, body, allowRetry: true, attachBearer: true, cancellationToken);

    private async Task<T?> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? body,
        bool allowRetry,
        bool attachBearer,
        CancellationToken cancellationToken)
    {
        using var response = await SendCoreAsync(method, relativePath, body, attachBearer, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && allowRetry && attachBearer)
        {
            if (await TryRefreshAsync(cancellationToken))
            {
                using var retry = await SendCoreAsync(method, relativePath, body, attachBearer: true, cancellationToken);
                return await ReadOrThrowAsync<T>(retry, cancellationToken);
            }
        }

        return await ReadOrThrowAsync<T>(response, cancellationToken);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(
        HttpMethod method,
        string relativePath,
        object? body,
        bool attachBearer,
        CancellationToken cancellationToken)
    {
        if (attachBearer)
        {
            await EnsureFreshTokenAsync(cancellationToken);
        }

        using var request = new HttpRequestMessage(method, relativePath.TrimStart('/'));
        if (attachBearer)
        {
            var token = CurrentToken;
            if (token is not null && !string.IsNullOrWhiteSpace(token.AccessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            }
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        return await GetClient().SendAsync(request, cancellationToken);
    }

    private async Task EnsureFreshTokenAsync(CancellationToken cancellationToken)
    {
        var token = CurrentToken;
        if (token is null || token.ExpiresAtUtc > DateTimeOffset.UtcNow.Add(TokenRefreshSkew))
        {
            return;
        }

        await TryRefreshAsync(cancellationToken);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await SendAsync<SessionToken>(
                HttpMethod.Post,
                "/api/v1/platform/auth/refresh",
                null,
                allowRetry: false,
                attachBearer: false,
                cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                ClearAccessToken();
                return false;
            }

            StoreAccessToken(token);
            return true;
        }
        catch (WebApiException)
        {
            ClearAccessToken();
            return false;
        }
    }

    private static async Task<T?> ReadOrThrowAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return default;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            ErrorResponse? error = null;
            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var document = JsonDocument.Parse(body);
                    var root = document.RootElement;
                    error = new ErrorResponse
                    {
                        Title = root.TryGetProperty("title", out var title) ? title.GetString() : null,
                        Detail = root.TryGetProperty("detail", out var detail) ? detail.GetString() : null,
                        Status = root.TryGetProperty("status", out var status) ? status.GetInt32() : (int)response.StatusCode,
                        Code = root.TryGetProperty("code", out var code) ? code.GetString() : null,
                    };
                }
                catch (JsonException)
                {
                    // Keep raw body.
                }
            }

            throw new WebApiException((int)response.StatusCode, error, body);
        }

        if (typeof(T) == typeof(object) || string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions);
    }

    private HttpClient GetClient()
    {
        if (_client is not null)
        {
            return _client;
        }

        var handler = new HttpClientHandler
        {
            CookieContainer = cookieJarStore.GetOrCreate(GetSessionId()),
            UseCookies = true,
        };

        if (hostEnvironment.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        _client = new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(_apiOptions.BaseUrl.TrimEnd('/') + "/"),
        };
        return _client;
    }

    public void Dispose()
    {
        _client?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void StoreAccessToken(SessionToken token) =>
        EnsureSession().SetString(AccessTokenKey, JsonSerializer.Serialize(token, JsonOptions));

    private void ClearAccessToken() => EnsureSession().Remove(AccessTokenKey);

    private ISession EnsureSession() =>
        httpContextAccessor.HttpContext?.Session
        ?? throw new InvalidOperationException("HTTP oturumu gerekli.");

    private string GetSessionId()
    {
        var session = EnsureSession();
        var id = session.GetString(JarIdKey);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N");
            session.SetString(JarIdKey, id);
        }

        return id;
    }
}
