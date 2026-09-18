using RestaurantOS.Domain;
using Xunit;

namespace RestaurantOS.Api.Tests;

public sealed class MonetizationPolicyTests
{
    [Fact]
    public void CatalogContainsCoreSellableFeatures()
    {
        Assert.Contains(MonetizationPolicy.All, x => x.FeatureKey == MonetizationPolicy.ActiveQr);
        Assert.Contains(MonetizationPolicy.All, x => x.FeatureKey == MonetizationPolicy.MenuThemes);
        Assert.Contains(MonetizationPolicy.All, x => x.FeatureKey == MonetizationPolicy.AnalyticsLookback);
        Assert.Contains(MonetizationPolicy.All, x => x.FeatureKey == MonetizationPolicy.MultiBranch);
        Assert.Equal(MonetizationGateMode.SoftDiscover, MonetizationPolicy.MenuThemesFeature.GateMode);
        Assert.Equal(MonetizationGateMode.CapacityMeter, MonetizationPolicy.ActiveQrFeature.GateMode);
        Assert.True(MonetizationPolicy.AnalyticsLookbackFeature.AhaSafe);
    }

    [Theory]
    [InlineData(0, 8, "ok")]
    [InlineData(5, 8, "ok")]
    [InlineData(6, 8, "warn")]
    [InlineData(8, 8, "critical")]
    [InlineData(3, null, "ok")]
    public void UsageToneFollowsWarnAndCriticalThresholds(int used, int? max, string expected)
    {
        Assert.Equal(expected, MonetizationPolicy.ResolveUsageTone(used, max));
    }

    [Fact]
    public void AnalyticsDaysClampToFreeWindow()
    {
        Assert.Equal(3, MonetizationPolicy.ResolveMaxAnalyticsDays(OrderHistoryRetention.FreeMaxHours));
        Assert.Equal(3, MonetizationPolicy.ClampAnalyticsDays(30, OrderHistoryRetention.FreeMaxHours));
        Assert.Equal(30, MonetizationPolicy.ClampAnalyticsDays(30, OrderHistoryRetention.ProMaxHours));
    }

    [Theory]
    [InlineData(10, "low")]
    [InlineData(7, "medium")]
    [InlineData(3, "high")]
    [InlineData(1, "critical")]
    [InlineData(0, "critical")]
    public void TrialUrgencyEscalatesNearExpiry(int days, string expected)
    {
        Assert.Equal(expected, MonetizationPolicy.TrialUrgency(days));
    }
}
