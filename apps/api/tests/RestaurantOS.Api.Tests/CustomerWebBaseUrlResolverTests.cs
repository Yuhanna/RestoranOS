using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using RestaurantOS.Application;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class CustomerWebBaseUrlResolverTests
{
    [Fact]
    public void DevelopmentLoopbackUsesPreferredLanWhenAvailable()
    {
        var lan = CustomerWebBaseUrlResolver.TryGetPreferredLanIPv4();
        var resolved = CustomerWebBaseUrlResolver.Resolve(
            new CustomerWebOptions { PublicBaseUrl = "http://localhost:5173" },
            new TestHostEnvironment(isDevelopment: true));

        if (lan is null)
        {
            Assert.Equal("http://localhost:5173", resolved);
            return;
        }

        Assert.Equal($"http://{lan}:5173", resolved);
    }

    [Fact]
    public void ProductionKeepsConfiguredUrl()
    {
        var resolved = CustomerWebBaseUrlResolver.Resolve(
            new CustomerWebOptions { PublicBaseUrl = "https://menu.example.com" },
            new TestHostEnvironment(isDevelopment: false));

        Assert.Equal("https://menu.example.com", resolved);
    }

    [Fact]
    public void PreferredLanPrefersWifiOverHostOnlyEthernet()
    {
        var lan = CustomerWebBaseUrlResolver.TryGetPreferredLanIPv4();
        if (lan is null)
        {
            return;
        }

        // On this Windows host, Wi-Fi is 10.x DHCP; Hyper-V/host-only often ends with .1.
        Assert.False(
            lan.EndsWith(".1", StringComparison.Ordinal),
            $"Preferred LAN should not be a host-only/gateway IP (.1). Got {lan}");
    }

    private sealed class TestHostEnvironment(bool isDevelopment) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = isDevelopment ? Environments.Development : Environments.Production;
        public string ApplicationName { get; set; } = "RestaurantOS.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
