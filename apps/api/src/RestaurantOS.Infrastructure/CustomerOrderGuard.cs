using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using RestaurantOS.Application;

namespace RestaurantOS.Infrastructure;

public sealed class CustomerOrderGuard(
    IMemoryCache cache,
    IOptions<CustomerOrderRateLimitOptions> options) : ICustomerOrderGuard
{
    private sealed class Counter
    {
        public int Value;
    }

    public void EnsureCanPlaceOrder(Guid customerSessionId, CustomerClientContext? client)
    {
        var settings = options.Value;
        if (GetRiskScore(customerSessionId, client?.ClientIp) >= settings.RiskBlockThreshold)
        {
            throw new CustomerExperienceException(
                "ORDER_BLOCKED",
                "Order temporarily unavailable due to unusual activity.");
        }

        var guestCount = Increment(
            $"customer-order:guest:{customerSessionId:N}",
            TimeSpan.FromMinutes(settings.GuestWindowMinutes));
        if (guestCount > settings.GuestPermitLimit)
        {
            throw new CustomerExperienceException(
                "ORDER_RATE_LIMITED",
                "Too many orders from this guest session. Please wait a moment.");
        }

        if (!string.IsNullOrWhiteSpace(client?.DeviceId))
        {
            var deviceCount = Increment(
                $"customer-order:device:{client.DeviceId.Trim()}",
                TimeSpan.FromMinutes(settings.DeviceWindowMinutes));
            if (deviceCount > settings.DevicePermitLimit)
            {
                throw new CustomerExperienceException(
                    "ORDER_RATE_LIMITED",
                    "Too many orders from this device. Please wait a moment.");
            }
        }

        if (!string.IsNullOrWhiteSpace(client?.ClientIp))
        {
            var ipCount = Increment(
                $"customer-order:ip:{client.ClientIp.Trim()}",
                TimeSpan.FromMinutes(settings.IpWindowMinutes));
            if (ipCount > settings.IpPermitLimit)
            {
                AddRisk(customerSessionId, client.ClientIp, 2);
            }
        }

        if (GetRiskScore(customerSessionId, client?.ClientIp) >= settings.RiskBlockThreshold)
        {
            throw new CustomerExperienceException(
                "ORDER_BLOCKED",
                "Order temporarily unavailable due to unusual activity.");
        }
    }

    public void RecordFailedRequest(CustomerClientContext? client, Guid? customerSessionId = null)
    {
        AddRisk(customerSessionId ?? Guid.Empty, client?.ClientIp, 1);
    }

    private int Increment(string key, TimeSpan window)
    {
        var counter = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new Counter();
        })!;
        return Interlocked.Increment(ref counter.Value);
    }

    private void AddRisk(Guid customerSessionId, string? clientIp, int points)
    {
        var settings = options.Value;
        var window = TimeSpan.FromMinutes(settings.RiskWindowMinutes);
        if (customerSessionId != Guid.Empty)
        {
            IncrementRisk($"customer-risk:session:{customerSessionId:N}", points, window);
        }

        if (!string.IsNullOrWhiteSpace(clientIp))
        {
            IncrementRisk($"customer-risk:ip:{clientIp.Trim()}", points, window);
        }
    }

    private void IncrementRisk(string key, int points, TimeSpan window)
    {
        var counter = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = window;
            return new Counter();
        })!;
        Interlocked.Add(ref counter.Value, points);
    }

    private int GetRiskScore(Guid customerSessionId, string? clientIp)
    {
        var sessionRisk = customerSessionId == Guid.Empty
            ? 0
            : cache.Get<Counter>($"customer-risk:session:{customerSessionId:N}")?.Value ?? 0;
        var ipRisk = string.IsNullOrWhiteSpace(clientIp)
            ? 0
            : cache.Get<Counter>($"customer-risk:ip:{clientIp.Trim()}")?.Value ?? 0;
        return Math.Max(sessionRisk, ipRisk);
    }
}
