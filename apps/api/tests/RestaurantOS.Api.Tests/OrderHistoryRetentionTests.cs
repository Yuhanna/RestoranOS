using RestaurantOS.Domain;
using Xunit;

namespace RestaurantOS.Api.Tests;

public sealed class OrderHistoryRetentionTests
{
    [Theory]
    [InlineData("Free", false, 72)]
    [InlineData("Pro", false, 720)]
    [InlineData("Pro", true, 720)]
    [InlineData("Enterprise", false, 2160)]
    [InlineData(null, false, 72)]
    public void ResolveMaxHoursMatchesPlanPolicy(string? planCode, bool isTrial, int expected)
    {
        Assert.Equal(expected, OrderHistoryRetention.ResolveMaxHours(planCode, isTrial));
    }

    [Fact]
    public void ClampRequestedHoursCapsAtPlanMax()
    {
        Assert.Equal(72, OrderHistoryRetention.ClampRequestedHours(168, OrderHistoryRetention.FreeMaxHours));
        Assert.Equal(720, OrderHistoryRetention.ClampRequestedHours(5000, OrderHistoryRetention.ProMaxHours));
        Assert.Equal(24, OrderHistoryRetention.ClampRequestedHours(24, OrderHistoryRetention.FreeMaxHours));
        Assert.Equal(1, OrderHistoryRetention.ClampRequestedHours(0, OrderHistoryRetention.FreeMaxHours));
    }
}
