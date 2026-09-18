using RestaurantOS.Domain;

namespace RestaurantOS.Api.Tests;

public sealed class MenuPackageDomainTests
{
    [Fact]
    public void IsOfferActiveAtRespectsWeekdayAndDailyWindow()
    {
        var package = NewPackage(
            dailyStart: new TimeOnly(11, 30),
            dailyEnd: new TimeOnly(15, 0),
            daysMask: 0b0011_1110); // Mon–Fri (bits 1–5)
        package.ReplaceComponents(
        [
            new MenuPackageComponent(package.Id, Guid.NewGuid(), "Ana", 0),
            new MenuPackageComponent(package.Id, Guid.NewGuid(), "İçecek", 1),
        ]);

        var tz = TimeZoneInfo.FindSystemTimeZoneById(
            OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

        // Wednesday 12:00 Istanbul
        var wednesdayNoon = new DateTimeOffset(2026, 9, 9, 9, 0, 0, TimeSpan.Zero);
        Assert.True(package.IsOfferActiveAt(wednesdayNoon, tz));

        // Wednesday 16:00 Istanbul
        var wednesdayEvening = new DateTimeOffset(2026, 9, 9, 13, 0, 0, TimeSpan.Zero);
        Assert.False(package.IsOfferActiveAt(wednesdayEvening, tz));

        // Saturday 12:00 Istanbul
        var saturdayNoon = new DateTimeOffset(2026, 9, 12, 9, 0, 0, TimeSpan.Zero);
        Assert.False(package.IsOfferActiveAt(saturdayNoon, tz));
    }

    [Fact]
    public void ReplaceComponentsRequiresAtLeastTwoItems()
    {
        var package = NewPackage(null, null, null);
        Assert.Throws<ArgumentException>(() =>
            package.ReplaceComponents(
            [
                new MenuPackageComponent(package.Id, Guid.NewGuid(), null, 0),
            ]));
    }

    private static MenuPackage NewPackage(TimeOnly? dailyStart, TimeOnly? dailyEnd, byte? daysMask) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Öğle A",
            "Tavuk seti",
            Money.Try(28_000),
            isActive: true,
            sortOrder: 0,
            dailyStart,
            dailyEnd,
            daysMask);
}
