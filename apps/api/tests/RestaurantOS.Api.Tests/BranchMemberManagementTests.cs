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
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class BranchMemberManagementTests
{
    [Fact]
    public async Task ManagerCanInviteStaffButNotManagerOrOwner()
    {
        using var factory = new MemberApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "owner-mm@branch.test",
                "Secure-Pass!42xx",
                "Member Cafe",
                "Ana"));
        var ownerToken = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.NotNull(ownerToken);

        var inviteManager = await client.SendAsync(Authorized(
            ownerToken.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/branches/{ownerToken.BranchId}/members",
            new ManagementInviteBranchMemberRequest(
                Email: "mgr-mm@branch.test",
                DisplayName: "Şube Müdürü",
                Password: "Secure-Pass!42mm",
                RoleKey: "manager")));
        Assert.Equal(HttpStatusCode.Created, inviteManager.StatusCode);

        var managerLogin = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("mgr-mm@branch.test", "Secure-Pass!42mm", ownerToken.TenantId, ownerToken.BranchId));
        var managerToken = await managerLogin.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, managerLogin.StatusCode);
        Assert.NotNull(managerToken);

        var staffOk = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/branches/{ownerToken.BranchId}/members",
            new ManagementInviteBranchMemberRequest(
                Email: "staff-mm@branch.test",
                DisplayName: "Garson Ali",
                Password: "Secure-Pass!42st",
                RoleKey: "staff")));
        var staff = await staffOk.Content.ReadFromJsonAsync<ManagementBranchMemberResponse>();
        Assert.Equal(HttpStatusCode.Created, staffOk.StatusCode);
        Assert.NotNull(staff);
        Assert.Equal("staff", staff.RoleKey);

        var managerBlocked = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/branches/{ownerToken.BranchId}/members",
            new ManagementInviteBranchMemberRequest(
                Email: "other-mgr@branch.test",
                DisplayName: "Başka Müdür",
                Password: "Secure-Pass!42ot",
                RoleKey: "manager")));
        Assert.Equal(HttpStatusCode.Forbidden, managerBlocked.StatusCode);

        var update = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Patch,
            $"/api/v1/management/branches/{ownerToken.BranchId}/members/{staff.MembershipId}",
            new ManagementUpdateBranchMemberRequest("Garson Ali Güncel", "05550001122")));
        var updated = await update.Content.ReadFromJsonAsync<ManagementBranchMemberResponse>();
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Garson Ali Güncel", updated?.DisplayName);

        var reset = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/branches/{ownerToken.BranchId}/members/{staff.MembershipId}/reset-password",
            new ManagementResetBranchMemberPasswordRequest("Secure-Pass!42nw")));
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var deactivateOwnerAttempt = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Get,
            $"/api/v1/management/branches/{ownerToken.BranchId}/members"));
        var members = await deactivateOwnerAttempt.Content.ReadFromJsonAsync<ManagementBranchMemberResponse[]>();
        Assert.Equal(HttpStatusCode.OK, deactivateOwnerAttempt.StatusCode);
        var ownerMembership = members?.First(x => x.Email == "owner-mm@branch.test");
        Assert.NotNull(ownerMembership);

        var deactivateOwner = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/branches/{ownerToken.BranchId}/members/{ownerMembership.MembershipId}/deactivate"));
        Assert.Equal(HttpStatusCode.Forbidden, deactivateOwner.StatusCode);

        var deactivateStaff = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/branches/{ownerToken.BranchId}/members/{staff.MembershipId}/deactivate"));
        Assert.Equal(HttpStatusCode.NoContent, deactivateStaff.StatusCode);

        var activateStaff = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/branches/{ownerToken.BranchId}/members/{staff.MembershipId}/activate"));
        Assert.Equal(HttpStatusCode.NoContent, activateStaff.StatusCode);

        var profile = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Patch,
            "/api/v1/management/auth/profile",
            new ManagementUpdateProfileRequest("Şube Müdürü Ad", "05551110000")));
        Assert.Equal(HttpStatusCode.NoContent, profile.StatusCode);

        var changePassword = await client.SendAsync(Authorized(
            managerToken.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/auth/change-password",
            new ManagementChangePasswordRequest("Secure-Pass!42mm", "Secure-Pass!42zz")));
        Assert.Equal(HttpStatusCode.NoContent, changePassword.StatusCode);
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

    private sealed class MemberApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _dbName = $"member-mgmt-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDbContextOptionsConfiguration<RestaurantOsDbContext>>();
                services.AddDbContext<RestaurantOsDbContext>(options =>
                    options.UseInMemoryDatabase(_dbName));
            });
        }
    }
}
