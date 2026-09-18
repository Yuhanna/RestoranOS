using RestaurantOS.Domain;
using Xunit;

namespace RestaurantOS.Api.Tests;

public sealed class ManagementUserProfileTests
{
    [Fact]
    public void ConstructorStoresOptionalDisplayNameAndPhone()
    {
        var user = new ManagementUser(
            Guid.NewGuid(),
            "ayse@ornek.com",
            "AYSE@ORNEK.COM",
            "pending",
            DateTimeOffset.UtcNow,
            "Ayşe Yılmaz",
            "05551234567");

        Assert.Equal("Ayşe Yılmaz", user.DisplayName);
        Assert.Equal("05551234567", user.Phone);
    }

    [Fact]
    public void UpdateProfileRequiresDisplayNameWhenFlagSet()
    {
        var user = new ManagementUser(
            Guid.NewGuid(),
            "ayse@ornek.com",
            "AYSE@ORNEK.COM",
            "pending",
            DateTimeOffset.UtcNow);

        Assert.Throws<ArgumentException>(() => user.UpdateProfile("  ", null));
        user.UpdateProfile("Ayşe Yılmaz", "0555 111 22 33");
        Assert.Equal("Ayşe Yılmaz", user.DisplayName);
        Assert.Equal("0555 111 22 33", user.Phone);
    }
}
