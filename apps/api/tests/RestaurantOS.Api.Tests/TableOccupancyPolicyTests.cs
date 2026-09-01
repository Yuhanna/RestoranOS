using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api.Tests;

public sealed class TableOccupancyPolicyTests
{
    private static readonly DateTimeOffset Base = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SessionLifetimeIsOneHourFortyMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(100), TableOccupancyPolicy.SessionLifetime);
    }

    [Fact]
    public void PostBillInactivityGraceIsFifteenMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(15), TableOccupancyPolicy.PostBillInactivityGrace);
    }

    [Fact]
    public void CountsAsOccupiedWhenSessionActiveAndNoBillCompleted()
    {
        var occupied = TableOccupancyPolicy.CountsAsOccupied(
            now: Base.AddMinutes(30),
            sessionExpiresAtUtc: Base.AddMinutes(100),
            lastSessionActivityUtc: Base,
            lastBillCompletedAtUtc: null);

        Assert.True(occupied);
    }

    [Fact]
    public void CountsAsOccupiedDuringPostBillGraceWithoutNewActivity()
    {
        var billCompleted = Base.AddMinutes(10);
        var occupied = TableOccupancyPolicy.CountsAsOccupied(
            now: billCompleted.AddMinutes(10),
            sessionExpiresAtUtc: Base.AddMinutes(100),
            lastSessionActivityUtc: Base,
            lastBillCompletedAtUtc: billCompleted);

        Assert.True(occupied);
    }

    [Fact]
    public void DoesNotCountAsOccupiedAfterPostBillGraceWithoutActivity()
    {
        var billCompleted = Base.AddMinutes(10);
        var occupied = TableOccupancyPolicy.CountsAsOccupied(
            now: billCompleted.AddMinutes(16),
            sessionExpiresAtUtc: Base.AddMinutes(100),
            lastSessionActivityUtc: Base,
            lastBillCompletedAtUtc: billCompleted);

        Assert.False(occupied);
    }

    [Fact]
    public void CountsAsOccupiedAfterBillWhenCustomerActivityOccurred()
    {
        var billCompleted = Base.AddMinutes(10);
        var occupied = TableOccupancyPolicy.CountsAsOccupied(
            now: billCompleted.AddMinutes(30),
            sessionExpiresAtUtc: Base.AddMinutes(100),
            lastSessionActivityUtc: billCompleted.AddMinutes(5),
            lastBillCompletedAtUtc: billCompleted);

        Assert.True(occupied);
    }

    [Fact]
    public void DoesNotCountAsOccupiedWhenSessionExpired()
    {
        var occupied = TableOccupancyPolicy.CountsAsOccupied(
            now: Base.AddMinutes(101),
            sessionExpiresAtUtc: Base.AddMinutes(100),
            lastSessionActivityUtc: Base,
            lastBillCompletedAtUtc: null);

        Assert.False(occupied);
    }

    [Fact]
    public void DoesNotCountAsOccupiedAfterInactivityExceedsSessionLifetime()
    {
        var occupied = TableOccupancyPolicy.CountsAsOccupied(
            now: Base.AddMinutes(101),
            sessionExpiresAtUtc: Base.AddHours(4),
            lastSessionActivityUtc: Base,
            lastBillCompletedAtUtc: null);

        Assert.False(occupied);
    }
}
