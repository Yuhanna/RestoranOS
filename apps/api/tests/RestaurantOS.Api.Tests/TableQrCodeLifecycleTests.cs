using RestaurantOS.Domain;

namespace RestaurantOS.Api.Tests;

public sealed class TableQrCodeLifecycleTests
{
    [Fact]
    public void ActivateIsIdempotentFromActiveAndAllowedFromInactive()
    {
        var qr = Create();
        qr.Activate();
        qr.Deactivate();
        qr.Activate();
        Assert.Equal(QrCodeStatus.Active, qr.Status);
    }

    [Fact]
    public void RevokedQrCannotBeActivatedAndClearsProtectedToken()
    {
        var qr = Create("protected-value");
        qr.Revoke(DateTimeOffset.UtcNow);
        Assert.Equal(QrCodeStatus.Revoked, qr.Status);
        Assert.Null(qr.ProtectedToken);
        Assert.Throws<InvalidQrCodeTransitionException>(qr.Activate);
        Assert.Throws<InvalidQrCodeTransitionException>(qr.Deactivate);
    }

    private static TableQrCode Create(string? protectedToken = null) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new string('A', 64),
            DateTimeOffset.UtcNow,
            protectedToken);
}
