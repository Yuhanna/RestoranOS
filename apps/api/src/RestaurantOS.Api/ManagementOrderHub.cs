using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;

namespace RestaurantOS.Api;

[Authorize(Policy = ManagementPolicies.OrderView)]
public sealed class ManagementOrderHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (!PermissionAuthorizationHandler.TryGetScope(
                Context.User!,
                out _,
                out var tenantId,
                out var branchId))
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            ManagementOrderGroup.Name(tenantId, branchId));
        await base.OnConnectedAsync();
    }
}

public sealed class SignalRManagementOrderNotifier(
    IHubContext<ManagementOrderHub> hubContext) : IManagementOrderNotifier
{
    public Task NotifyAsync(
        Guid tenantId,
        Guid branchId,
        CustomerOrderResult order,
        CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(ManagementOrderGroup.Name(tenantId, branchId))
            .SendAsync(
                "orderStatusChanged",
                new ManagementOrderResponse(
                    order.Id,
                    order.DisplayNumber,
                    order.Status,
                    order.CreatedAtUtc,
                    order.StatusChangedAtUtc,
                    order.EstimatedReadyAtUtc,
                    order.AmountMinor,
                    order.Currency),
                cancellationToken);

    public Task NotifyServiceRequestAsync(
        Guid tenantId,
        Guid branchId,
        ServiceRequestResult request,
        CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(ManagementOrderGroup.Name(tenantId, branchId))
            .SendAsync(
                "serviceRequestCreated",
                new ManagementServiceRequestResponse(
                    request.Id,
                    request.TableId,
                    request.TableLabel,
                    request.Type,
                    request.Status,
                    request.Note,
                    request.CreatedAtUtc,
                    request.CompletedAtUtc),
                cancellationToken);
}

internal static class ManagementOrderGroup
{
    public static string Name(Guid tenantId, Guid branchId) =>
        $"management-orders:{tenantId:N}:{branchId:N}";
}
