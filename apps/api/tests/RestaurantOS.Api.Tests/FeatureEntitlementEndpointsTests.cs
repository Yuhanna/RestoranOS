using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class FeatureEntitlementEndpointsTests
{
    [Fact]
    public async Task NewRegistrationStartsProTrialWithUnlimitedTables()
    {
        using var factory = new EntitlementApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "trial-owner@entitlement.test",
                "Secure-Pass!42xx",
                "Trial Cafe",
                "Ana"));
        var token = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.NotNull(token);

        var workspaceResponse = await client.SendAsync(Authorized(token.AccessToken, HttpMethod.Get, "/api/v1/management/workspace"));
        var workspace = await workspaceResponse.Content.ReadFromJsonAsync<ManagementWorkspaceResponse>();
        Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);
        Assert.NotNull(workspace?.Entitlements);
        Assert.Equal("Pro", workspace.Entitlements.PlanCode);
        Assert.Equal("Pro (deneme)", workspace.Entitlements.PlanDisplayName);
        Assert.True(workspace.Entitlements.IsTrial);
        Assert.NotNull(workspace.Entitlements.TrialEndsAtUtc);
        Assert.Null(workspace.Entitlements.MaxTablesPerBranch);
        Assert.True(workspace.Entitlements.CanUseLiveOrderPanel);

        for (var i = 1; i <= 9; i++)
        {
            var created = await client.SendAsync(Authorized(
                token.AccessToken,
                HttpMethod.Post,
                "/api/v1/management/tables",
                new ManagementCreateTableRequest($"Masa {i}")));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }
    }

    [Fact]
    public async Task ExpiredProTrialDowngradesToFreeAndEnforcesTableLimit()
    {
        using var factory = new EntitlementApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "expired-owner@entitlement.test",
                "Secure-Pass!42xx",
                "Expired Cafe",
                "Ana"));
        var token = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.NotNull(token);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var subscription = await db.TenantSubscriptions.SingleAsync(x => x.TenantId == token.TenantId);
            subscription.ChangePlan(
                SubscriptionPlanCodes.Pro,
                DateTimeOffset.UtcNow.AddDays(-40),
                DateTimeOffset.UtcNow.AddDays(-1));
            await db.SaveChangesAsync();
        }

        var workspaceResponse = await client.SendAsync(Authorized(token.AccessToken, HttpMethod.Get, "/api/v1/management/workspace"));
        var workspace = await workspaceResponse.Content.ReadFromJsonAsync<ManagementWorkspaceResponse>();
        Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);
        Assert.NotNull(workspace?.Entitlements);
        Assert.Equal("Free", workspace.Entitlements.PlanCode);
        Assert.False(workspace.Entitlements.IsTrial);
        Assert.Equal(8, workspace.Entitlements.MaxTablesPerBranch);

        for (var i = 1; i <= 8; i++)
        {
            var created = await client.SendAsync(Authorized(
                token.AccessToken,
                HttpMethod.Post,
                "/api/v1/management/tables",
                new ManagementCreateTableRequest($"Masa {i}")));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var ninth = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/tables",
            new ManagementCreateTableRequest("Masa 9")));
        var ninthProblem = await ninth.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        Assert.Equal(HttpStatusCode.Forbidden, ninth.StatusCode);
        Assert.Equal("ENTITLEMENT_TABLE_LIMIT", ninthProblem?.Code);
    }

    [Fact]
    public async Task FreePlanAllowsProductImagesWhenForcedOntoFree()
    {
        using var factory = new EntitlementApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "free-owner@entitlement.test",
                "Secure-Pass!42xx",
                "Limit Cafe",
                "Ana"));
        var token = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.NotNull(token);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var subscription = await db.TenantSubscriptions.SingleAsync(x => x.TenantId == token.TenantId);
            subscription.DowngradeToFree(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var workspaceResponse = await client.SendAsync(Authorized(token.AccessToken, HttpMethod.Get, "/api/v1/management/workspace"));
        var workspace = await workspaceResponse.Content.ReadFromJsonAsync<ManagementWorkspaceResponse>();
        Assert.Equal(HttpStatusCode.OK, workspaceResponse.StatusCode);
        Assert.NotNull(workspace?.Entitlements);
        Assert.Equal("Free", workspace.Entitlements.PlanCode);
        Assert.True(workspace.Entitlements.CanUseProductImages);

        var menu = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/menus",
            new ManagementCreateMenuRequest("Akşam")));
        var menuPayload = await menu.Content.ReadFromJsonAsync<ManagementMenuSummaryResponse>();
        Assert.Equal(HttpStatusCode.Created, menu.StatusCode);
        Assert.NotNull(menuPayload);

        var category = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menuPayload.Id}/categories",
            new ManagementCreateCategoryRequest("Ana yemek", 0)));
        var categoryPayload = await category.Content.ReadFromJsonAsync<ManagementMenuCategoryResponse>();
        Assert.Equal(HttpStatusCode.Created, category.StatusCode);
        Assert.NotNull(categoryPayload);

        var withImage = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menuPayload.Id}/items",
            new ManagementCreateMenuItemRequest(
                categoryPayload.Id,
                "Levrek",
                "Izgara",
                35000,
                true,
                0,
                "https://cdn.example/levrek.jpg",
                "Levrek")));
        Assert.Equal(HttpStatusCode.Created, withImage.StatusCode);
    }

    private static HttpRequestMessage Authorized(
        string accessToken,
        HttpMethod method,
        string path,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private sealed record ProblemDetailsContract(string? Title, string? Detail, string? Code);

    private sealed class EntitlementApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"entitlement-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ManagementAuth:SigningKey",
                "test-only-signing-key-32-bytes-minimum-value");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<RestaurantOsDbContext>>();
                services.AddDbContext<RestaurantOsDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
            });
        }
    }
}
