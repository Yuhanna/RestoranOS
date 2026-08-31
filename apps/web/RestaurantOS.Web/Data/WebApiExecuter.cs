using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace RestaurantOS.Web.Data;

public sealed class ApiCookieJarStore
{
    private readonly ConcurrentDictionary<string, CookieContainer> _jars = new(StringComparer.Ordinal);

    public CookieContainer GetOrCreate(string sessionId) =>
        _jars.GetOrAdd(sessionId, _ => new CookieContainer());

    public void Remove(string sessionId) => _jars.TryRemove(sessionId, out _);
}

public class WebApiExecuter(
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment hostEnvironment,
    IOptions<ApiOptions> apiOptions,
    ApiCookieJarStore cookieJarStore,
    ApiSessionScope sessionScope) : IWebApiExecuter
{
    private readonly ApiSessionScope _sessionScope = sessionScope;
    private readonly ApiCookieJarStore _cookieJarStore = cookieJarStore;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApiOptions _apiOptions = apiOptions.Value;

    public bool IsAuthenticated => CurrentToken is not null;

    public string? CurrentRestaurantName =>
        httpContextAccessor.HttpContext?.Session.GetString(_sessionScope.RestaurantNameSessionKey);

    public string? CurrentBranchName =>
        httpContextAccessor.HttpContext?.Session.GetString(_sessionScope.BranchNameSessionKey);

    public JwtToken? CurrentToken
    {
        get
        {
            var session = httpContextAccessor.HttpContext?.Session;
            if (session is null)
            {
                return null;
            }

            var json = session.GetString(_sessionScope.AccessTokenSessionKey);
            return string.IsNullOrEmpty(json)
                ? null
                : JsonSerializer.Deserialize<JwtToken>(json, JsonOptions);
        }
    }

    public Task<T?> InvokeGetAsync<T>(string relativePath, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Get, relativePath, null, allowRetry: true, cancellationToken);

    public Task<T?> InvokePostAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Post, relativePath, body, allowRetry: true, cancellationToken);

    public Task<T?> InvokePutAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Put, relativePath, body, allowRetry: true, cancellationToken);

    public Task<T?> InvokePatchAsync<T>(string relativePath, object? body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(HttpMethod.Patch, relativePath, body, allowRetry: true, cancellationToken);

    public async Task InvokeDeleteAsync(string relativePath, CancellationToken cancellationToken = default) =>
        await SendAsync<object>(HttpMethod.Delete, relativePath, null, allowRetry: true, cancellationToken);

    public async Task<T?> InvokePostMultipartAsync<T>(
        string relativePath,
        IFormFile file,
        string? imageAlt,
        CancellationToken cancellationToken = default)
    {
        await using var stream = file.OpenReadStream();
        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(
            string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType);
        content.Add(streamContent, "file", file.FileName);
        if (!string.IsNullOrWhiteSpace(imageAlt))
        {
            content.Add(new StringContent(imageAlt), "imageAlt");
        }

        using var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, relativePath.TrimStart('/'))
        {
            Content = content,
        };
        var token = CurrentToken;
        if (token is not null && !string.IsNullOrWhiteSpace(token.AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        return await ReadOrThrowAsync<T>(response, cancellationToken);
    }

    public async Task LoginAsync(
        string email,
        string password,
        Guid? tenantId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        await LoginCoreAsync(
            "/api/v1/management/auth/login",
            new { email, password, tenantId, branchId },
            cancellationToken);
        await RefreshWorkspaceIdentityAsync(cancellationToken);
    }

    protected async Task LoginCoreAsync(
        string relativePath,
        object body,
        CancellationToken cancellationToken)
    {
        EnsureSession();
        ClearAccessToken();
        _cookieJarStore.Remove(GetSessionId());

        var token = await SendAsync<JwtToken>(
            HttpMethod.Post,
            relativePath,
            body,
            allowRetry: false,
            cancellationToken,
            attachBearer: false);

        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new WebApiException(StatusCodes.Status401Unauthorized, new ErrorResponse
            {
                Code = "INVALID_CREDENTIALS",
                Detail = "Login did not return an access token.",
            });
        }

        StoreAccessToken(token);
    }

    public async Task RegisterAsync(
        string email,
        string password,
        string restaurantName,
        string branchName,
        CancellationToken cancellationToken = default)
    {
        EnsureSession();
        ClearAccessToken();
        _cookieJarStore.Remove(GetSessionId());

        var token = await SendAsync<JwtToken>(
            HttpMethod.Post,
            "/api/v1/management/auth/register",
            new
            {
                email,
                password,
                restaurantName,
                branchName,
            },
            allowRetry: false,
            cancellationToken,
            attachBearer: false);

        if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new WebApiException(StatusCodes.Status400BadRequest, new ErrorResponse
            {
                Code = "REGISTER_FAILED",
                Detail = "Registration did not return an access token.",
            });
        }

        StoreAccessToken(token);
        await RefreshWorkspaceIdentityAsync(cancellationToken);
    }

    public virtual async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await SendAsync<object>(
                HttpMethod.Post,
                LogoutRelativePath,
                null,
                allowRetry: false,
                cancellationToken,
                attachBearer: false);
        }
        catch (WebApiException)
        {
            // Best-effort logout; clear local session regardless.
        }
        finally
        {
            ClearAccessToken();
            _cookieJarStore.Remove(GetSessionId());
        }
    }

    protected virtual string LogoutRelativePath => "/api/v1/management/auth/logout";

    protected async Task<T?> SendAsync<T>(
        HttpMethod method,
        string relativePath,
        object? body,
        bool allowRetry,
        CancellationToken cancellationToken,
        bool attachBearer = true)
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
        using var client = CreateClient();
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

        return await client.SendAsync(request, cancellationToken);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await SendAsync<JwtToken>(
                HttpMethod.Post,
                "/api/v1/management/auth/refresh",
                null,
                allowRetry: false,
                cancellationToken,
                attachBearer: false);
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
                        Code = root.TryGetProperty("code", out var code)
                            ? code.GetString()
                            : root.TryGetProperty("title", out var codeTitle) ? codeTitle.GetString() : null,
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

    private HttpClient CreateClient()
    {
        var sessionId = GetSessionId();
        var handler = new HttpClientHandler
        {
            CookieContainer = _cookieJarStore.GetOrCreate(sessionId),
            UseCookies = true,
        };

        if (hostEnvironment.IsDevelopment())
        {
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        return new HttpClient(handler)
        {
            BaseAddress = new Uri(_apiOptions.BaseUrl.TrimEnd('/') + "/"),
        };
    }

    protected void StoreAccessToken(JwtToken token)
    {
        EnsureSession().SetString(_sessionScope.AccessTokenSessionKey, JsonSerializer.Serialize(token, JsonOptions));
    }

    protected void ClearAccessToken()
    {
        var session = EnsureSession();
        session.Remove(_sessionScope.AccessTokenSessionKey);
        session.Remove(_sessionScope.RestaurantNameSessionKey);
        session.Remove(_sessionScope.BranchNameSessionKey);
    }

    private async Task RefreshWorkspaceIdentityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await InvokeGetAsync<WorkspaceIdentity>(
                "/api/v1/management/workspace",
                cancellationToken);
            var session = EnsureSession();
            if (workspace is null)
            {
                session.Remove(_sessionScope.RestaurantNameSessionKey);
                session.Remove(_sessionScope.BranchNameSessionKey);
                return;
            }

            session.SetString(_sessionScope.RestaurantNameSessionKey, workspace.RestaurantName);
            session.SetString(_sessionScope.BranchNameSessionKey, workspace.BranchName);
        }
        catch (WebApiException)
        {
            // Workspace label is UX-only; auth still works without it.
        }
    }

    private sealed class WorkspaceIdentity
    {
        public string RestaurantName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
    }

    protected ISession EnsureSession()
    {
        var session = httpContextAccessor.HttpContext?.Session
            ?? throw new InvalidOperationException("HTTP session is required for API access tokens.");
        return session;
    }

    protected string GetSessionId()
    {
        var session = EnsureSession();
        var id = session.GetString(_sessionScope.ApiJarIdSessionKey);
        if (string.IsNullOrEmpty(id))
        {
            id = Guid.NewGuid().ToString("N");
            session.SetString(_sessionScope.ApiJarIdSessionKey, id);
        }

        return id;
    }
}
