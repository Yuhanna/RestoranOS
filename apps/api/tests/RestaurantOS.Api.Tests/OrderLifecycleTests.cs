using RestaurantOS.Domain;

namespace RestaurantOS.Api.Tests;

public sealed class OrderLifecycleTests
{
    private static DateTimeOffset At(int minute) =>
        new(2026, 8, 25, 10, minute, 0, TimeSpan.Zero);

    private static CustomerOrder CreateOrder() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "idempotency-key",
            new string('A', 64),
            "#1001",
            Money.Try(1_000),
            At(0),
            At(18));

    [Fact]
    public void ValidLifecycleReachesServedThenCompleted()
    {
        var order = CreateOrder();

        order.ChangeStatus(OrderStatus.Accepted, At(1));
        order.ChangeStatus(OrderStatus.Preparing, At(2));
        order.ChangeStatus(OrderStatus.Ready, At(15));
        order.ChangeStatus(OrderStatus.Served, At(18));

        Assert.Equal(OrderStatus.Served, order.Status);

        order.ChangeStatus(OrderStatus.Completed, At(20));

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(At(20), order.StatusChangedAtUtc);
    }

    [Fact]
    public void ReadyCanSkipServedStraightToCompletedForCheckClose()
    {
        var order = CreateOrder();
        order.ChangeStatus(OrderStatus.Ready, At(5));
        order.ChangeStatus(OrderStatus.Completed, At(6));

        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public void ForwardSkipIsAllowedAndCountsAsDone()
    {
        var order = CreateOrder();

        order.ChangeStatus(OrderStatus.Ready, At(5));

        Assert.Equal(OrderStatus.Ready, order.Status);
        Assert.Equal(At(5), order.StatusChangedAtUtc);
    }

    [Fact]
    public void BackwardTransitionDoesNotMutateOrder()
    {
        var order = CreateOrder();
        order.ChangeStatus(OrderStatus.Ready, At(5));

        Assert.Throws<InvalidOrderStatusTransitionException>(
            () => order.ChangeStatus(OrderStatus.Preparing, DateTimeOffset.UtcNow));
        Assert.Equal(OrderStatus.Ready, order.Status);
    }

    [Fact]
    public void CancelledOrderIsTerminal()
    {
        var order = CreateOrder();
        order.ChangeStatus(OrderStatus.Cancelled, DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOrderStatusTransitionException>(
            () => order.ChangeStatus(OrderStatus.Accepted, DateTimeOffset.UtcNow));
    }
}
