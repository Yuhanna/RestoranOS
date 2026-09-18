using RestaurantOS.Admin.Data;

namespace RestaurantOS.Admin.Tests;

public sealed class AdminAccessTests
{
    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Billing", true)]
    [InlineData("Support", false)]
    [InlineData("ReadOnly", false)]
    [InlineData(null, false)]
    public void CatalogWriteFollowsOwnerAndBilling(string? role, bool expected) =>
        Assert.Equal(expected, AdminAccess.CanWriteCatalog(role));

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Billing", false)]
    [InlineData("Support", false)]
    public void CatalogPublishIsOwnerOnly(string role, bool expected) =>
        Assert.Equal(expected, AdminAccess.CanPublishCatalog(role));

    [Theory]
    [InlineData("Owner", "draft", true)]
    [InlineData("Billing", "draft", true)]
    [InlineData("Support", "draft", false)]
    [InlineData("Owner", "published", true)]
    [InlineData("Billing", "published", false)]
    [InlineData("Owner", "archived", false)]
    public void CatalogArchiveMatchesStatus(string role, string status, bool expected) =>
        Assert.Equal(expected, AdminAccess.CanArchiveCatalog(role, status));

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Billing", true)]
    [InlineData("Support", true)]
    [InlineData("ReadOnly", false)]
    public void CampaignWriteIncludesSupport(string role, bool expected) =>
        Assert.Equal(expected, AdminAccess.CanWriteCampaigns(role));

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Billing", true)]
    [InlineData("Support", true)]
    [InlineData("ReadOnly", false)]
    public void NotificationWriteMatchesCampaigns(string role, bool expected) =>
        Assert.Equal(expected, AdminAccess.CanWriteNotifications(role));

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Billing", false)]
    [InlineData("Support", false)]
    [InlineData("ReadOnly", false)]
    [InlineData(null, false)]
    public void StaffManageIsOwnerOnly(string? role, bool expected) =>
        Assert.Equal(expected, AdminAccess.CanManageStaff(role));

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Billing", true)]
    [InlineData("Support", false)]
    [InlineData("ReadOnly", false)]
    [InlineData(null, false)]
    public void TenantWriteFollowsOwnerAndBilling(string? role, bool expected) =>
        Assert.Equal(expected, AdminAccess.CanWriteTenants(role));

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Billing", true)]
    [InlineData("Support", false)]
    public void QuoteAcceptFollowsCatalogWrite(string role, bool expected) =>
        Assert.Equal(expected, AdminAccess.CanAcceptQuotes(role));

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Billing", true)]
    [InlineData("Support", true)]
    [InlineData("ReadOnly", false)]
    public void QuoteRejectIncludesSupport(string role, bool expected) =>
        Assert.Equal(expected, AdminAccess.CanRejectQuotes(role));

    [Theory]
    [InlineData("Open", "Açık")]
    [InlineData("Accepted", "Onaylandı")]
    [InlineData("Rejected", "Reddedildi")]
    public void QuoteStatusLabelsAreOperatorFacing(string status, string expected) =>
        Assert.Equal(expected, AdminAccess.QuoteStatusLabel(status));

    [Fact]
    public void EmptyOverrideLimitReadsAsCatalogDefault() =>
        Assert.Equal("sınırsız", AdminAccess.LimitLabel(null));

    [Theory]
    [InlineData("non_pro", "Pro olmayanlar")]
    [InlineData("all", "Tümü")]
    [InlineData("pro", "Pro")]
    public void AudienceLabelsAreOperatorFacing(string audience, string expected) =>
        Assert.Equal(expected, AdminAccess.AudienceLabel(audience));
}
