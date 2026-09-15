using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class PlatformCatalogService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider) : IPlatformCatalogService
{
    public async Task<PlatformCatalogSnapshot> GetCatalogAsync(CancellationToken cancellationToken)
    {
        var prices = await dbContext.PlanPrices
            .AsNoTracking()
            .OrderBy(x => x.ProductCode)
            .ThenBy(x => x.Interval)
            .ThenByDescending(x => x.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var grouped = prices.GroupBy(x => x.ProductCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(Map).ToArray(), StringComparer.OrdinalIgnoreCase);

        var products = new List<PlatformCatalogPlanResult>
        {
            ToProduct(SubscriptionPlanCodes.Free, grouped),
            ToProduct(SubscriptionPlanCodes.Pro, grouped),
            ToProduct(SubscriptionPlanCodes.Enterprise, grouped),
            ToProduct(CatalogProductCodes.ExtraBranch, grouped),
        };

        return new PlatformCatalogSnapshot(
            products,
            "Yayınlanan fiyat yeni yükseltmelerde geçerlidir. Mevcut üyelerin plan kodu değişmez; canlı tutar yerinde güncellenmez, yeni sürüm açılır.");
    }

    public async Task<ManagedPlanPriceResult> CreateDraftAsync(
        Guid actorUserId,
        string actorRole,
        CreatePlanPriceCommand command,
        CancellationToken cancellationToken)
    {
        _ = actorRole;
        EnsureCanWrite(await RequireActiveStaffRoleAsync(actorUserId, cancellationToken));
        PlanPrice entity;
        try
        {
            entity = new PlanPrice(
                Guid.NewGuid(),
                command.ProductCode,
                command.Interval,
                command.Currency,
                command.AmountMinor,
                command.TaxInclusive,
                timeProvider.GetUtcNow(),
                actorUserId);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.PlanPrices.Add(entity);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "CatalogPriceDraft",
            true,
            timeProvider.GetUtcNow(),
            actorUserId,
            subjectId: entity.Id,
            detail: $"{entity.ProductCode} {entity.Interval} {entity.AmountMinor}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ManagedPlanPriceResult> PublishAsync(
        Guid actorUserId,
        string actorRole,
        Guid priceId,
        CancellationToken cancellationToken)
    {
        _ = actorRole;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            try
            {
                if (!PlatformStaffRoles.CanPublishCatalog(
                        await RequireActiveStaffRoleAsync(actorUserId, cancellationToken)))
                {
                    throw new CustomerExperienceException(
                        "PLATFORM_ROLE_DENIED",
                        "Fiyat yayınlamak için Owner rolü gerekir.");
                }

                var now = timeProvider.GetUtcNow();
                var entity = await dbContext.PlanPrices.SingleOrDefaultAsync(x => x.Id == priceId, cancellationToken)
                    ?? throw new CustomerExperienceException("PRICE_NOT_FOUND", "Fiyat sürümü bulunamadı.");

                var currentPublished = await dbContext.PlanPrices
                    .Where(x =>
                        x.Id != entity.Id
                        && x.ProductCode == entity.ProductCode
                        && x.Interval == entity.Interval
                        && x.Currency == entity.Currency
                        && x.Status == PlanPriceStatuses.Published)
                    .ToListAsync(cancellationToken);
                foreach (var previous in currentPublished)
                {
                    previous.Archive(now);
                }

                if (currentPublished.Count > 0)
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                try
                {
                    entity.Publish(now);
                }
                catch (InvalidOperationException exception)
                {
                    throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
                }

                dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
                    Guid.NewGuid(),
                    "CatalogPricePublish",
                    true,
                    now,
                    actorUserId,
                    subjectId: entity.Id,
                    detail: $"{entity.ProductCode} {entity.Interval} {entity.AmountMinor}"));
                await dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return Map(entity);
            }
            catch (DbUpdateException)
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw new CustomerExperienceException(
                    "VALIDATION_ERROR",
                    "Bu ürün için aynı anda yalnızca bir yayınlı fiyat olabilir. İşlemi tekrar deneyin.");
            }
            catch
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                throw;
            }
        });
    }

    public async Task<ManagedPlanPriceResult> ArchiveAsync(
        Guid actorUserId,
        string actorRole,
        Guid priceId,
        CancellationToken cancellationToken)
    {
        _ = actorRole;
        EnsureCanWrite(await RequireActiveStaffRoleAsync(actorUserId, cancellationToken));
        var now = timeProvider.GetUtcNow();
        var entity = await dbContext.PlanPrices.SingleOrDefaultAsync(x => x.Id == priceId, cancellationToken)
            ?? throw new CustomerExperienceException("PRICE_NOT_FOUND", "Fiyat sürümü bulunamadı.");
        entity.Archive(now);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "CatalogPriceArchive",
            true,
            now,
            actorUserId,
            subjectId: entity.Id,
            detail: entity.ProductCode));
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<long?> GetPublishedAmountMinorAsync(
        string productCode,
        string interval,
        string currency,
        CancellationToken cancellationToken)
    {
        var code = CatalogProductCodes.Normalize(productCode);
        var normalizedInterval = BillingIntervals.Normalize(interval);
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency)
            ? BranchBillingPolicy.Currency
            : currency.Trim().ToUpperInvariant();
        var match = await dbContext.PlanPrices
            .AsNoTracking()
            .Where(x =>
                x.ProductCode == code
                && x.Interval == normalizedInterval
                && x.Currency == normalizedCurrency
                && x.Status == PlanPriceStatuses.Published)
            .OrderByDescending(x => x.PublishedAtUtc)
            .Select(x => (long?)x.AmountMinor)
            .FirstOrDefaultAsync(cancellationToken);
        return match;
    }

    private async Task<string> RequireActiveStaffRoleAsync(Guid actorUserId, CancellationToken cancellationToken)
    {
        var staff = await dbContext.PlatformStaff
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == actorUserId && x.IsActive, cancellationToken);
        if (staff is null)
        {
            throw new CustomerExperienceException(
                "PLATFORM_ACCESS_DENIED",
                "This account is not authorized for platform management.");
        }

        return staff.RoleCode;
    }

    private static void EnsureCanWrite(string actorRole)
    {
        if (!PlatformStaffRoles.CanWriteCatalog(actorRole))
        {
            throw new CustomerExperienceException(
                "PLATFORM_ROLE_DENIED",
                "Katalog yazmak için Owner veya Billing rolü gerekir.");
        }
    }

    private static PlatformCatalogPlanResult ToProduct(
        string productCode,
        Dictionary<string, ManagedPlanPriceResult[]> grouped)
    {
        grouped.TryGetValue(productCode, out var prices);
        FeatureEntitlements? entitlements = productCode == CatalogProductCodes.ExtraBranch
            ? null
            : PlanCatalog.Resolve(productCode);
        return new PlatformCatalogPlanResult(
            productCode,
            CatalogProductCodes.KindOf(productCode),
            CatalogProductCodes.DisplayName(productCode),
            entitlements,
            prices ?? []);
    }

    private static ManagedPlanPriceResult Map(PlanPrice price) =>
        new(
            price.Id,
            price.ProductCode,
            price.ProductKind,
            CatalogProductCodes.DisplayName(price.ProductCode),
            price.Interval,
            price.Currency,
            price.AmountMinor,
            price.TaxInclusive,
            price.Status,
            price.CreatedAtUtc,
            price.PublishedAtUtc,
            price.ArchivedAtUtc);
}
