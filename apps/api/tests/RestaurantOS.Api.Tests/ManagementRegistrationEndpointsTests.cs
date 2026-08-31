using System.Net;
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

public sealed class ManagementRegistrationEndpointsTests
{
    [Fact]
    public async Task RegisterCreatesTenantRestaurantBranchOwnerAndAllowsScopedLogin()
    {
        using var factory = new RegistrationApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "owner@new-restaurant.test",
                "Secure-Pass!42xx",
                "Marea Demo",
                "Nişantaşı"));
        var created = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();

        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.TenantId);
        Assert.NotEqual(Guid.Empty, created.BranchId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            Assert.Equal(1, await db.Tenants.CountAsync());
            Assert.Equal(1, await db.Restaurants.CountAsync());
            Assert.Equal(1, await db.Branches.CountAsync());
            Assert.Equal(1, await db.ManagementUsers.CountAsync());
            Assert.Equal(1, await db.ManagementMemberships.CountAsync(x => x.IsActive));
            Assert.Contains(await db.ManagementAuditLogs.ToListAsync(), x => x.Action == "Registered" && x.Succeeded);
            var subscription = await db.TenantSubscriptions.SingleAsync(x => x.TenantId == created.TenantId);
            Assert.Equal("Pro", subscription.PlanCode);
            Assert.NotNull(subscription.ExpiresAtUtc);
            Assert.True(subscription.ExpiresAtUtc > DateTimeOffset.UtcNow.AddDays(25));
            Assert.True(subscription.IsTrialActive(DateTimeOffset.UtcNow));
        }

        var duplicate = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "owner@new-restaurant.test",
                "Secure-Pass!42xx",
                "Other",
                "Branch"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var login = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@new-restaurant.test", "Secure-Pass!42xx"));
        var token = await login.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(created.TenantId, token?.TenantId);
        Assert.Equal(created.BranchId, token?.BranchId);
    }

    [Fact]
    public async Task RegisterRejectsWeakPassword()
    {
        using var factory = new RegistrationApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest("weak@test.local", "short", "Cafe", "Main"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class RegistrationApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"reg-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
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
