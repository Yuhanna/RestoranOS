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
        var ranked = EnumerateLanCandidates()
            .Where(candidate => IsPrivateLanAddress(candidate.Address))
            .OrderBy(Score)
            .Select(candidate => candidate.Address.ToString())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return ranked.FirstOrDefault();
    }

    private static int Score(LanCandidate candidate)
    {
        // Lower is better: real Wi-Fi DHCP beats Hyper-V / host-only Ethernet (.1).
        var score = 100;
        if (candidate.IsWireless)
        {
            score -= 50;
        }

        if (candidate.IsDhcp)
        {
            score -= 20;
        }

        if (candidate.IsLikelyHostOnlyOrGateway)
        {
            score += 40;
        }

        if (candidate.IsVirtual)
        {
            score += 80;
        }

        return score;
    }

    private static IEnumerable<LanCandidate> EnumerateLanCandidates()
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
            var description = networkInterface.Description;
            var isVirtual = IsVirtualAdapter(name, description);
            var isWireless = IsWirelessAdapter(name, description, networkInterface.NetworkInterfaceType);

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

                var bytes = address.Address.GetAddressBytes();
                var isLikelyHostOnlyOrGateway = bytes is [192, 168, _, 1]
                    || bytes is [10, _, _, 1]
                    || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31 && bytes[3] == 1);
                var isDhcp = OperatingSystem.IsWindows()
                    && address.PrefixOrigin == PrefixOrigin.Dhcp;

                yield return new LanCandidate(
                    address.Address,
                    isWireless,
                    isDhcp,
                    isVirtual,
                    isLikelyHostOnlyOrGateway);
            }
        }
    }

    private static bool IsWirelessAdapter(string name, string description, NetworkInterfaceType type) =>
        type == NetworkInterfaceType.Wireless80211
        || name.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
        || name.Contains("WiFi", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Wireless", StringComparison.OrdinalIgnoreCase)
        || description.Contains("Wi-Fi", StringComparison.OrdinalIgnoreCase)
        || description.Contains("Wireless", StringComparison.OrdinalIgnoreCase);

    private static bool IsVirtualAdapter(string name, string description)
    {
        ReadOnlySpan<string> markers =
        [
            "vEthernet",
            "Hyper-V",
            "Virtual",
            "VMware",
            "VirtualBox",
            "WSL",
            "Default Switch",
            "Loopback",
            "Bluetooth",
            "Local Area Connection*",
        ];

        foreach (var marker in markers)
        {
            if (name.Contains(marker, StringComparison.OrdinalIgnoreCase)
                || description.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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

    private sealed record LanCandidate(
        IPAddress Address,
        bool IsWireless,
        bool IsDhcp,
        bool IsVirtual,
        bool IsLikelyHostOnlyOrGateway);
}
