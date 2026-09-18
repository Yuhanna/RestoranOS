using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class ManagementOrderItemSummaryTests
{
    [Fact]
    public void BuildItemSummaryShowsFirstTwoAndRemainderCount()
    {
        var summary = ManagementOrderService.BuildItemSummary(
        [
            ("Lahmacun", 2),
            ("Ayran", 1),
            ("Çorba", 1),
        ]);

        Assert.Equal("1× Ayran · 2× Lahmacun · +1", summary);
    }

    [Fact]
    public void BuildItemSummaryReturnsEmptyWhenNoItems()
    {
        Assert.Equal(string.Empty, ManagementOrderService.BuildItemSummary([]));
    }
}
