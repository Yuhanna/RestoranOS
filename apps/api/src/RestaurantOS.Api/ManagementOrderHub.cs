using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RestaurantOS.Api.Models.Dto;
using RestaurantOS.Application;
using RestaurantOS.Infrastructure;

namespace RestaurantOS.Api;

[Authorize(Policy = ManagementPolicies.OrderView)]
public sealed class ManagementOrderHub(
    IFeatureEntitlementService entitlements,
    ILivePanelSessionLeaseService livePanelSessions) : Hub
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

        try
        {
            var plan = await entitlements.EnsureLivePanelSessionAllowedAsync(
                tenantId,
                Context.ConnectionAborted);
            var lease = await livePanelSessions.TryAcquireAsync(
                tenantId,
                branchId,
                Context.ConnectionId,
                plan.MaxConcurrentLiveSessions,
                Context.ConnectionAborted);
            if (!lease.Acquired)
            {
                await Clients.Caller.SendAsync(
                    "livePanelDenied",
                    new
                    {
                        code = "ENTITLEMENT_LIVE_SESSION_LIMIT",
                        message = lease.DenialMessage
                            ?? "Canlı panel oturum limiti doldu. Pro’ya geçerek ekibinizle birlikte takip edebilirsiniz.",
                        activeCount = lease.ActiveCount,
                        maxSessions = lease.MaxSessions,
                    },
                    Context.ConnectionAborted);
                Context.Abort();
                return;
            }
        }
        catch (EntitlementException exception)
        {
            await Clients.Caller.SendAsync(
                "livePanelDenied",
                new { code = exception.Code, message = exception.Message },
                Context.ConnectionAborted);
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            ManagementOrderGroup.Name(tenantId, branchId));
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            ManagementTenantGroup.Name(tenantId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (PermissionAuthorizationHandler.TryGetScope(
                Context.User!,
                out _,
                out var tenantId,
                out var branchId))
        {
            await livePanelSessions.ReleaseAsync(
                tenantId,
                branchId,
                Context.ConnectionId,
                CancellationToken.None);
        }

        await base.OnDisconnectedAsync(exception);
    }
}

public sealed class SignalRManagementOrderNotifier(
    IHubContext<ManagementOrderHub> hubContext,
    IServiceScopeFactory scopeFactory) : IManagementOrderNotifier
{
    public async Task NotifyAsync(
        Guid tenantId,
        Guid branchId,
        CustomerOrderResult order,
        CancellationToken cancellationToken)
    {
        Guid tableId = Guid.Empty;
        string tableLabel = "—";
        string itemSummary = string.Empty;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<RestaurantOsDbContext>();
            var table = await dbContext.CustomerOrders
                .AsNoTracking()
                .Where(entry => entry.Id == order.Id)
                .Select(entry => new
                {
                    entry.TableId,
                    TableLabel = dbContext.DiningTables
                        .Where(table => table.Id == entry.TableId)
                        .Select(table => table.Label)
                        .FirstOrDefault(),
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (table is not null)
            {
                tableId = table.TableId;
                tableLabel = table.TableLabel ?? "—";
            }

            var itemLines = await dbContext.CustomerOrderItems
                .AsNoTracking()
                .Where(item => item.OrderId == order.Id)
                .Select(item => new { item.Name, item.Quantity })
                .ToListAsync(cancellationToken);
            itemSummary = ManagementOrderService.BuildItemSummary(
                itemLines.Select(item => (item.Name, item.Quantity)));
        }

        await hubContext.Clients
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
                    order.Currency,
                    tableId,
                    tableLabel,
                    itemSummary),
                cancellationToken);
    }

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

public sealed class SignalRManagementNotificationNotifier(
    IHubContext<ManagementOrderHub> hubContext) : IManagementNotificationNotifier
{
    public Task NotifyAsync(
        Guid tenantId,
        ManagedNotificationResult notification,
        CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(ManagementTenantGroup.Name(tenantId))
            .SendAsync(
                "audienceNotificationPublished",
                new ManagementAudienceNotificationResponse(
                    notification.Id,
                    notification.Title,
                    notification.Body,
                    notification.ActionUrl),
                cancellationToken);
}

internal static class ManagementOrderGroup
{
    public static string Name(Guid tenantId, Guid branchId) =>
        $"management-orders:{tenantId:N}:{branchId:N}";
}

internal static class ManagementTenantGroup
{
    public static string Name(Guid tenantId) => $"management-tenant:{tenantId:N}";
}
