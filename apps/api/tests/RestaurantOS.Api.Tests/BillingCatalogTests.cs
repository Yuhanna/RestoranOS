using RestaurantOS.Domain;

namespace RestaurantOS.Api.Tests;

public sealed class BillingCatalogTests
{
    [Fact]
    public void FreePriceMustBeZero()
    {
        var error = Assert.Throws<ArgumentException>(() => new PlanPrice(
            Guid.NewGuid(),
            SubscriptionPlanCodes.Free,
            BillingIntervals.Month,
            "TRY",
            100,
            true,
            DateTimeOffset.UtcNow,
            Guid.NewGuid()));
        Assert.Contains("Free", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AmountCannotBeMutatedAfterPublish()
    {
        var price = new PlanPrice(
            Guid.NewGuid(),
            SubscriptionPlanCodes.Pro,
            BillingIntervals.Month,
            "try",
            249900,
            true,
            DateTimeOffset.UtcNow,
            Guid.NewGuid());
        price.Publish(DateTimeOffset.UtcNow);
        Assert.Equal(PlanPriceStatuses.Published, price.Status);
        Assert.Equal(249900, price.AmountMinor);
        Assert.Equal("TRY", price.Currency);
    }

    [Fact]
    public void ArchivedPriceCannotBePublished()
    {
        var price = new PlanPrice(
            Guid.NewGuid(),
            SubscriptionPlanCodes.Pro,
            BillingIntervals.Year,
            "TRY",
            2499000,
            true,
            DateTimeOffset.UtcNow,
            Guid.NewGuid());
        price.Archive(DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => price.Publish(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PlatformStaffDefaultsToReadOnlyForUnknownRole()
    {
        var staff = new PlatformStaff(Guid.NewGuid(), "something-else", DateTimeOffset.UtcNow);
        Assert.Equal(PlatformStaffRoles.ReadOnly, staff.RoleCode);
        Assert.False(PlatformStaffRoles.CanPublishCatalog(staff.RoleCode));
        Assert.False(PlatformStaffRoles.CanWriteCatalog(staff.RoleCode));
    }

    [Fact]
    public void YearlyFreePriceMustAlsoBeZero()
    {
        var error = Assert.Throws<ArgumentException>(() => new PlanPrice(
            Guid.NewGuid(),
            SubscriptionPlanCodes.Free,
            BillingIntervals.Year,
            "TRY",
            1,
            true,
            DateTimeOffset.UtcNow,
            Guid.NewGuid()));
        Assert.Contains("Free", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntervalMustBeMonthOrYear()
    {
        Assert.Throws<ArgumentException>(() => new PlanPrice(
            Guid.NewGuid(),
            SubscriptionPlanCodes.Pro,
            "weekly",
            "TRY",
            100,
            true,
            DateTimeOffset.UtcNow,
            Guid.NewGuid()));
    }

    [Fact]
    public void SupportCannotWriteCatalogButCanManageCampaigns()
    {
        Assert.False(PlatformStaffRoles.CanWriteCatalog(PlatformStaffRoles.Support));
        Assert.False(PlatformStaffRoles.CanPublishCatalog(PlatformStaffRoles.Support));
        Assert.True(PlatformStaffRoles.CanManageCampaigns(PlatformStaffRoles.Support));
        Assert.False(PlatformStaffRoles.CanManageStaff(PlatformStaffRoles.Support));
        Assert.True(PlatformStaffRoles.CanManageStaff(PlatformStaffRoles.Owner));
        Assert.False(PlatformStaffRoles.CanWriteTenants(PlatformStaffRoles.Support));
        Assert.True(PlatformStaffRoles.CanWriteTenants(PlatformStaffRoles.Billing));
        Assert.False(PlatformStaffRoles.CanAcceptQuotes(PlatformStaffRoles.Support));
        Assert.True(PlatformStaffRoles.CanRejectQuotes(PlatformStaffRoles.Support));
    }
}
