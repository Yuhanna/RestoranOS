using Microsoft.AspNetCore.SignalR;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api;

public sealed class CustomerOrderHub(ICustomerExperienceService service) : Hub
{
    public async Task SubscribeToOrder(Guid orderId, string sessionToken)
    {
        // Do not take CancellationToken as a hub parameter: JS clients only send
        // (orderId, sessionToken). A CT parameter breaks binding on some runtimes.
        if (!await service.CanAccessOrderAsync(sessionToken, orderId, Context.ConnectionAborted))
        {
            throw new HubException("Order subscription is not authorized.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            OrderGroup.Name(orderId),
            Context.ConnectionAborted);
    }
}

public sealed class SignalROrderStatusNotifier(IHubContext<CustomerOrderHub> hubContext)
    : IOrderStatusNotifier
{
    public Task NotifyAsync(CustomerOrderResult order, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(OrderGroup.Name(order.Id))
            .SendAsync(
                "orderStatusChanged",
                new CustomerOrderResponse(
                    order.Id.ToString(),
                    order.DisplayNumber,
                    order.Status,
                    order.StatusChangedAtUtc,
                    order.EstimatedReadyAtUtc,
                    new MoneyResponse(order.AmountMinor, order.Currency)),
                cancellationToken);
}

internal static class OrderGroup
{
    public static string Name(Guid orderId) => $"customer-order:{orderId:N}";
}
