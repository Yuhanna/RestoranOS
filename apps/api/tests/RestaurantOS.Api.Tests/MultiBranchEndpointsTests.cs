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

public sealed class MultiBranchEndpointsTests
{
    [Fact]
    public async Task ProTrialAllowsThreeBranchesAndBlocksFourth()
    {
        using var factory = new MultiBranchApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "hq-owner@branch.test",
                "Secure-Pass!42xx",
                "HQ Cafe",
                "Merkez"));
        var token = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.NotNull(token);

        var workspaceResponse = await client.SendAsync(Authorized(token.AccessToken, HttpMethod.Get, "/api/v1/management/workspace"));
        var workspace = await workspaceResponse.Content.ReadFromJsonAsync<ManagementWorkspaceResponse>();
        Assert.True(workspace?.Entitlements?.CanUseMultiBranch);
        Assert.True(workspace?.Entitlements?.IsTrial);
        Assert.Equal(3, workspace?.Entitlements?.MaxBranches);

        foreach (var name in new[] { "Kadıköy", "Beşiktaş" })
        {
            var created = await client.SendAsync(Authorized(
                token.AccessToken,
                HttpMethod.Post,
                "/api/v1/management/branches",
                new ManagementCreateBranchRequest(name)));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var fourth = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/branches",
            new ManagementCreateBranchRequest("Üsküdar")));
        var problem = await fourth.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        Assert.Equal(HttpStatusCode.Forbidden, fourth.StatusCode);
        Assert.Equal("ENTITLEMENT_BRANCH_LIMIT", problem?.Code);
    }

    [Fact]
    public async Task PaidProRequiresAddonConfirmationForThirdBranch()
    {
        using var factory = new MultiBranchApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "paid-owner@branch.test",
                "Secure-Pass!42xx",
                "Paid Cafe",
                "Merkez"));
        var token = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.NotNull(token);

        var checkout = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/subscription/checkout",
            new ManagementSubscriptionCheckoutRequest("Pro")));
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);

        var second = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/branches",
            new ManagementCreateBranchRequest("İkinci")));
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var thirdBlocked = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/branches",
            new ManagementCreateBranchRequest("Üçüncü")));
        var blockedProblem = await thirdBlocked.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        Assert.Equal(HttpStatusCode.PaymentRequired, thirdBlocked.StatusCode);
        Assert.Equal("BRANCH_ADDON_REQUIRED", blockedProblem?.Code);

        var third = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/branches",
            new ManagementCreateBranchRequest("Üçüncü", ConfirmAddonPurchase: true)));
        Assert.Equal(HttpStatusCode.Created, third.StatusCode);

        var workspaceResponse = await client.SendAsync(Authorized(token.AccessToken, HttpMethod.Get, "/api/v1/management/workspace"));
        var workspace = await workspaceResponse.Content.ReadFromJsonAsync<ManagementWorkspaceResponse>();
        Assert.Equal(1, workspace?.Entitlements?.PurchasedBranchAddonCount);
        Assert.Equal(3, workspace?.Entitlements?.MaxBranches);
    }

    [Fact]
    public async Task FreePlanBlocksSecondBranchAndEnterpriseNeedsQuote()
    {
        using var factory = new MultiBranchApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "free-owner@branch.test",
                "Secure-Pass!42xx",
                "Free Cafe",
                "Ana"));
        var token = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.NotNull(token);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var subscription = await db.TenantSubscriptions.SingleAsync(x => x.TenantId == token.TenantId);
            subscription.DowngradeToFree(DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var created = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/branches",
            new ManagementCreateBranchRequest("İkinci")));
        var problem = await created.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        Assert.Equal(HttpStatusCode.Forbidden, created.StatusCode);
        Assert.Equal("ENTITLEMENT_BRANCH_LIMIT", problem?.Code);

        var enterprise = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/subscription/checkout",
            new ManagementSubscriptionCheckoutRequest("Enterprise")));
        var enterpriseProblem = await enterprise.Content.ReadFromJsonAsync<ProblemDetailsContract>();
        Assert.Equal(HttpStatusCode.PaymentRequired, enterprise.StatusCode);
        Assert.Equal("ENTERPRISE_QUOTE_REQUIRED", enterpriseProblem?.Code);

        var quote = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/subscription/enterprise-quote",
            new ManagementEnterpriseQuoteRequest(
                "Ayşe Yılmaz",
                "free-owner@branch.test",
                "05551112233",
                8,
                "Zincir planı")));
        Assert.Equal(HttpStatusCode.Created, quote.StatusCode);
    }

    [Fact]
    public async Task TrialExpiryFreezesExtraBranches()
    {
        using var factory = new MultiBranchApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "expire-owner@branch.test",
                "Secure-Pass!42xx",
                "Expire Cafe",
                "Merkez"));
        var token = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.NotNull(token);

        await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/branches",
            new ManagementCreateBranchRequest("İkinci")));
        await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/branches",
            new ManagementCreateBranchRequest("Üçüncü")));

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
        Assert.Equal("Free", workspace?.Entitlements?.PlanCode);
        Assert.Equal(2, workspace?.Entitlements?.FrozenBranchCount);
        Assert.Equal(1, workspace?.Entitlements?.ActiveBranchCount);
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

    private sealed class MultiBranchApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"branches-{Guid.NewGuid():N}";

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
