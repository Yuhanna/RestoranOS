using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using QRCoder;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class ManagementTableService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<CustomerWebOptions> customerWebOptions,
    IHostEnvironment hostEnvironment,
    IFeatureEntitlementService entitlements) : IManagementTableService
{
    private const string ProtectorPurpose = "RestaurantOS.TableQrToken.v1";

    public async Task<IReadOnlyList<ManagementTableResult>> ListTablesAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.TableView, cancellationToken);
        var tables = await dbContext.DiningTables
            .AsNoTracking()
            .Where(table => table.TenantId == tenantId && table.BranchId == branchId)
            .OrderBy(table => table.Label)
            .Select(table => new
            {
                table.Id,
                table.Label,
                table.IsActive,
                ActiveQrCount = dbContext.TableQrCodes.Count(qr =>
                    qr.TableId == table.Id
                    && qr.TenantId == tenantId
                    && qr.BranchId == branchId
                    && qr.Status == QrCodeStatus.Active),
            })
            .ToListAsync(cancellationToken);
        return await MapTablesWithStatusAsync(
            tenantId,
            branchId,
            tables.Select(table => (table.Id, table.Label, table.IsActive, table.ActiveQrCount)).ToList(),
            cancellationToken);
    }

    public async Task<ManagementTableResult> CreateTableAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string label,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.TableEdit, cancellationToken);
        await entitlements.EnsureCanCreateTableAsync(tenantId, branchId, cancellationToken);
        DiningTable table;
        try
        {
            table = new DiningTable(Guid.NewGuid(), tenantId, branchId, label);
        }
        catch (ArgumentException)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", "A table label is required.");
        }

        var exists = await dbContext.DiningTables.AnyAsync(
            existing => existing.TenantId == tenantId
                && existing.BranchId == branchId
                && existing.Label == table.Label,
            cancellationToken);
        if (exists)
        {
            throw new CustomerExperienceException("TABLE_LABEL_CONFLICT", "A table with this label already exists.");
        }
        dbContext.DiningTables.Add(table);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "TableCreated",
            true,
            timeProvider.GetUtcNow(),
            userId,
            tenantId,
            branchId,
            table.Id,
            table.Label));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new CustomerExperienceException("TABLE_LABEL_CONFLICT", "A table with this label already exists.");
        }

        var created = await MapTablesWithStatusAsync(
            tenantId,
            branchId,
            [(table.Id, table.Label, table.IsActive, 0)],
            cancellationToken);
        return created[0];
    }

    public async Task<ManagementTableResult> UpdateTableAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        string? label,
        bool? isActive,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.TableEdit, cancellationToken);
        var table = await FindTableAsync(tenantId, branchId, tableId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(label))
        {
            try
            {
                table.Rename(label);
            }
            catch (ArgumentException)
            {
                throw new CustomerExperienceException("VALIDATION_ERROR", "A table label is required.");
            }

            var exists = await dbContext.DiningTables.AnyAsync(
                existing => existing.Id != table.Id
                    && existing.TenantId == tenantId
                    && existing.BranchId == branchId
                    && existing.Label == table.Label,
                cancellationToken);
            if (exists)
            {
                throw new CustomerExperienceException("TABLE_LABEL_CONFLICT", "A table with this label already exists.");
            }
        }

        if (isActive is not null)
        {
            table.SetActive(isActive.Value);
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "TableUpdated",
            true,
            timeProvider.GetUtcNow(),
            userId,
            tenantId,
            branchId,
            table.Id,
            table.Label));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new CustomerExperienceException("TABLE_LABEL_CONFLICT", "A table with this label already exists.");
        }

        var activeQrCount = await dbContext.TableQrCodes.CountAsync(
            qr => qr.TableId == table.Id
                && qr.TenantId == tenantId
                && qr.BranchId == branchId
                && qr.Status == QrCodeStatus.Active,
            cancellationToken);
        var updated = await MapTablesWithStatusAsync(
            tenantId,
            branchId,
            [(table.Id, table.Label, table.IsActive, activeQrCount)],
            cancellationToken);
        return updated[0];
    }

    public async Task<IReadOnlyList<ManagementQrCodeResult>> ListQrCodesAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.TableView, cancellationToken);
        await FindTableAsync(tenantId, branchId, tableId, cancellationToken);
        return await dbContext.TableQrCodes
            .AsNoTracking()
            .Where(qr => qr.TenantId == tenantId && qr.BranchId == branchId && qr.TableId == tableId)
            .OrderByDescending(qr => qr.CreatedAtUtc)
            .Select(qr => new ManagementQrCodeResult(
                qr.Id,
                qr.TableId,
                qr.Status.ToString().ToLowerInvariant(),
                qr.CreatedAtUtc,
                qr.RevokedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ManagementGeneratedQrResult> GenerateQrAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.TableEdit, cancellationToken);
        var table = await FindTableAsync(tenantId, branchId, tableId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var token = OpaqueToken.Create();
        var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        var qr = new TableQrCode(
            Guid.NewGuid(),
            tenantId,
            branchId,
            table.Id,
            OpaqueToken.Hash(token),
            now,
            protector.Protect(token));

        var previous = await dbContext.TableQrCodes
            .Where(existing =>
                existing.TenantId == tenantId
                && existing.BranchId == branchId
                && existing.TableId == table.Id
                && existing.Status == QrCodeStatus.Active)
            .ToListAsync(cancellationToken);
        foreach (var existing in previous)
        {
            existing.Deactivate();
        }

        dbContext.TableQrCodes.Add(qr);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "QrGenerated",
            true,
            now,
            userId,
            tenantId,
            branchId,
            qr.Id,
            table.Label));
        await dbContext.SaveChangesAsync(cancellationToken);

        var entryUrl = BuildEntryUrl(token);
        return new ManagementGeneratedQrResult(
            qr.Id,
            qr.TableId,
            table.Label,
            qr.Status.ToString().ToLowerInvariant(),
            token,
            entryUrl,
            CreateSvg(entryUrl),
            qr.CreatedAtUtc);
    }

    public async Task<ManagementQrCodeResult> ChangeQrStatusAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid qrCodeId,
        QrCodeStatus nextStatus,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.TableEdit, cancellationToken);
        var qr = await dbContext.TableQrCodes.SingleOrDefaultAsync(
            existing => existing.Id == qrCodeId && existing.TenantId == tenantId && existing.BranchId == branchId,
            cancellationToken)
            ?? throw new CustomerExperienceException("QR_NOT_FOUND", "QR code was not found.");

        try
        {
            switch (nextStatus)
            {
                case QrCodeStatus.Active:
                    var siblings = await dbContext.TableQrCodes
                        .Where(existing =>
                            existing.Id != qr.Id
                            && existing.TenantId == tenantId
                            && existing.BranchId == branchId
                            && existing.TableId == qr.TableId
                            && existing.Status == QrCodeStatus.Active)
                        .ToListAsync(cancellationToken);
                    foreach (var sibling in siblings)
                    {
                        sibling.Deactivate();
                    }

                    qr.Activate();
                    break;
                case QrCodeStatus.Inactive:
                    qr.Deactivate();
                    break;
                case QrCodeStatus.Revoked:
                    qr.Revoke(timeProvider.GetUtcNow());
                    break;
                default:
                    throw new CustomerExperienceException("INVALID_QR_TRANSITION", "QR status is not supported.");
            }
        }
        catch (InvalidQrCodeTransitionException exception)
        {
            throw new CustomerExperienceException("INVALID_QR_TRANSITION", exception.Message);
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "QrStatusChange",
            true,
            timeProvider.GetUtcNow(),
            userId,
            tenantId,
            branchId,
            qr.Id,
            nextStatus.ToString().ToLowerInvariant()));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ManagementQrCodeResult(
            qr.Id,
            qr.TableId,
            qr.Status.ToString().ToLowerInvariant(),
            qr.CreatedAtUtc,
            qr.RevokedAtUtc);
    }

    public async Task<ManagementQrPrintResult> GetPrintPayloadAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid qrCodeId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.TableView, cancellationToken);
        var qr = await dbContext.TableQrCodes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                existing => existing.Id == qrCodeId && existing.TenantId == tenantId && existing.BranchId == branchId,
                cancellationToken)
            ?? throw new CustomerExperienceException("QR_NOT_FOUND", "QR code was not found.");

        if (qr.Status == QrCodeStatus.Revoked)
        {
            throw new CustomerExperienceException("QR_REVOKED", "A revoked QR code cannot be printed.");
        }

        if (string.IsNullOrWhiteSpace(qr.ProtectedToken))
        {
            throw new CustomerExperienceException(
                "QR_TOKEN_UNAVAILABLE",
                "This QR cannot be reprinted. Generate a new QR code.");
        }

        string token;
        try
        {
            token = dataProtectionProvider.CreateProtector(ProtectorPurpose).Unprotect(qr.ProtectedToken);
        }
        catch (CryptographicException)
        {
            throw new CustomerExperienceException(
                "QR_TOKEN_UNAVAILABLE",
                "This QR cannot be reprinted. Generate a new QR code.");
        }

        var entryUrl = BuildEntryUrl(token);
        var table = await dbContext.DiningTables
            .AsNoTracking()
            .SingleAsync(
                existing => existing.Id == qr.TableId && existing.TenantId == tenantId && existing.BranchId == branchId,
                cancellationToken);
        return new ManagementQrPrintResult(
            qr.Id,
            qr.TableId,
            table.Label,
            qr.Status.ToString().ToLowerInvariant(),
            entryUrl,
            CreateSvg(entryUrl));
    }

    public async Task<ManagementTableResult> ReleaseTableAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.OrderModify, cancellationToken);
        var table = await FindTableAsync(tenantId, branchId, tableId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        await TableSessionSettlement.ReleaseTableOperationalStateAsync(
            dbContext,
            tenantId,
            branchId,
            tableId,
            now,
            cancellationToken);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "TableReleased",
            true,
            now,
            userId,
            tenantId,
            branchId,
            table.Id,
            table.Label));
        await dbContext.SaveChangesAsync(cancellationToken);

        var refreshed = await MapTablesWithStatusAsync(
            tenantId,
            branchId,
            [(table.Id, table.Label, table.IsActive, await dbContext.TableQrCodes.CountAsync(
                qr => qr.TableId == table.Id
                    && qr.TenantId == tenantId
                    && qr.BranchId == branchId
                    && qr.Status == QrCodeStatus.Active,
                cancellationToken))],
            cancellationToken);
        return refreshed[0];
    }

    public async Task<ManagementTableCheckResult> GetTableCheckAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.OrderView, cancellationToken);
        _ = await FindTableAsync(tenantId, branchId, tableId, cancellationToken);
        var open = await LoadOpenRoundsAsync(tenantId, branchId, tableId, cancellationToken);
        return new ManagementTableCheckResult(
            open.Count,
            open.Sum(order => order.TotalAmountMinor),
            open.Any(IsIncompleteKitchen));
    }

    public async Task<ManagementTableCheckCloseResult> CloseTableCheckAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        string tender,
        bool confirmIncompleteKitchen,
        string? note,
        CancellationToken cancellationToken)
    {
        await EnsurePermissionAsync(userId, tenantId, branchId, ManagementPermissions.OrderModify, cancellationToken);
        _ = await FindTableAsync(tenantId, branchId, tableId, cancellationToken);
        var normalizedTender = NormalizeTender(tender);
        var open = await LoadOpenRoundsAsync(tenantId, branchId, tableId, cancellationToken);
        if (open.Count == 0)
        {
            throw new CustomerExperienceException(
                "TABLE_CHECK_EMPTY",
                "Bu masada kapatılacak adisyon bulunmuyor.");
        }

        var incomplete = open.Any(IsIncompleteKitchen);
        if (incomplete && !confirmIncompleteKitchen)
        {
            throw new CustomerExperienceException(
                "TABLE_HAS_UNFINISHED_ORDERS",
                "Mutfakta bitmemiş sipariş var. Kapatmak için onay gerekir.");
        }

        var now = timeProvider.GetUtcNow();
        foreach (var order in open)
        {
            order.ChangeStatus(OrderStatus.Completed, now);
        }

        var sessionIds = open.Select(order => order.CustomerSessionId).Distinct().ToArray();
        foreach (var sessionId in sessionIds)
        {
            await TableSessionSettlement.CompleteOpenBillRequestsAsync(dbContext, sessionId, now, cancellationToken);
            await TableSessionSettlement.ShortenSessionAfterSettlementAsync(dbContext, sessionId, now, cancellationToken);
        }

        var total = open.Sum(order => order.TotalAmountMinor);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "TableCheckClose",
            true,
            now,
            userId,
            tenantId,
            branchId,
            tableId,
            $"{normalizedTender}:{open.Count}:{note}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ManagementTableCheckCloseResult(
            open.Count,
            total,
            normalizedTender,
            incomplete && confirmIncompleteKitchen);
    }

    private async Task<List<CustomerOrder>> LoadOpenRoundsAsync(
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken) =>
        await dbContext.CustomerOrders
            .Where(order =>
                order.TenantId == tenantId
                && order.BranchId == branchId
                && order.TableId == tableId
                && order.Status != OrderStatus.Completed
                && order.Status != OrderStatus.Cancelled)
            .ToListAsync(cancellationToken);

    private static bool IsIncompleteKitchen(CustomerOrder order) =>
        order.Status is OrderStatus.Submitted or OrderStatus.Accepted or OrderStatus.Preparing;

    private static string NormalizeTender(string? tender)
    {
        var value = tender?.Trim().ToLowerInvariant() ?? string.Empty;
        return value switch
        {
            "cash" or "nakit" => "cash",
            "card" or "kart" => "card",
            _ => throw new CustomerExperienceException("INVALID_TENDER", "Ödeme türü nakit veya kart olmalıdır."),
        };
    }

    private async Task<IReadOnlyList<ManagementTableResult>> MapTablesWithStatusAsync(
        Guid tenantId,
        Guid branchId,
        List<(Guid Id, string Label, bool IsActive, int ActiveQrCount)> tables,
        CancellationToken cancellationToken)
    {
        if (tables.Count == 0)
        {
            return [];
        }

        var now = timeProvider.GetUtcNow();
        var pendingTableIds = (await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order =>
                order.TenantId == tenantId
                && order.BranchId == branchId
                && order.Status != OrderStatus.Completed
                && order.Status != OrderStatus.Cancelled)
            .Select(order => order.TableId)
            .Distinct()
            .ToListAsync(cancellationToken)).ToHashSet();

        var occupiedTableIds = await BuildOccupiedTableIdsAsync(
            tenantId,
            branchId,
            now,
            cancellationToken);

        return tables
            .Select(table =>
            {
                var operationalStatus = pendingTableIds.Contains(table.Id)
                    ? "has_pending_order"
                    : occupiedTableIds.Contains(table.Id)
                        ? "occupied"
                        : "available";
                return new ManagementTableResult(
                    table.Id,
                    table.Label,
                    table.IsActive,
                    table.ActiveQrCount,
                    operationalStatus);
            })
            .ToArray();
    }

    private async Task<HashSet<Guid>> BuildOccupiedTableIdsAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var activeSessions = await dbContext.CustomerSessions
            .AsNoTracking()
            .Where(session =>
                session.TenantId == tenantId
                && session.BranchId == branchId
                && session.ExpiresAtUtc > now)
            .Select(session => new
            {
                session.Id,
                session.TableId,
                session.CreatedAtUtc,
                session.ExpiresAtUtc,
            })
            .ToListAsync(cancellationToken);

        if (activeSessions.Count == 0)
        {
            return [];
        }

        var sessionIds = activeSessions.Select(session => session.Id).ToArray();

        var guestActivity = await dbContext.GuestSessions
            .AsNoTracking()
            .Where(guest => sessionIds.Contains(guest.TableSessionId))
            .GroupBy(guest => guest.TableSessionId)
            .Select(group => new { SessionId = group.Key, LastActivity = group.Max(x => x.LastActivityAtUtc) })
            .ToDictionaryAsync(x => x.SessionId, x => x.LastActivity, cancellationToken);

        var orderActivity = await dbContext.CustomerOrders
            .AsNoTracking()
            .Where(order => sessionIds.Contains(order.CustomerSessionId))
            .GroupBy(order => order.CustomerSessionId)
            .Select(group => new
            {
                SessionId = group.Key,
                LastActivity = group.Max(x =>
                    x.StatusChangedAtUtc >= x.CreatedAtUtc ? x.StatusChangedAtUtc : x.CreatedAtUtc),
            })
            .ToDictionaryAsync(x => x.SessionId, x => x.LastActivity, cancellationToken);

        var serviceActivity = await dbContext.ServiceRequests
            .AsNoTracking()
            .Where(request => sessionIds.Contains(request.CustomerSessionId))
            .GroupBy(request => request.CustomerSessionId)
            .Select(group => new
            {
                SessionId = group.Key,
                LastActivity = group.Max(x =>
                    x.CompletedAtUtc ?? x.CreatedAtUtc),
            })
            .ToDictionaryAsync(x => x.SessionId, x => x.LastActivity, cancellationToken);

        var billCompletedAt = await dbContext.ServiceRequests
            .AsNoTracking()
            .Where(request =>
                sessionIds.Contains(request.CustomerSessionId)
                && request.Type == ServiceRequestType.Bill
                && request.Status == ServiceRequestStatus.Completed
                && request.CompletedAtUtc != null)
            .GroupBy(request => request.CustomerSessionId)
            .Select(group => new
            {
                SessionId = group.Key,
                CompletedAt = group.Max(x => x.CompletedAtUtc!.Value),
            })
            .ToDictionaryAsync(x => x.SessionId, x => x.CompletedAt, cancellationToken);

        var occupied = new HashSet<Guid>();
        foreach (var session in activeSessions)
        {
            var lastActivity = session.CreatedAtUtc;
            if (guestActivity.TryGetValue(session.Id, out var guestLast))
            {
                lastActivity = Max(lastActivity, guestLast);
            }

            if (orderActivity.TryGetValue(session.Id, out var orderLast))
            {
                lastActivity = Max(lastActivity, orderLast);
            }

            if (serviceActivity.TryGetValue(session.Id, out var serviceLast))
            {
                lastActivity = Max(lastActivity, serviceLast);
            }

            DateTimeOffset? billCompletedUtc = billCompletedAt.TryGetValue(session.Id, out var completed)
                ? completed
                : null;
            if (TableOccupancyPolicy.CountsAsOccupied(
                    now,
                    session.ExpiresAtUtc,
                    lastActivity,
                    billCompletedUtc))
            {
                occupied.Add(session.TableId);
            }
        }

        return occupied;
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) =>
        left >= right ? left : right;

    private async Task<DiningTable> FindTableAsync(
        Guid tenantId,
        Guid branchId,
        Guid tableId,
        CancellationToken cancellationToken) =>
        await dbContext.DiningTables.SingleOrDefaultAsync(
            table => table.Id == tableId && table.TenantId == tenantId && table.BranchId == branchId,
            cancellationToken)
        ?? throw new CustomerExperienceException("TABLE_NOT_FOUND", "Table was not found.");

    private async Task EnsurePermissionAsync(
        Guid userId,
        Guid tenantId,
        Guid branchId,
        string permission,
        CancellationToken cancellationToken)
    {
        var allowed = await dbContext.ManagementMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.UserId == userId
                && membership.TenantId == tenantId
                && membership.IsActive
                && membership.BranchId == branchId
                && dbContext.Branches.Any(branch =>
                    branch.Id == branchId && branch.TenantId == tenantId)
                && dbContext.ManagementRolePermissions.Any(rolePermission =>
                    rolePermission.RoleId == membership.RoleId
                    && rolePermission.Permission == permission),
                cancellationToken);
        if (!allowed)
        {
            throw new ManagementAuthException("FORBIDDEN", "The requested operation is not permitted.");
        }
    }

    private string BuildEntryUrl(string token)
    {
        var baseUrl = CustomerWebBaseUrlResolver.Resolve(customerWebOptions.Value, hostEnvironment);
        return $"{baseUrl}/?qr={Uri.EscapeDataString(token)}";
    }

    private static string CreateSvg(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
        return new SvgQRCode(data).GetGraphic(6);
    }
}
