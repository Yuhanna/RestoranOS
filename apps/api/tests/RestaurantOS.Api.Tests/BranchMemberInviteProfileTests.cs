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

public sealed class BranchMemberInviteProfileTests
{
    [Fact]
    public async Task InviteRequiresDisplayNameAndPersistsPhone()
    {
        using var factory = new InviteApiFactory();
        using var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(
                "owner-invite@profile.test",
                "Secure-Pass!42xx",
                "Profile Cafe",
                "Ana"));
        var token = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.NotNull(token);

        var missingName = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/branches/{token.BranchId}/members",
            new ManagementInviteBranchMemberRequest(
                Email: "garson@profile.test",
                DisplayName: "  ",
                Phone: "05551112233",
                Password: "Secure-Pass!42yy",
                RoleKey: "staff")));
        Assert.Equal(HttpStatusCode.BadRequest, missingName.StatusCode);

        var created = await client.SendAsync(Authorized(
            token.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/branches/{token.BranchId}/members",
            new ManagementInviteBranchMemberRequest(
                Email: "garson@profile.test",
                DisplayName: "Ali Garson",
                Phone: "05551112233",
                Password: "Secure-Pass!42yy",
                RoleKey: "staff")));
        var member = await created.Content.ReadFromJsonAsync<ManagementBranchMemberResponse>();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(member);
        Assert.Equal("Ali Garson", member.DisplayName);
        Assert.Equal("05551112233", member.Phone);
        Assert.Equal("garson@profile.test", member.Email);
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

    private sealed class InviteApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"invite-profile-{Guid.NewGuid():N}";

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
