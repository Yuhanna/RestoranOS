namespace RestaurantOS.Application;

public sealed record CustomerClientContext(string? DeviceId, string? ClientIp);

public sealed class CustomerOrderRateLimitOptions
{
    public const string SectionName = "RateLimits:CustomerOrders";

    public int GuestPermitLimit { get; init; } = 2;
    public int GuestWindowMinutes { get; init; } = 1;
    public int DevicePermitLimit { get; init; } = 5;
    public int DeviceWindowMinutes { get; init; } = 5;
    public int IpPermitLimit { get; init; } = 20;
    public int IpWindowMinutes { get; init; } = 5;
    public int RiskBlockThreshold { get; init; } = 5;
    public int RiskWindowMinutes { get; init; } = 15;
}

public interface ICustomerOrderGuard
{
    void EnsureCanPlaceOrder(Guid customerSessionId, CustomerClientContext? client);

    void RecordFailedRequest(CustomerClientContext? client, Guid? customerSessionId = null);
}
