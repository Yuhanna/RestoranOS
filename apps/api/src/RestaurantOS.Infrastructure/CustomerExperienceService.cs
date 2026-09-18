using System.Globalization;
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
    IManagementOrderNotifier managementOrderNotifier,
    ICustomerOrderGuard customerOrderGuard,
    IFeatureEntitlementService entitlements) : ICustomerExperienceService
{
    private static readonly TimeSpan DefaultPrepTime = TimeSpan.FromMinutes(18);

    public async Task<CustomerSessionResult> ResolveQrAsync(
        string qrToken,
        string locale,
        CancellationToken cancellationToken,
        CustomerClientContext? client = null)
    {
        if (string.IsNullOrWhiteSpace(qrToken) || qrToken.Length is < 32 or > 512)
        {
            customerOrderGuard.RecordFailedRequest(client);
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
            customerOrderGuard.RecordFailedRequest(client);
            throw new CustomerExperienceException("INVALID_QR", "QR token is invalid.");
        }

        var menu = await dbContext.Menus
            .AsNoTracking()
            .Where(x => x.TenantId == qr.TenantId
                && x.BranchId == qr.BranchId
                && x.PublishedAtUtc != null
                && x.ArchivedAtUtc == null)
            .OrderByDescending(x => x.PublishedAtUtc)
            .Select(x => new { x.Id, x.BranchId })
            .FirstOrDefaultAsync(cancellationToken);

        // Default: use this branch's published menu. If none, fall back to another branch
        // in the same restaurant (shared "main" menu until the branch copies its own).
        if (menu is null)
        {
            var restaurantId = qr.Table.Branch.RestaurantId;
            var siblingBranchIds = await dbContext.Branches.AsNoTracking()
                .Where(branch => branch.TenantId == qr.TenantId && branch.RestaurantId == restaurantId)
                .Select(branch => branch.Id)
                .ToListAsync(cancellationToken);
            menu = await dbContext.Menus
                .AsNoTracking()
                .Where(x => x.TenantId == qr.TenantId
                    && siblingBranchIds.Contains(x.BranchId)
                    && x.PublishedAtUtc != null
                    && x.ArchivedAtUtc == null)
                .OrderByDescending(x => x.PublishedAtUtc)
                .Select(x => new { x.Id, x.BranchId })
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (menu is null)
        {
            throw new CustomerExperienceException("MENU_UNAVAILABLE", "A published menu is not available.");
        }

        var menuBranchId = menu.BranchId;
        var resolvedLocale = SupportedLocales.Normalize(locale);
        var categories = await dbContext.MenuCategories
            .AsNoTracking()
            .Where(x => x.MenuId == menu.Id && x.TenantId == qr.TenantId && x.BranchId == menuBranchId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        var products = await dbContext.MenuItems
            .AsNoTracking()
            .Where(x => x.MenuId == menu.Id && x.TenantId == qr.TenantId && x.BranchId == menuBranchId)
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);
        if (products.Count == 0)
        {
            throw new CustomerExperienceException(
                "MENU_UNAVAILABLE",
                "Published menu has no items. Add products and publish again.");
        }
        var categoryIds = categories.Select(x => x.Id).ToArray();
        var productIds = products.Select(x => x.Id).ToArray();
        var categoryTranslations = await dbContext.MenuCategoryTranslations
            .AsNoTracking()
            .Where(x => x.TenantId == qr.TenantId && x.BranchId == menuBranchId && categoryIds.Contains(x.CategoryId))
            .ToListAsync(cancellationToken);
        var itemTranslations = await dbContext.MenuItemTranslations
            .AsNoTracking()
            .Where(x => x.TenantId == qr.TenantId && x.BranchId == menuBranchId && productIds.Contains(x.ItemId))
            .ToListAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var promotions = await dbContext.MenuPromotions
            .AsNoTracking()
            .Where(x => x.TenantId == qr.TenantId && x.BranchId == qr.BranchId && x.IsActive)
            .ToListAsync(cancellationToken);
        var activePromotions = PromotionPricingService.FilterActivePromotions(promotions, now);

        // Keep sibling phone sessions alive while the table still has open rounds.
        // Otherwise device Y scanning the same QR kills device X's poll/SignalR token.
        var hasOpenRounds = await dbContext.CustomerOrders
            .AsNoTracking()
            .AnyAsync(
                x =>
                    x.TenantId == qr.TenantId
                    && x.BranchId == qr.BranchId
                    && x.TableId == qr.TableId
                    && x.Status != OrderStatus.Completed
                    && x.Status != OrderStatus.Cancelled,
                cancellationToken);
        if (!hasOpenRounds)
        {
            await TableSessionSettlement.SupersedeActiveTableSessionsAsync(
                dbContext,
                qr.TenantId,
                qr.BranchId,
                qr.TableId,
                now,
                cancellationToken);
        }

        var sessionToken = OpaqueToken.Create();
        var tableSessionId = Guid.NewGuid();
        dbContext.CustomerSessions.Add(new CustomerSession(
            tableSessionId,
            qr.TenantId,
            qr.BranchId,
            qr.TableId,
            OpaqueToken.Hash(sessionToken),
            resolvedLocale,
            now,
            now.Add(TableOccupancyPolicy.SessionLifetime)));
        dbContext.GuestSessions.Add(new GuestSession(
            Guid.NewGuid(),
            qr.TenantId,
            qr.BranchId,
            tableSessionId,
            client?.DeviceId,
            HashClientIp(client?.ClientIp),
            resolvedLocale,
            now,
            now));
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
            .OrderBy(x => x.CreatedAtUtc)
            .Take(20)
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
                var promotion = PromotionPricingService.ResolveBestPromotion(
                    activePromotions,
                    item.Id,
                    item.CategoryId,
                    now);
                var pricing = PromotionPricingService.PriceMenuItem(item, promotion);
                return new MenuItemResult(
                    item.Id,
                    item.CategoryId,
                    translation?.Name ?? item.Name,
                    translation?.Description ?? item.Description,
                    pricing.FinalAmountMinor,
                    pricing.Currency,
                    item.IsAvailable,
                    item.ImageUrl,
                    string.IsNullOrWhiteSpace(item.ImageAlt) ? (translation?.Name ?? item.Name) : item.ImageAlt,
                    pricing.ListAmountMinor,
                    pricing.DiscountAmountMinor,
                    promotion?.Name,
                    MenuCatalogJson.ParseItem(item.CatalogJson),
                    promotion?.DiscountKind,
                    promotion?.DiscountValue,
                    promotion?.EffectiveEndsAtUtc(now, PromotionPricingService.DefaultBranchTimeZone));
            }).ToArray(),
            openTypes.Select(ServiceRequest.ToApiCode).ToArray(),
            activeOrders.Select(MapOrder).ToArray(),
            await ResolveCustomerMenuSettingsAsync(
                qr.TenantId,
                qr.Table.Branch.CustomerMenuSettingsJson,
                cancellationToken),
            await LoadOfferPackagesAsync(qr.TenantId, qr.BranchId, now, cancellationToken));
    }

    private async Task<CustomerMenuSettingsData> ResolveCustomerMenuSettingsAsync(
        Guid tenantId,
        string? settingsJson,
        CancellationToken cancellationToken)
    {
        var settings = MenuCatalogJson.ParseSettings(settingsJson);
        var plan = await entitlements.GetEntitlementsAsync(tenantId, cancellationToken);
        var themeId = CustomerMenuThemes.ResolveEffective(settings.ThemeId, plan.CanUseMenuThemes);
        var showWatermark = settings.ShowBrandWatermark && plan.CanUseBrandWatermark;
        if (themeId == settings.ThemeId && showWatermark == settings.ShowBrandWatermark)
        {
            return settings;
        }

        return settings with { ThemeId = themeId, ShowBrandWatermark = showWatermark };
    }

    private async Task<IReadOnlyList<CustomerMenuPackageResult>> LoadOfferPackagesAsync(
        Guid tenantId,
        Guid branchId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        List<MenuPackage> packages;
        try
        {
            packages = await dbContext.MenuPackages
                .AsNoTracking()
                .Include(x => x.Components)
                .ThenInclude(x => x.MenuItem)
                .Where(x => x.TenantId == tenantId && x.BranchId == branchId && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Offer packages are additive. A schema mismatch must not block table QR resolve.
            return [];
        }

        var tz = PromotionPricingService.DefaultBranchTimeZone;
        return packages
            .Where(package => package.IsOfferActiveAt(now, tz))
            .Where(package => package.Components.All(c =>
                c.MenuItem is not null && c.MenuItem.IsAvailable))
            .Select(package =>
            {
                var components = package.Components
                    .OrderBy(x => x.SortOrder)
                    .Select(x => new CustomerMenuPackageComponentResult(
                        x.MenuItemId,
                        x.MenuItem!.Name,
                        x.SlotLabel,
                        x.MenuItem.PriceAmountMinor,
                        x.MenuItem.ImageUrl ?? string.Empty,
                        string.IsNullOrWhiteSpace(x.MenuItem.ImageAlt)
                            ? x.MenuItem.Name
                            : x.MenuItem.ImageAlt,
                        x.MenuItem.Description ?? string.Empty))
                    .ToArray();
                var listTotal = components.Sum(x => x.ListAmountMinor);
                var packagePrice = package.PriceAmountMinor;
                var discount = Math.Max(0, listTotal - packagePrice);
                return new CustomerMenuPackageResult(
                    package.Id,
                    package.Name,
                    package.Description ?? string.Empty,
                    packagePrice,
                    package.PriceCurrency,
                    listTotal,
                    discount,
                    components,
                    package.DailyStartLocal?.ToString("HH\\:mm", CultureInfo.InvariantCulture),
                    package.DailyEndLocal?.ToString("HH\\:mm", CultureInfo.InvariantCulture));
            })
            .ToArray();
    }

    public async Task<CustomerOrderResult> CreateOrderAsync(
        string sessionToken,
        string idempotencyKey,
        IReadOnlyCollection<CreateOrderLine> lines,
        CancellationToken cancellationToken,
        CustomerClientContext? client = null)
    {
        if (string.IsNullOrWhiteSpace(sessionToken) || sessionToken.Length is < 32 or > 512)
        {
            customerOrderGuard.RecordFailedRequest(client);
            throw new CustomerExperienceException("INVALID_SESSION", "Customer session is invalid.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length is < 8 or > 128)
        {
            throw new CustomerExperienceException("INVALID_IDEMPOTENCY_KEY", "A valid idempotency key is required.");
        }

        if (lines.Count is < 1 or > 50
            || lines.Any(x =>
                x.Quantity is < 1 or > 50
                || x.Note?.Length > 160
                || (x.PackageId is null) == (x.ProductId == Guid.Empty)))
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
            customerOrderGuard.RecordFailedRequest(client);
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

        customerOrderGuard.EnsureCanPlaceOrder(session.Id, client);

        var restaurantId = await dbContext.Branches.AsNoTracking()
            .Where(branch => branch.Id == session.BranchId && branch.TenantId == session.TenantId)
            .Select(branch => branch.RestaurantId)
            .SingleAsync(cancellationToken);
        var restaurantBranchIds = await dbContext.Branches.AsNoTracking()
            .Where(branch => branch.TenantId == session.TenantId && branch.RestaurantId == restaurantId)
            .Select(branch => branch.Id)
            .ToListAsync(cancellationToken);

        var productLines = lines.Where(x => x.PackageId is null).ToArray();
        var packageLines = lines.Where(x => x.PackageId is not null).ToArray();

        var productIds = productLines.Select(x => x.ProductId).Distinct().ToArray();
        var products = productIds.Length == 0
            ? new Dictionary<Guid, MenuItem>()
            : await dbContext.MenuItems
                .AsNoTracking()
                .Where(x => productIds.Contains(x.Id)
                    && x.TenantId == session.TenantId
                    && restaurantBranchIds.Contains(x.BranchId)
                    && x.IsAvailable
                    && dbContext.Menus.Any(menu =>
                        menu.Id == x.MenuId
                        && menu.TenantId == session.TenantId
                        && restaurantBranchIds.Contains(menu.BranchId)
                        && menu.PublishedAtUtc != null
                        && menu.ArchivedAtUtc == null))
                .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (products.Count != productIds.Length)
        {
            throw new CustomerExperienceException("ORDER_REJECTED", "One or more products are unavailable.");
        }

        var packageIds = packageLines.Select(x => x.PackageId!.Value).Distinct().ToArray();
        var packages = packageIds.Length == 0
            ? new List<MenuPackage>()
            : await dbContext.MenuPackages
                .AsNoTracking()
                .Include(x => x.Components)
                .ThenInclude(x => x.MenuItem)
                .Where(x => packageIds.Contains(x.Id)
                    && x.TenantId == session.TenantId
                    && x.BranchId == session.BranchId)
                .ToListAsync(cancellationToken);
        if (packages.Count != packageIds.Length)
        {
            throw new CustomerExperienceException("ORDER_REJECTED", "One or more lunch packages are unavailable.");
        }

        var tz = PromotionPricingService.DefaultBranchTimeZone;
        foreach (var package in packages)
        {
            if (!package.IsOfferActiveAt(now, tz)
                || package.Components.Count < 2
                || package.Components.Any(c => c.MenuItem is null || !c.MenuItem.IsAvailable))
            {
                throw new CustomerExperienceException("ORDER_REJECTED", "One or more lunch packages are unavailable.");
            }
        }

        var packageById = packages.ToDictionary(x => x.Id);

        var promotions = await dbContext.MenuPromotions
            .AsNoTracking()
            .Where(x => x.TenantId == session.TenantId && x.BranchId == session.BranchId && x.IsActive)
            .ToListAsync(cancellationToken);
        var activePromotions = PromotionPricingService.FilterActivePromotions(promotions, now);

        var orderId = Guid.NewGuid();
        long subtotalMinor = 0;
        long discountMinor = 0;
        var orderLines = new List<CustomerOrderItem>();
        var prepCandidates = new List<int>();

        foreach (var line in productLines)
        {
            var product = products[line.ProductId];
            var promotion = PromotionPricingService.ResolveBestPromotion(
                activePromotions,
                product.Id,
                product.CategoryId,
                now);
            var pricing = PromotionPricingService.PriceMenuItem(product, promotion);
            subtotalMinor = checked(subtotalMinor + pricing.ListAmountMinor * line.Quantity);
            discountMinor = checked(discountMinor + pricing.DiscountAmountMinor * line.Quantity);
            orderLines.Add(new CustomerOrderItem(
                Guid.NewGuid(),
                orderId,
                product.Id,
                product.Name,
                Money.Try(pricing.ListAmountMinor),
                Money.Try(pricing.DiscountAmountMinor),
                Money.Try(pricing.FinalAmountMinor),
                line.Quantity,
                line.Note));
            prepCandidates.Add(product.PrepTimeSeconds is > 0
                ? product.PrepTimeSeconds.Value
                : (int)DefaultPrepTime.TotalSeconds);
        }

        foreach (var line in packageLines)
        {
            var package = packageById[line.PackageId!.Value];
            var components = package.Components.OrderBy(x => x.SortOrder).ToArray();
            var listPrices = components.Select(c => Math.Max(0, c.MenuItem!.PriceAmountMinor)).ToArray();
            var listTotal = listPrices.Sum();
            var packagePrice = package.PriceAmountMinor;
            var packageDiscount = Math.Max(0, listTotal - packagePrice);
            var allocatedFinals = AllocateProportionally(packagePrice, listPrices);
            var allocatedDiscounts = AllocateProportionally(packageDiscount, listPrices);

            subtotalMinor = checked(subtotalMinor + listTotal * line.Quantity);
            discountMinor = checked(discountMinor + packageDiscount * line.Quantity);

            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                var item = component.MenuItem!;
                var note = string.IsNullOrWhiteSpace(line.Note)
                    ? $"Paket: {package.Name}"
                    : $"{line.Note.Trim()} · Paket: {package.Name}";
                orderLines.Add(new CustomerOrderItem(
                    Guid.NewGuid(),
                    orderId,
                    item.Id,
                    item.Name,
                    Money.Try(listPrices[i]),
                    Money.Try(allocatedDiscounts[i]),
                    Money.Try(allocatedFinals[i]),
                    line.Quantity,
                    note.Length > 160 ? note[..160] : note,
                    package.Id,
                    package.Name));
                prepCandidates.Add(item.PrepTimeSeconds is > 0
                    ? item.PrepTimeSeconds.Value
                    : (int)DefaultPrepTime.TotalSeconds);
            }
        }

        var totalMinor = checked(subtotalMinor - discountMinor);
        var prepSeconds = prepCandidates.DefaultIfEmpty((int)DefaultPrepTime.TotalSeconds).Max();
        var order = new CustomerOrder(
            orderId,
            session.TenantId,
            session.BranchId,
            session.TableId,
            session.Id,
            idempotencyKey,
            requestHash,
            $"#{now:MMddHHmm}-{orderId.ToString("N")[..4].ToUpperInvariant()}",
            Money.Try(subtotalMinor),
            Money.Try(discountMinor),
            Money.Try(totalMinor),
            now,
            now.AddSeconds(prepSeconds));
        foreach (var orderLine in orderLines)
        {
            order.Items.Add(orderLine);
        }

        var guestSession = await dbContext.GuestSessions
            .Where(entry =>
                entry.TableSessionId == session.Id
                && entry.Status == GuestSessionStatus.Active)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (guestSession is not null)
        {
            guestSession.Touch(now);
            order.AttachGuestSession(guestSession.Id);
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

        customerOrderGuard.RecordSuccessfulOrder(session.Id, client);

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

        var guestSession = await dbContext.GuestSessions
            .Where(entry =>
                entry.TableSessionId == session.Id
                && entry.Status == GuestSessionStatus.Active)
            .OrderByDescending(entry => entry.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        guestSession?.Touch(now);

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

        if (order.Status == nextStatus)
        {
            return MapOrder(order);
        }

        if (expectedStatusChangedAtUtc is not null
            && !StatusVersionMatches(order.StatusChangedAtUtc, expectedStatusChangedAtUtc.Value))
        {
            if (IsAlreadyAtOrBeyondTarget(order.Status, nextStatus))
            {
                return MapOrder(order);
            }

            throw new CustomerExperienceException(
                "ORDER_CONCURRENCY_CONFLICT",
                "Order status changed concurrently. Reload and retry.");
        }

        DateTimeOffset changedAt;
        try
        {
            changedAt = timeProvider.GetUtcNow();
            order.ChangeStatus(nextStatus, changedAt);
            if (estimatedReadyAtUtc is not null)
            {
                order.SetEstimatedReadyAt(estimatedReadyAtUtc.Value);
            }
            else if (nextStatus is OrderStatus.Ready or OrderStatus.Served or OrderStatus.Completed)
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

        if (nextStatus == OrderStatus.Completed)
        {
            await TableSessionSettlement.CompleteOpenBillRequestsAsync(
                dbContext,
                order.CustomerSessionId,
                changedAt,
                cancellationToken);
            await TableSessionSettlement.ShortenSessionAfterSettlementAsync(
                dbContext,
                order.CustomerSessionId,
                changedAt,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
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
            order.TotalAmountMinor,
            order.TotalCurrency,
            order.CreatedAtUtc,
            order.SubtotalAmountMinor,
            order.DiscountAmountMinor);

    private static bool StatusVersionMatches(DateTimeOffset actual, DateTimeOffset expected)
    {
        actual = actual.ToUniversalTime();
        expected = expected.ToUniversalTime();
        if (actual == expected)
        {
            return true;
        }

        // Hidden MVC fields and JSON round-trips often drop fractional seconds.
        if (Math.Abs((actual - expected).TotalMilliseconds) <= 1000)
        {
            return true;
        }

        return actual.UtcDateTime.Ticks / TimeSpan.TicksPerSecond
            == expected.UtcDateTime.Ticks / TimeSpan.TicksPerSecond;
    }

    private static bool IsAlreadyAtOrBeyondTarget(OrderStatus current, OrderStatus requested)
    {
        if (requested == OrderStatus.Cancelled)
        {
            return false;
        }

        return (int)current >= (int)requested;
    }

    private static string? HashClientIp(string? clientIp) =>
        string.IsNullOrWhiteSpace(clientIp)
            ? null
            : OpaqueToken.Hash(clientIp.Trim().ToLowerInvariant());

    private static string HashOrder(IEnumerable<CreateOrderLine> lines)
    {
        var canonical = string.Join(
            '\n',
            lines.Select(x =>
                x.PackageId is Guid packageId
                    ? $"pkg:{packageId:N}|{x.Quantity}|{x.Note?.Trim() ?? string.Empty}"
                    : $"{x.ProductId:N}|{x.Quantity}|{x.Note?.Trim() ?? string.Empty}"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>Largest-remainder allocation so shares sum exactly to <paramref name="totalMinor"/>.</summary>
    private static long[] AllocateProportionally(long totalMinor, long[] weights)
    {
        if (weights.Length == 0)
        {
            return [];
        }

        var weightSum = weights.Sum();
        if (totalMinor <= 0 || weightSum <= 0)
        {
            return weights.Select(_ => 0L).ToArray();
        }

        var raw = weights.Select(w => (decimal)totalMinor * w / weightSum).ToArray();
        var floors = raw.Select(x => (long)Math.Floor(x)).ToArray();
        var remainder = totalMinor - floors.Sum();
        var order = Enumerable.Range(0, weights.Length)
            .OrderByDescending(i => raw[i] - floors[i])
            .ThenBy(i => i)
            .ToArray();
        for (var i = 0; i < remainder; i++)
        {
            floors[order[i]]++;
        }

        return floors;
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
