using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Domain;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class ManagementTableQrEndpointsTests
{
    private const string Password = "A-strong-test-password!42";

    [Fact]
    public async Task TableAndQrLifecycleStaysInsideTenantAndDoesNotPersistPlaintext()
    {
        using var factory = new ManagementTableApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeTableEdit: true);
        var access = await LoginForAccessAsync(client);

        var created = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/tables",
            new ManagementCreateTableRequest("Masa 12")));
        var table = await created.Content.ReadFromJsonAsync<ManagementTableResponse>();
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.NotNull(table);

        var generated = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{table.Id}/qr-codes"));
        var qr = await generated.Content.ReadFromJsonAsync<ManagementGeneratedQrResponse>();
        Assert.Equal(HttpStatusCode.Created, generated.StatusCode);
        Assert.NotNull(qr);
        Assert.True(qr.Token.Length >= 32);
        Assert.Contains("svg", qr.SvgMarkup, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("qr=", qr.EntryUrl, StringComparison.Ordinal);

        var listed = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            $"/api/v1/management/tables/{table.Id}/qr-codes"));
        var listedJson = await listed.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.DoesNotContain(qr.Token, listedJson, StringComparison.Ordinal);

        var second = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{table.Id}/qr-codes"));
        var secondQr = await second.Content.ReadFromJsonAsync<ManagementGeneratedQrResponse>();
        Assert.NotNull(secondQr);

        var previous = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            $"/api/v1/management/tables/{table.Id}/qr-codes"));
        var codes = await previous.Content.ReadFromJsonAsync<ManagementQrCodeResponse[]>();
        Assert.Contains(codes!, x => x.Id == qr.Id && x.Status == "inactive");
        Assert.Contains(codes!, x => x.Id == secondQr.Id && x.Status == "active");

        var resolved = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = secondQr.Token, locale = "tr" });
        Assert.Equal(HttpStatusCode.Conflict, resolved.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            db.Menus.Add(new PublishedMenu(Guid.NewGuid(), SeedIds.TenantA, SeedIds.BranchA, "Live", SeedIds.CreatedAtUtc));
            await db.SaveChangesAsync();
        }

        var liveResolve = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = secondQr.Token, locale = "tr" });
        Assert.Equal(HttpStatusCode.OK, liveResolve.StatusCode);

        var revoked = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/qr-codes/{secondQr.Id}/revoke"));
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);

        var afterRevoke = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new { qrToken = secondQr.Token, locale = "tr" });
        Assert.Equal(HttpStatusCode.NotFound, afterRevoke.StatusCode);

        var printRevoked = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            $"/api/v1/management/qr-codes/{secondQr.Id}/print"));
        Assert.Equal(HttpStatusCode.Conflict, printRevoked.StatusCode);

        var reprintInactive = await client.SendAsync(Authorized(
            access,
            HttpMethod.Get,
            $"/api/v1/management/qr-codes/{qr.Id}/print"));
        var print = await reprintInactive.Content.ReadFromJsonAsync<ManagementQrPrintResponse>();
        Assert.Equal(HttpStatusCode.OK, reprintInactive.StatusCode);
        Assert.NotNull(print);
        Assert.DoesNotContain("token", print.GetType().GetProperties().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var stored = await db.TableQrCodes.SingleAsync(x => x.Id == qr.Id);
            Assert.NotEqual(qr.Token, stored.TokenHash);
            Assert.Equal(OpaqueToken.Hash(qr.Token), stored.TokenHash);
            Assert.NotEqual(qr.Token, stored.ProtectedToken);
            Assert.DoesNotContain(qr.Token, stored.ProtectedToken ?? string.Empty, StringComparison.Ordinal);
            Assert.Null((await db.TableQrCodes.SingleAsync(x => x.Id == secondQr.Id)).ProtectedToken);
            Assert.Contains(await db.ManagementAuditLogs.ToListAsync(), x => x.Action == "QrGenerated");
        }
    }

    [Fact]
    public async Task ForeignTableAndMissingPermissionAreIsolated()
    {
        using var factory = new ManagementTableApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeTableEdit: true);
        var access = await LoginForAccessAsync(client);

        var foreign = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{SeedIds.TableB}/qr-codes"));
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        using var viewOnlyFactory = new ManagementTableApiFactory();
        using var viewOnlyClient = viewOnlyFactory.CreateClient();
        await SeedAsync(viewOnlyFactory, includeTableEdit: false);
        var viewAccess = await LoginForAccessAsync(viewOnlyClient);
        var denied = await viewOnlyClient.SendAsync(Authorized(
            viewAccess,
            HttpMethod.Post,
            "/api/v1/management/tables",
            new ManagementCreateTableRequest("Masa X")));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var viewOk = await viewOnlyClient.SendAsync(Authorized(
            viewAccess,
            HttpMethod.Get,
            "/api/v1/management/tables"));
        Assert.Equal(HttpStatusCode.OK, viewOk.StatusCode);
    }

    [Fact]
    public async Task DuplicateLabelAndRevokedActivateAreRejected()
    {
        using var factory = new ManagementTableApiFactory();
        using var client = factory.CreateClient();
        await SeedAsync(factory, includeTableEdit: true);
        var access = await LoginForAccessAsync(client);

        var first = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/tables",
            new ManagementCreateTableRequest("Teras 1")));
        var duplicate = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            "/api/v1/management/tables",
            new ManagementCreateTableRequest("Teras 1")));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var table = await first.Content.ReadFromJsonAsync<ManagementTableResponse>();
        var generated = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/tables/{table!.Id}/qr-codes"));
        var qr = await generated.Content.ReadFromJsonAsync<ManagementGeneratedQrResponse>();
        await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/qr-codes/{qr!.Id}/revoke"));
        var activate = await client.SendAsync(Authorized(
            access,
            HttpMethod.Post,
            $"/api/v1/management/qr-codes/{qr.Id}/activate"));
        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
    }

    private static async Task<string> LoginForAccessAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/v1/management/auth/login",
            new ManagementLoginRequest("owner@example.test", Password, SeedIds.TenantA, SeedIds.BranchA));
        var access = await response.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(access);
        return access.AccessToken;
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

    private static async Task SeedAsync(ManagementTableApiFactory factory, bool includeTableEdit)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
        await db.Database.EnsureCreatedAsync();
        var now = SeedIds.CreatedAtUtc;
        var role = new ManagementRole(SeedIds.Role, "RestaurantManager");
        var user = new ManagementUser(
            SeedIds.User,
            "owner@example.test",
            "OWNER@EXAMPLE.TEST",
            "pending",
            now);
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ManagementUser>>();
        user.UpdatePasswordHash(hasher.HashPassword(user, Password));
        db.AddRange(
            new Tenant(SeedIds.TenantA, "Tenant A"),
            new Restaurant(SeedIds.RestaurantA, SeedIds.TenantA, "Restaurant A"),
            new Branch(SeedIds.BranchA, SeedIds.TenantA, SeedIds.RestaurantA, "Branch A"),
            new Tenant(SeedIds.TenantB, "Tenant B"),
            new Restaurant(SeedIds.RestaurantB, SeedIds.TenantB, "Restaurant B"),
            new Branch(SeedIds.BranchB, SeedIds.TenantB, SeedIds.RestaurantB, "Branch B"),
            new DiningTable(SeedIds.TableB, SeedIds.TenantB, SeedIds.BranchB, "Foreign"),
            role,
            new ManagementRolePermissionGrant(role.Id, ManagementPermissions.TableView),
            user,
            new ManagementMembership(
                Guid.NewGuid(),
                user.Id,
                SeedIds.TenantA,
                SeedIds.BranchA,
                role.Id));
        if (includeTableEdit)
        {
            db.ManagementRolePermissions.Add(
                new ManagementRolePermissionGrant(role.Id, ManagementPermissions.TableEdit));
        }

        await db.SaveChangesAsync();
    }

    public sealed class ManagementTableApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"table-tests-{Guid.NewGuid()}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting(
                "ManagementAuth:SigningKey",
                "test-only-signing-key-32-bytes-minimum-value");
            builder.UseSetting("CustomerWeb:PublicBaseUrl", "http://localhost:5173");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<RestaurantOsDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<RestaurantOsDbContext>>();
                services.RemoveAll<RestaurantOsDbContext>();
                services.AddDbContext<RestaurantOsDbContext>(
                    options => options.UseInMemoryDatabase(_databaseName));
            });
        }
    }

    private static class SeedIds
    {
        public static readonly Guid TenantA = Guid.Parse("41000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantA = Guid.Parse("41000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchA = Guid.Parse("41000000-0000-0000-0000-000000000003");
        public static readonly Guid TenantB = Guid.Parse("42000000-0000-0000-0000-000000000001");
        public static readonly Guid RestaurantB = Guid.Parse("42000000-0000-0000-0000-000000000002");
        public static readonly Guid BranchB = Guid.Parse("42000000-0000-0000-0000-000000000003");
        public static readonly Guid TableB = Guid.Parse("42000000-0000-0000-0000-000000000004");
        public static readonly Guid User = Guid.Parse("43000000-0000-0000-0000-000000000001");
        public static readonly Guid Role = Guid.Parse("43000000-0000-0000-0000-000000000002");
        public static readonly DateTimeOffset CreatedAtUtc =
            DateTimeOffset.Parse("2026-08-25T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
    }
}
