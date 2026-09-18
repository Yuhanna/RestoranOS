using RestaurantOS.Domain;

namespace RestaurantOS.Api.Tests;

public sealed class MenuPromotionScheduleTests
{
    private static readonly TimeZoneInfo Istanbul = ResolveIstanbul();

    [Fact]
    public void WeekdayMaskBlocksInactiveDays()
    {
        var promotion = NewPromotion(daysOfWeekMask: 0b0011_1110, dailyStart: null, dailyEnd: null);
        var monday = new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
        var saturday = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

        Assert.True(promotion.IsActiveAt(monday, Istanbul));
        Assert.False(promotion.IsActiveAt(saturday, Istanbul));
    }

    [Fact]
    public void DailyWindowUsesBranchTimezone()
    {
        var promotion = NewPromotion(
            daysOfWeekMask: null,
            dailyStart: new TimeOnly(17, 0),
            dailyEnd: new TimeOnly(19, 0));

        var during = new DateTimeOffset(2026, 9, 10, 14, 30, 0, TimeSpan.Zero);
        var after = new DateTimeOffset(2026, 9, 10, 16, 30, 0, TimeSpan.Zero);

        Assert.True(promotion.IsActiveAt(during, Istanbul));
        Assert.False(promotion.IsActiveAt(after, Istanbul));
    }

    [Fact]
    public void EffectiveEndsAtPrefersEarlierDailyEnd()
    {
        var promotion = NewPromotion(
            daysOfWeekMask: null,
            dailyStart: new TimeOnly(12, 0),
            dailyEnd: new TimeOnly(15, 0),
            endsAtUtc: new DateTimeOffset(2026, 12, 31, 21, 0, 0, TimeSpan.Zero));

        var now = new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero);
        var effective = promotion.EffectiveEndsAtUtc(now, Istanbul);
        Assert.NotNull(effective);
        Assert.True(effective < promotion.EndsAtUtc);
    }

    private static MenuPromotion NewPromotion(
        byte? daysOfWeekMask,
        TimeOnly? dailyStart,
        TimeOnly? dailyEnd,
        DateTimeOffset? endsAtUtc = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test",
            PromotionScopes.AllMenu,
            DiscountKinds.Percent,
            20,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            endsAtUtc,
            dailyStart,
            dailyEnd,
            null,
            null,
            true,
            daysOfWeekMask);

    private static TimeZoneInfo ResolveIstanbul()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }
    }
}
