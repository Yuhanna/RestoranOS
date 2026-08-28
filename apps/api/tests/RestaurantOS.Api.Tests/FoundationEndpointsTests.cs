using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Api.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class FoundationEndpointsTests : IClassFixture<FoundationEndpointsTests.FoundationApiFactory>
{
    private readonly HttpClient _client;

    public FoundationEndpointsTests(FoundationApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SystemEndpointReturnsVersionedServiceContract()
    {
        var response = await _client.GetAsync("/api/v1/system");
        var body = await response.Content.ReadFromJsonAsync<SystemInfoResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("restaurant-os-api", body.Service);
        Assert.Equal("healthy", body.Status);
        Assert.NotEqual(default, body.TimestampUtc);
    }

    [Fact]
    public async Task LiveHealthEndpointDoesNotRequireDatabase()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CorrelationIdIsPreservedAndSecurityHeadersAreSet()
    {
        const string correlationId = "foundation-test-correlation";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await _client.SendAsync(request);

        Assert.Equal(correlationId, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
    }

    [Fact]
    public async Task UnsafeCorrelationIdIsReplaced()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/system");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "unsafe value");

        var response = await _client.SendAsync(request);
        var returnedValue = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();

        Assert.NotEqual("unsafe value", returnedValue);
        Assert.Matches("^[a-f0-9]{32}$", returnedValue);
    }

    public sealed class FoundationApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ManagementAuth:SigningKey", TestSigningKey);
    }

    private const string TestSigningKey = "test-only-signing-key-32-bytes-minimum-value";
}
