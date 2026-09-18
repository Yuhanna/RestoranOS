namespace RestaurantOS.Application;

public sealed record CustomerClientContext(string? DeviceId, string? ClientIp);

public sealed class CustomerOrderRateLimitOptions
{
    public const string SectionName = "RateLimits:CustomerOrders";

    public int GuestPermitLimit { get; init; } = 10;
    public int GuestWindowMinutes { get; init; } = 30;
    public int DevicePermitLimit { get; init; } = 5;
    public int DeviceWindowMinutes { get; init; } = 5;
    public int IpPermitLimit { get; init; } = 20;
    public int IpWindowMinutes { get; init; } = 5;
    public int RiskBlockThreshold { get; init; } = 5;
    public int RiskWindowMinutes { get; init; } = 15;
}

public interface ICustomerOrderGuard
{
    /// <summary>Throws if the guest/device is over quota or blocked. Does not consume quota.</summary>
    void EnsureCanPlaceOrder(Guid customerSessionId, CustomerClientContext? client);

    /// <summary>Increments rate-limit counters after an order is persisted.</summary>
    void RecordSuccessfulOrder(Guid customerSessionId, CustomerClientContext? client);

    void RecordFailedRequest(CustomerClientContext? client, Guid? customerSessionId = null);
}
