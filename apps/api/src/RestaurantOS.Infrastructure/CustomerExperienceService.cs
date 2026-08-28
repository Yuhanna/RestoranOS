using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class CustomerExperienceService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider,
    IOrderStatusNotifier orderStatusNotifier,
    IManagementOrderNotifier managementOrderNotifier) : ICustomerExperienceService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(4);
    private static readonly TimeSpan DefaultPrepTime = TimeSpan.FromMinutes(18);

    public async Task<CustomerSessionResult> ResolveQrAsync(
        string qrToken,
        string locale,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(qrToken) || qrToken.Length is < 32 or > 512)
        {
            throw new CustomerExperienceException("INVALID_QR", "QR token is invalid.");
        }

        var qrHash = OpaqueToken.Hash(qrToken);
        var qr = await dbContext.TableQrCodes
            .AsNoTracking()
            .Include(x => x.Table)
            .ThenInclude(x => x.Branch)
            .ThenInclude(x => x.Restaurant)
            .SingleOrDefaultAsync(
                x => x.TokenHash == qrHash
                    && x.Status == QrCodeStatus.Active
                    && x.Table.IsActive
                    && x.TenantId == x.Table.TenantId
                    && x.BranchId == x.Table.BranchId
                    && x.Table.TenantId == x.Table.Branch.TenantId
                    && x.Table.Branch.TenantId == x.Table.Branch.Restaurant.TenantId,
                cancellationToken);

        if (qr is null)
        {
            throw new CustomerExperienceException("INVALID_QR", "QR token is invalid.");
        }

        var menu = await dbContext.Menus
            .AsNoTracking()
            .Where(x => x.TenantId == qr.TenantId
                && x.BranchId == qr.BranchId
                && x.PublishedAtUtc != null
                && x.ArchivedAtUtc == null)
            .OrderByDescending(x => x.PublishedAtUtc)
            .Select(x => new { x.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (menu is null)
        {
            throw new CustomerExperienceException("MENU_UNAVAILABLE", "A published menu is not available.");
        }

        var resolvedLocale = SupportedLocales.Normalize(locale);
        var categories = await dbContext.MenuCategories
            .AsNoTracking()
            .Where(x => x.MenuId == menu.Id && x.TenantId == qr.TenantId && x.BranchId == qr.BranchId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var products = await dbContext.MenuItems
            .AsNoTracking()
            .Where(x => x.MenuId == menu.Id && x.TenantId == qr.TenantId && x.BranchId == qr.BranchId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var categoryIds = categories.Select(x => x.Id).ToArray();
        var productIds = products.Select(x => x.Id).ToArray();
        var categoryTranslations = await dbContext.MenuCategoryTranslations
            .AsNoTracking()
            .Where(x => x.TenantId == qr.TenantId && x.BranchId == qr.BranchId && categoryIds.Contains(x.CategoryId))
            .ToListAsync(cancellationToken);
        var itemTranslations = await dbContext.MenuItemTranslations
            .AsNoTracking()
            .Where(x => x.TenantId == qr.TenantId && x.BranchId == qr.BranchId && productIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);

        var sessionToken = OpaqueToken.Create();
        var now = timeProvider.GetUtcNow();
        dbContext.CustomerSessions.Add(new CustomerSession(
            Guid.NewGuid(),
            qr.TenantId,
            qr.BranchId,
            qr.TableId,
            OpaqueToken.Hash(sessionToken),
            resolvedLocale,
            now,
            now.Add(SessionLifetime)));
        await dbContext.SaveChangesAsync(cancellationToken);

        var openTypes = await dbContext.ServiceRequests
            .AsNoTracking()
            .Where(x =>
                x.TenantId == qr.TenantId
                && x.BranchId == qr.BranchId
                && x.TableId == qr.TableId
                && x.Status == ServiceRequestStatus.Open)
            .Select(x => x.Type)
            .Distinct()
            .ToListAsync(cancellationToken);

        var activeOrders = await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(x =>
                x.TenantId == qr.TenantId
                && x.BranchId == qr.BranchId
                && x.TableId == qr.TableId
                && x.Status != OrderStatus.Completed
                && x.Status != OrderStatus.Cancelled)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new CustomerSessionResult(
            sessionToken,
            qr.Table.Branch.Restaurant.Name,
            qr.Table.Branch.Name,
            qr.Table.Label,
            resolvedLocale,
            categories.Select(category =>
            {
                var translation = categoryTranslations.FirstOrDefault(x => x.CategoryId == category.Id && x.Locale == resolvedLocale)
                    ?? categoryTranslations.FirstOrDefault(x => x.CategoryId == category.Id && x.Locale == SupportedLocales.Turkish);
                return new MenuCategoryResult(category.Id, translation?.Name ?? category.Name);
            }).ToArray(),
            products.Select(item =>
            {
                var translation = itemTranslations.FirstOrDefault(x => x.ItemId == item.Id && x.Locale == resolvedLocale)
                    ?? itemTranslations.FirstOrDefault(x => x.ItemId == item.Id && x.Locale == SupportedLocales.Turkish);
                return new MenuItemResult(
                    item.Id,
                    item.CategoryId,
                    translation?.Name ?? item.Name,
                    translation?.Description ?? item.Description,
                    item.Price.AmountMinor,
                    item.Price.Currency,
                    item.IsAvailable,
                    item.ImageUrl,
                    string.IsNullOrWhiteSpace(item.ImageAlt) ? (translation?.Name ?? item.Name) : item.ImageAlt);
            }).ToArray(),
            openTypes.Select(ServiceRequest.ToApiCode).ToArray(),
            activeOrders.Select(MapOrder).ToArray());
    }

    public async Task<CustomerOrderResult> CreateOrderAsync(
        string sessionToken,
        string idempotencyKey,
        IReadOnlyCollection<CreateOrderLine> lines,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || sessionToken.Length is < 32 or > 512)
        {
            throw new CustomerExperienceException("INVALID_SESSION", "Customer session is invalid.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 8 or > 128)
        {
            throw new CustomerExperienceException("INVALID_IDEMPOTENCY_KEY", "A valid idempotency key is required.");
        }

        if (lines.Count is < 1 or > 50 || lines.Any(x => x.Quantity is < 1 or > 50 || x.Note?.Length > 160))
        {
            throw new CustomerExperienceException("INVALID_ORDER", "Order lines are invalid.");
        }

        var now = timeProvider.GetUtcNow();
        var sessionHash = OpaqueToken.Hash(sessionToken);
        var session = await dbContext.CustomerSessions.SingleOrDefaultAsync(
            x => x.TokenHash == sessionHash && x.ExpiresAtUtc > now,
            cancellationToken);
        if (session is null)
        {
            throw new CustomerExperienceException("INVALID_SESSION", "Customer session is invalid.");
        }

        var requestHash = HashOrder(lines);
        var existing = await dbContext.CustomerOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.CustomerSessionId == session.Id && x.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(existing.RequestHash),
                    Convert.FromHexString(requestHash)))
            {
                throw new CustomerExperienceException(
                    "IDEMPOTENCY_CONFLICT",
                    "The idempotency key was already used for a different request.");
            }

            return MapOrder(existing);
        }

        var productIds = lines.Select(x => x.ProductId).Distinct().ToArray();
        var products = await dbContext.MenuItems
            .AsNoTracking()
            .Where(x => productIds.Contains(x.Id)
                && x.TenantId == session.TenantId
                && x.BranchId == session.BranchId
                && x.IsAvailable
                && dbContext.Menus.Any(menu =>
                    menu.Id == x.MenuId
                    && menu.TenantId == session.TenantId
                    && menu.BranchId == session.BranchId
                    && menu.PublishedAtUtc != null))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != productIds.Length)
        {
            throw new CustomerExperienceException("ORDER_REJECTED", "One or more products are unavailable.");
        }

        var orderId = Guid.NewGuid();
        var totalMinor = lines.Sum(line => checked(products[line.ProductId].Price.AmountMinor * line.Quantity));
        var prepSeconds = lines
            .Select(line => products[line.ProductId].PrepTimeSeconds is > 0
                ? products[line.ProductId].PrepTimeSeconds!.Value
                : (int)DefaultPrepTime.TotalSeconds)
            .DefaultIfEmpty((int)DefaultPrepTime.TotalSeconds)
            .Max();
        var order = new CustomerOrder(
            orderId,
            session.TenantId,
            session.BranchId,
            session.TableId,
            session.Id,
            idempotencyKey,
            requestHash,
            $"#{now:MMddHHmm}-{orderId.ToString("N")[..4].ToUpperInvariant()}",
            Money.Try(totalMinor),
            now,
            now.AddSeconds(prepSeconds));
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            order.Items.Add(new CustomerOrderItem(
                Guid.NewGuid(),
                order.Id,
                product.Id,
                product.Name,
                product.Price,
                line.Quantity,
                line.Note));
        }

        dbContext.CustomerOrders.Add(order);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var concurrentOrder = await dbContext.CustomerOrders
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.CustomerSessionId == session.Id && x.IdempotencyKey == idempotencyKey,
                    cancellationToken);
            if (concurrentOrder is null || concurrentOrder.RequestHash != requestHash)
            {
                throw;
            }

            return MapOrder(concurrentOrder);
        }

        var created = MapOrder(order);
        await managementOrderNotifier.NotifyAsync(
            session.TenantId,
            session.BranchId,
            created,
            cancellationToken);
        return created;
    }

    public async Task<ServiceRequestResult> CreateServiceRequestAsync(
        string sessionToken,
        string type,
        string? note,
        CancellationToken cancellationToken)
    {
        if (!ServiceRequest.TryParseType(type, out var requestType))
        {
            throw new CustomerExperienceException(
                "INVALID_SERVICE_REQUEST_TYPE",
                "Supported types: waiter, bill, water, cutlery, napkin, other.");
        }

        var session = await FindSessionAsync(sessionToken, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var openExists = await dbContext.ServiceRequests
            .AsNoTracking()
            .AnyAsync(
                x => x.TenantId == session.TenantId
                    && x.BranchId == session.BranchId
                    && x.TableId == session.TableId
                    && x.Type == requestType
                    && x.Status == ServiceRequestStatus.Open,
                cancellationToken);
        if (openExists)
        {
            throw new CustomerExperienceException(
                "SERVICE_REQUEST_OPEN",
                "This request is already open for the table. Staff will handle it shortly.");
        }

        var table = await dbContext.DiningTables
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == session.TableId
                    && x.TenantId == session.TenantId
                    && x.BranchId == session.BranchId,
                cancellationToken)
            ?? throw new CustomerExperienceException("INVALID_SESSION", "Customer session is invalid.");

        ServiceRequest entity;
        try
        {
            entity = new ServiceRequest(
                Guid.NewGuid(),
                session.TenantId,
                session.BranchId,
                session.TableId,
                session.Id,
                requestType,
                now,
                note);
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "Service request note is invalid.");
        }

        dbContext.ServiceRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        var result = new ServiceRequestResult(
            entity.Id,
            entity.TableId,
            table.Label,
            ServiceRequest.ToApiCode(entity.Type),
            ServiceRequest.ToApiStatus(entity.Status),
            entity.Note,
            entity.CreatedAtUtc,
            entity.CompletedAtUtc);
        await managementOrderNotifier.NotifyServiceRequestAsync(
            session.TenantId,
            session.BranchId,
            result,
            cancellationToken);
        return result;
    }

    public async Task<CustomerOrderResult> GetOrderAsync(
        string sessionToken,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var session = await FindSessionAsync(sessionToken, cancellationToken);
        var order = await dbContext.CustomerOrders
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == orderId
                    && x.TenantId == session.TenantId
                    && x.BranchId == session.BranchId
                    && x.TableId == session.TableId,
                cancellationToken);
        if (order is null)
        {
            throw new CustomerExperienceException("ORDER_NOT_FOUND", "Order was not found.");
        }

        return MapOrder(order);
    }

    public async Task<bool> CanAccessOrderAsync(
        string sessionToken,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        CustomerSession session;
        try
        {
            session = await FindSessionAsync(sessionToken, cancellationToken);
        }
        catch (CustomerExperienceException)
        {
            return false;
        }

        return await dbContext.CustomerOrders
            .AsNoTracking()
            .AnyAsync(
                x => x.Id == orderId
                    && x.TenantId == session.TenantId
                    && x.BranchId == session.BranchId
                    && x.TableId == session.TableId,
                cancellationToken);
    }

    public async Task<CustomerOrderResult> ChangeOrderStatusAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        string status,
        CancellationToken cancellationToken) =>
        await ChangeOrderStatusCoreAsync(
            tenantId,
            branchId,
            orderId,
            status,
            null,
            null,
            null,
            cancellationToken);

    public async Task<CustomerOrderResult> ChangeOrderStatusAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        string status,
        Guid actorUserId,
        CancellationToken cancellationToken) =>
        await ChangeOrderStatusCoreAsync(
            tenantId,
            branchId,
            orderId,
            status,
            null,
            actorUserId,
            null,
            cancellationToken);

    public async Task<CustomerOrderResult> ChangeOrderStatusAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        string status,
        DateTimeOffset expectedStatusChangedAtUtc,
        Guid actorUserId,
        CancellationToken cancellationToken,
        DateTimeOffset? estimatedReadyAtUtc = null) =>
        await ChangeOrderStatusCoreAsync(
            tenantId,
            branchId,
            orderId,
            status,
            expectedStatusChangedAtUtc,
            actorUserId,
            estimatedReadyAtUtc,
            cancellationToken);

    private async Task<CustomerOrderResult> ChangeOrderStatusCoreAsync(
        Guid tenantId,
        Guid branchId,
        Guid orderId,
        string status,
        DateTimeOffset? expectedStatusChangedAtUtc,
        Guid? actorUserId,
        DateTimeOffset? estimatedReadyAtUtc,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrderStatus>(status, true, out var nextStatus))
        {
            throw new CustomerExperienceException("INVALID_ORDER_STATUS", "Order status is invalid.");
        }

        var order = await dbContext.CustomerOrders.SingleOrDefaultAsync(
            x => x.Id == orderId && x.TenantId == tenantId && x.BranchId == branchId,
            cancellationToken);
        if (order is null)
        {
            throw new CustomerExperienceException("ORDER_NOT_FOUND", "Order was not found.");
        }

        if (expectedStatusChangedAtUtc is not null
            && order.StatusChangedAtUtc != expectedStatusChangedAtUtc.Value.ToUniversalTime())
        {
            throw new CustomerExperienceException(
                "ORDER_CONCURRENCY_CONFLICT",
                "Order status changed concurrently. Reload and retry.");
        }

        try
        {
            var changedAt = timeProvider.GetUtcNow();
            order.ChangeStatus(nextStatus, changedAt);
            if (estimatedReadyAtUtc is not null)
            {
                order.SetEstimatedReadyAt(estimatedReadyAtUtc.Value);
            }
            else if (nextStatus is OrderStatus.Ready or OrderStatus.Completed)
            {
                order.SetEstimatedReadyAt(changedAt);
            }

            if (actorUserId is not null)
            {
                dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
                    Guid.NewGuid(),
                    "OrderStatusChange",
                    true,
                    changedAt,
                    actorUserId,
                    tenantId,
                    branchId,
                    orderId,
                    nextStatus.ToString().ToLowerInvariant()));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (InvalidOrderStatusTransitionException exception)
        {
            throw new CustomerExperienceException("INVALID_STATUS_TRANSITION", exception.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new CustomerExperienceException(
                "ORDER_CONCURRENCY_CONFLICT",
                "Order status changed concurrently. Reload and retry.");
        }

        var result = MapOrder(order);
        await orderStatusNotifier.NotifyAsync(result, cancellationToken);
        return result;
    }

    private async Task<CustomerSession> FindSessionAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || sessionToken.Length is < 32 or > 512)
        {
            throw new CustomerExperienceException("INVALID_SESSION", "Customer session is invalid.");
        }

        var now = timeProvider.GetUtcNow();
        var sessionHash = OpaqueToken.Hash(sessionToken);
        return await dbContext.CustomerSessions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    x => x.TokenHash == sessionHash && x.ExpiresAtUtc > now,
                    cancellationToken)
            ?? throw new CustomerExperienceException("INVALID_SESSION", "Customer session is invalid.");
    }

    private static CustomerOrderResult MapOrder(CustomerOrder order) =>
        new(
            order.Id,
            order.DisplayNumber,
            order.Status.ToString().ToLowerInvariant(),
            order.StatusChangedAtUtc,
            order.EstimatedReadyAtUtc,
            order.Total.AmountMinor,
            order.Total.Currency,
            order.CreatedAtUtc);

    private static string HashOrder(IEnumerable<CreateOrderLine> lines)
    {
        var canonical = string.Join(
            '\n',
            lines.Select(x => $"{x.ProductId:N}|{x.Quantity}|{x.Note?.Trim() ?? string.Empty}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}

public static class OpaqueToken
{
    public static string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
