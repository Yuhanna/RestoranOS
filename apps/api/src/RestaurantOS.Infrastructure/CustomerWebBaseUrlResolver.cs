using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using RestaurantOS.Application;

namespace RestaurantOS.Infrastructure;

public static class CustomerWebBaseUrlResolver
{
    public static string Resolve(CustomerWebOptions options, IHostEnvironment? environment = null)
    {
        var configured = (options.PublicBaseUrl ?? "http://localhost:5173").Trim().TrimEnd('/');
        if (environment is not null && !environment.IsDevelopment())
        {
            return configured;
        }

        var preferredLan = TryGetPreferredLanIPv4();
        if (preferredLan is null)
        {
            return configured;
        }

        if (IsLoopbackUrl(configured))
        {
            return WithHostPort(configured, preferredLan);
        }

        if (Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            && uri.Port == 5173
            && IPAddress.TryParse(uri.Host, out var configuredIp)
            && configuredIp.AddressFamily == AddressFamily.InterNetwork
            && IsPrivateLanAddress(configuredIp)
            && preferredLan != uri.Host)
        {
            return WithHostPort(configured, preferredLan);
        }

        return configured;
    }

    public static string? TryGetPreferredLanIPv4()
    {
        foreach (var candidate in EnumerateLanCandidates())
        {
            if (IsPrivateLanAddress(candidate))
            {
                return candidate.ToString();
            }
        }

        return null;
    }

    private static IEnumerable<IPAddress> EnumerateLanCandidates()
    {
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var name = networkInterface.Name;
            var isPreferredNic = name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
                || name.Contains("WiFi", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Ethernet", StringComparison.OrdinalIgnoreCase);

            foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(address.Address))
                {
                    continue;
                }

                if (!isPreferredNic)
                {
                    continue;
                }

                yield return address.Address;
            }
        }

        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            foreach (var address in networkInterface.GetIPProperties().UnicastAddresses)
            {
                if (address.Address.AddressFamily != AddressFamily.InterNetwork)
                {
                    continue;
                }

                if (IPAddress.IsLoopback(address.Address))
                {
                    continue;
                }

                yield return address.Address;
            }
        }
    }

    private static bool IsLoopbackUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.IsLoopback || uri.Host is "localhost" or "127.0.0.1");

    private static bool IsPrivateLanAddress(IPAddress ip)
    {
        if (ip.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }

    private static string WithHostPort(string configured, string host)
    {
        if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri))
        {
            return $"http://{host}:5173";
        }

        var builder = new UriBuilder(uri)
        {
            Host = host,
            Port = uri.Port > 0 ? uri.Port : 5173,
        };
        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
