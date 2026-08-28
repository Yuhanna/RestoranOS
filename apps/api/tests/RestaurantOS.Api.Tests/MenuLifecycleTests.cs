using RestaurantOS.Domain;

namespace RestaurantOS.Api.Tests;

public sealed class MenuLifecycleTests
{
    [Fact]
    public void DraftPublishesAndUnpublishesUntilArchived()
    {
        var menu = new PublishedMenu(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Akşam");
        Assert.Equal("draft", menu.Lifecycle);
        menu.Publish(DateTimeOffset.UtcNow);
        Assert.Equal("published", menu.Lifecycle);
        menu.Unpublish();
        Assert.Equal("draft", menu.Lifecycle);
        menu.Archive(DateTimeOffset.UtcNow);
        Assert.Equal("archived", menu.Lifecycle);
        Assert.Throws<InvalidMenuStateException>(() => menu.Rename("Yeni"));
        Assert.Throws<InvalidMenuStateException>(() => menu.Publish(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void UnsupportedLocaleFallsBackToTurkish()
    {
        Assert.Equal("tr", SupportedLocales.Normalize("de"));
        Assert.Equal("en", SupportedLocales.Normalize("en-GB"));
        Assert.False(SupportedLocales.IsSupported("de"));
    }
}
