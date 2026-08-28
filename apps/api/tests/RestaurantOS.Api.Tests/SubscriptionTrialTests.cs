using RestaurantOS.Domain;

namespace RestaurantOS.Api.Tests;

public sealed class SubscriptionTrialTests
{
    [Fact]
    public void CreateProTrialLastsThirtyDays()
    {
        var started = new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);
        var trial = TenantSubscription.CreateProTrial(Guid.NewGuid(), started);

        Assert.Equal(SubscriptionPlanCodes.Pro, trial.PlanCode);
        Assert.Equal(started.AddDays(30), trial.ExpiresAtUtc);
        Assert.True(trial.IsTrialActive(started.AddDays(12)));
        Assert.False(trial.IsExpired(started.AddDays(12)));
    }

    [Fact]
    public void ExpiredTrialCanDowngradeToFree()
    {
        var started = new DateTimeOffset(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);
        var trial = TenantSubscription.CreateProTrial(Guid.NewGuid(), started);
        var afterExpiry = started.AddDays(31);

        Assert.True(trial.IsExpired(afterExpiry));
        trial.DowngradeToFree(afterExpiry);

        Assert.Equal(SubscriptionPlanCodes.Free, trial.PlanCode);
        Assert.Null(trial.ExpiresAtUtc);
        Assert.False(trial.IsTrialActive(afterExpiry));
    }

    [Fact]
    public void ConvertToPaidClearsExpiry()
    {
        var started = DateTimeOffset.UtcNow;
        var trial = TenantSubscription.CreateProTrial(Guid.NewGuid(), started);
        trial.ConvertToPaid(SubscriptionPlanCodes.Pro, started.AddDays(10));

        Assert.Equal(SubscriptionPlanCodes.Pro, trial.PlanCode);
        Assert.Null(trial.ExpiresAtUtc);
        Assert.False(trial.IsTrialActive(started.AddDays(11)));
    }
}
