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

/// <summary>
/// Release 1 (§113) exit-criteria flow: register → table → QR → menu → customer order →
/// restaurant visibility → status change → customer tracking.
/// Runs against the API host in-process (HTTP), without a browser.
/// </summary>
public sealed class Release1EndToEndTests
{
    private const string Password = "Secure-Pass!42xx";

    [Fact]
    public async Task QrToOrderTrackingFlowCompletesForFreshTenant()
    {
        using var factory = new Release1ApiFactory();
        using var client = factory.CreateClient();
        var email = $"release1-e2e-{Guid.NewGuid():N}@local.test";

        var register = await client.PostAsJsonAsync(
            "/api/v1/management/auth/register",
            new ManagementRegisterRequest(email, Password, "E2E Restoran", "Merkez Şube"));
        var access = await register.Content.ReadFromJsonAsync<ManagementAccessTokenResponse>();
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        Assert.NotNull(access);

        var tableResponse = await client.SendAsync(Authorized(
            access.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/tables",
            new ManagementCreateTableRequest("Masa 1")));
        var table = await tableResponse.Content.ReadFromJsonAsync<ManagementTableResponse>();
        Assert.Equal(HttpStatusCode.Created, tableResponse.StatusCode);
        Assert.NotNull(table);

        var menuResponse = await client.SendAsync(Authorized(
            access.AccessToken,
            HttpMethod.Post,
            "/api/v1/management/menus",
            new ManagementCreateMenuRequest("Gün Menüsü")));
        var menu = await menuResponse.Content.ReadFromJsonAsync<ManagementMenuSummaryResponse>();
        Assert.Equal(HttpStatusCode.Created, menuResponse.StatusCode);
        Assert.NotNull(menu);

        var categoryResponse = await client.SendAsync(Authorized(
            access.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu.Id}/categories",
            new ManagementCreateCategoryRequest("Ana yemek", 1)));
        var category = await categoryResponse.Content.ReadFromJsonAsync<ManagementMenuCategoryResponse>();
        Assert.Equal(HttpStatusCode.Created, categoryResponse.StatusCode);
        Assert.NotNull(category);

        var itemResponse = await client.SendAsync(Authorized(
            access.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu.Id}/items",
            new ManagementCreateMenuItemRequest(category.Id, "Köfte", "Izgara", 25_000, true, 1)));
        var item = await itemResponse.Content.ReadFromJsonAsync<ManagementMenuItemResponse>();
        Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);
        Assert.NotNull(item);

        var publishResponse = await client.SendAsync(Authorized(
            access.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/menus/{menu.Id}/publish"));
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        var qrResponse = await client.SendAsync(Authorized(
            access.AccessToken,
            HttpMethod.Post,
            $"/api/v1/management/tables/{table.Id}/qr-codes"));
        var qr = await qrResponse.Content.ReadFromJsonAsync<ManagementGeneratedQrResponse>();
        Assert.Equal(HttpStatusCode.Created, qrResponse.StatusCode);
        Assert.NotNull(qr);
        Assert.True(qr.Token.Length >= 32);

        var sessionResponse = await client.PostAsJsonAsync(
            "/api/v1/customer/sessions/resolve",
            new ResolveQrRequest(qr.Token, "tr"));
        var session = await sessionResponse.Content.ReadFromJsonAsync<CustomerSessionResponse>();
        Assert.Equal(HttpStatusCode.OK, sessionResponse.StatusCode);
        Assert.NotNull(session);
        Assert.Equal("E2E Restoran", session.RestaurantName);
        Assert.Equal("Merkez Şube", session.BranchName);
        Assert.Equal("Masa 1", session.TableLabel);
        Assert.Contains(session.Products, product => product.Id == item.Id.ToString() && product.Name == "Köfte");

        using var orderRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/customer/orders");
        orderRequest.Headers.Add("Idempotency-Key", $"release1-e2e-{Guid.NewGuid():N}");
        orderRequest.Content = JsonContent.Create(new CreateCustomerOrderRequest(
            session.SessionToken,
            [new CreateCustomerOrderLineRequest(item.Id.ToString(), 2, [], "Az acılı")]));
        var createOrderResponse = await client.SendAsync(orderRequest);
        var customerOrder = await createOrderResponse.Content.ReadFromJsonAsync<CustomerOrderResponse>();
        Assert.Equal(HttpStatusCode.Created, createOrderResponse.StatusCode);
        Assert.NotNull(customerOrder);
        Assert.Equal("submitted", customerOrder.Status);
        Assert.Equal(50_000, customerOrder.Total.AmountMinor);

        var activeResponse = await client.SendAsync(Authorized(
            access.AccessToken,
            HttpMethod.Get,
            "/api/v1/management/orders/active"));
        var activeOrders = await activeResponse.Content.ReadFromJsonAsync<ManagementOrderResponse[]>();
        Assert.Equal(HttpStatusCode.OK, activeResponse.StatusCode);
        Assert.NotNull(activeOrders);
        Assert.Contains(activeOrders, order => order.Id == Guid.Parse(customerOrder.Id));

        var detailResponse = await client.SendAsync(Authorized(
            access.AccessToken,
            HttpMethod.Get,
            $"/api/v1/management/orders/{customerOrder.Id}"));
        var detail = await detailResponse.Content.ReadFromJsonAsync<ManagementOrderDetailResponse>();
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        Assert.NotNull(detail);
        Assert.Single(detail.Items);
        Assert.Equal("Köfte", detail.Items[0].Name);
        Assert.Equal(2, detail.Items[0].Quantity);
        Assert.Equal("Az acılı", detail.Items[0].Note);
        Assert.NotEmpty(detail.StatusHistory);
        Assert.Equal("submitted", detail.StatusHistory[0].Status);

        var statusResponse = await client.SendAsync(Authorized(
            access.AccessToken,
            HttpMethod.Put,
            $"/api/v1/management/orders/{customerOrder.Id}/status",
            new ManagementChangeOrderStatusRequest("accepted", customerOrder.StatusChangedAt)));
        var updated = await statusResponse.Content.ReadFromJsonAsync<ManagementOrderResponse>();
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal("accepted", updated.Status);

        using var trackRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/customer/orders/{customerOrder.Id}");
        trackRequest.Headers.Add("X-Customer-Session", session.SessionToken);
        var trackResponse = await client.SendAsync(trackRequest);
        var tracked = await trackResponse.Content.ReadFromJsonAsync<CustomerOrderResponse>();
        Assert.Equal(HttpStatusCode.OK, trackResponse.StatusCode);
        Assert.NotNull(tracked);
        Assert.Equal("accepted", tracked.Status);
        Assert.True(tracked.EstimatedReadyAt > DateTimeOffset.UtcNow.AddMinutes(-1));
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

    private sealed class Release1ApiFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"release1-e2e-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting(
                "ManagementAuth:SigningKey",
                "test-only-signing-key-32-bytes-minimum-value");
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
}
