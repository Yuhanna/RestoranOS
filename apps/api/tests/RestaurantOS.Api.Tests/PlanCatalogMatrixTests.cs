using RestaurantOS.Domain;
using Xunit;

namespace RestaurantOS.Api.Tests;

public sealed class PlanCatalogMatrixTests
{
    [Fact]
    public void FreePlanUsesActiveQrAndSingleLiveSessionGates()
    {
        var free = PlanCatalog.Free;
        Assert.Equal(1, free.MaxBranches);
        Assert.Null(free.MaxTablesPerBranch);
        Assert.Equal(8, free.MaxActiveQrCodes);
        Assert.Equal(2, free.MaxActiveUsers);
        Assert.Equal(1, free.MaxConcurrentLiveSessions);
        Assert.True(free.CanUseLiveOrderPanel);
        Assert.True(free.CanUseProductImages);
        Assert.False(free.CanUsePromotions);
        Assert.True(free.CanUseAnalytics);
        Assert.False(free.CanUseMenuThemes);
        Assert.False(free.CanUseMenuTranslations);
        Assert.False(free.CanManageAdditionalRoles);
        Assert.False(free.CanUseMultiBranch);
        Assert.False(free.HasPrioritySupport);
        Assert.False(free.CanUseBrandWatermark);
    }

    [Fact]
    public void ProAndEnterpriseUnlockGrowthFeatures()
    {
        Assert.Null(PlanCatalog.Pro.MaxActiveQrCodes);
        Assert.Null(PlanCatalog.Pro.MaxConcurrentLiveSessions);
        Assert.True(PlanCatalog.Pro.CanUsePromotions);
        Assert.True(PlanCatalog.Pro.CanUseAnalytics);
        Assert.True(PlanCatalog.Pro.CanUseMenuThemes);
        Assert.False(PlanCatalog.Pro.HasPrioritySupport);

        Assert.Null(PlanCatalog.Enterprise.MaxBranches);
        Assert.Null(PlanCatalog.Enterprise.MaxActiveUsers);
        Assert.True(PlanCatalog.Enterprise.HasPrioritySupport);
        Assert.True(PlanCatalog.Enterprise.CanUsePromotions);
        Assert.True(PlanCatalog.Enterprise.CanUseMenuThemes);
        Assert.True(PlanCatalog.Pro.CanUseBrandWatermark);
        Assert.True(PlanCatalog.Enterprise.CanUseBrandWatermark);
    }

    [Fact]
    public void TenantOverrideCapsEnterpriseAndUnlocksFreeMultiBranch()
    {
        var now = DateTimeOffset.UtcNow;
        var enterprise = new TenantSubscription(Guid.NewGuid(), SubscriptionPlanCodes.Enterprise, now);
        enterprise.SetContractOverrides(12, 40, 24 * 180, 30, "sözleşme", now);
        var effective = PlanCatalog.ResolveEffective(enterprise, now);
        Assert.Equal(12, effective.MaxBranches);
        Assert.Equal(40, effective.MaxActiveUsers);
        Assert.Equal(30, effective.MaxActiveQrCodes);
        Assert.True(effective.CanUseMultiBranch);
        Assert.Equal(24 * 180, OrderHistoryRetention.ResolveMaxHours(enterprise.PlanCode, false, enterprise.OverrideMaxOrderHistoryHours));

        var free = new TenantSubscription(Guid.NewGuid(), SubscriptionPlanCodes.Free, now);
        free.SetContractOverrides(4, null, null, null, null, now);
        var freeEffective = PlanCatalog.ResolveEffective(free, now);
        Assert.Equal(4, freeEffective.MaxBranches);
        Assert.True(freeEffective.CanUseMultiBranch);
    }

    [Fact]
    public void OpenQuoteCannotBeAcceptedTwice()
    {
        var quote = new EnterpriseQuoteRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ada",
            "ada@example.test",
            null,
            3,
            null,
            DateTimeOffset.UtcNow);
        var reviewer = Guid.NewGuid();
        quote.Accept(reviewer, DateTimeOffset.UtcNow, "ok");
        Assert.Equal(EnterpriseQuoteStatuses.Accepted, quote.Status);
        Assert.Throws<InvalidOperationException>(() => quote.Accept(reviewer, DateTimeOffset.UtcNow, "again"));
    }
}
