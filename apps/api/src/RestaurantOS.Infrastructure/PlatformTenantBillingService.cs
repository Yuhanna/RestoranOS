using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class PlatformTenantBillingService(
    RestaurantOsDbContext dbContext,
    TimeProvider timeProvider,
    IFeatureEntitlementService entitlements) : IPlatformTenantBillingService
{
    public async Task<PlatformTenantListResult> ListTenantsAsync(
        string? query,
        string? planCode,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        skip = Math.Max(0, skip);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 100);
        var now = timeProvider.GetUtcNow();
        var tenantsQuery = dbContext.Tenants.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            tenantsQuery = tenantsQuery.Where(tenant =>
                tenant.Name.Contains(term)
                || dbContext.Restaurants.Any(restaurant =>
                    restaurant.TenantId == tenant.Id && restaurant.Name.Contains(term)));
        }

        var tenantIds = await tenantsQuery.Select(x => x.Id).ToListAsync(cancellationToken);
        var subscriptions = await dbContext.TenantSubscriptions
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId))
            .ToListAsync(cancellationToken);
        var subscriptionByTenant = subscriptions.ToDictionary(x => x.TenantId);
        if (!string.IsNullOrWhiteSpace(planCode))
        {
            var normalized = PlanCatalog.Normalize(planCode);
            tenantIds = tenantIds
                .Where(id => PlanCatalog.Normalize(
                    subscriptionByTenant.TryGetValue(id, out var row) ? row.PlanCode : SubscriptionPlanCodes.Free)
                    == normalized)
                .ToList();
        }

        var total = tenantIds.Count;
        var pageIds = tenantIds.Skip(skip).Take(take).ToArray();
        var pageTenants = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => pageIds.Contains(x.Id))
            .ToListAsync(cancellationToken);
        var restaurants = await dbContext.Restaurants
            .AsNoTracking()
            .Where(x => pageIds.Contains(x.TenantId))
            .Select(x => new { x.TenantId, x.Name })
            .ToListAsync(cancellationToken);
        var restaurantName = restaurants
            .GroupBy(x => x.TenantId)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Name).First().Name);
        var branchCounts = await dbContext.Branches
            .AsNoTracking()
            .Where(x => pageIds.Contains(x.TenantId) && !x.IsFrozen)
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var branchByTenant = branchCounts.ToDictionary(x => x.TenantId, x => x.Count);
        var openQuotes = await dbContext.EnterpriseQuoteRequests
            .AsNoTracking()
            .Where(x => pageIds.Contains(x.TenantId) && x.Status == EnterpriseQuoteStatuses.Open)
            .GroupBy(x => x.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var quotesByTenant = openQuotes.ToDictionary(x => x.TenantId, x => x.Count);

        var items = pageTenants
            .OrderBy(x => x.Name)
            .Select(tenant =>
            {
                var subscription = subscriptionByTenant.GetValueOrDefault(tenant.Id);
                var effective = subscription is null
                    ? PlanCatalog.Free
                    : PlanCatalog.ResolveEffective(subscription, now);
                var isTrial = subscription?.IsTrialActive(now) == true;
                return new PlatformTenantListItemResult(
                    tenant.Id,
                    tenant.Name,
                    restaurantName.GetValueOrDefault(tenant.Id, tenant.Name),
                    effective.PlanCode,
                    isTrial,
                    subscription?.ExpiresAtUtc,
                    branchByTenant.GetValueOrDefault(tenant.Id),
                    effective.MaxBranches,
                    quotesByTenant.GetValueOrDefault(tenant.Id));
            })
            .ToArray();

        return new PlatformTenantListResult(items, total, skip, take);
    }

    public async Task<PlatformTenantDetailResult> GetTenantAsync(
        Guid actorUserId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var detail = await LoadDetailAsync(tenantId, cancellationToken);
        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "PlatformTenantViewed",
            true,
            timeProvider.GetUtcNow(),
            actorUserId,
            tenantId,
            detail: detail.RestaurantName));
        await dbContext.SaveChangesAsync(cancellationToken);
        return detail;
    }

    public async Task<PlatformTenantDetailResult> UpdateSubscriptionAsync(
        Guid actorUserId,
        string actorRole,
        Guid tenantId,
        UpdatePlatformTenantSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        if (!PlatformStaffRoles.CanWriteTenants(actorRole))
        {
            throw new CustomerExperienceException(
                "PLATFORM_ROLE_DENIED",
                "Üye aboneliğini yalnızca Owner veya Billing değiştirir.");
        }

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new CustomerExperienceException("TENANT_NOT_FOUND", "Tenant bulunamadı.");
        var subscription = await GetOrCreateSubscriptionAsync(tenantId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var previous = $"{subscription.PlanCode}/{subscription.OverrideMaxBranches}";
        try
        {
            subscription.ChangePlan(command.PlanCode, now, command.ExpiresAtUtc);
            subscription.SetPurchasedBranchAddonCount(command.PurchasedBranchAddonCount, now);
            subscription.SetContractOverrides(
                command.OverrideMaxBranches,
                command.OverrideMaxActiveUsers,
                command.OverrideMaxOrderHistoryHours,
                command.OverrideMaxActiveQrCodes,
                command.ContractNote,
                now);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "PlatformTenantSubscriptionUpdated",
            true,
            now,
            actorUserId,
            tenant.Id,
            detail: $"{previous}->{subscription.PlanCode}/{subscription.OverrideMaxBranches}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await LoadDetailAsync(tenantId, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformQuoteListItemResult>> ListQuotesAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.EnterpriseQuoteRequests.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = EnterpriseQuoteStatuses.Normalize(status);
            query = query.Where(x => x.Status == normalized);
        }

        var quotes = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
        var tenantIds = quotes.Select(x => x.TenantId).Distinct().ToArray();
        var names = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        return quotes
            .Select(quote => new PlatformQuoteListItemResult(
                quote.Id,
                quote.TenantId,
                names.GetValueOrDefault(quote.TenantId, quote.TenantId.ToString()),
                quote.EstimatedBranchCount,
                quote.Status,
                quote.CreatedAtUtc))
            .ToArray();
    }

    public async Task<PlatformQuoteDetailResult> GetQuoteAsync(
        Guid quoteId,
        CancellationToken cancellationToken)
    {
        var quote = await dbContext.EnterpriseQuoteRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == quoteId, cancellationToken)
            ?? throw new CustomerExperienceException("QUOTE_NOT_FOUND", "Teklif bulunamadı.");
        return await MapQuoteAsync(quote, cancellationToken);
    }

    public async Task<PlatformQuoteDetailResult> AcceptQuoteAsync(
        Guid actorUserId,
        string actorRole,
        Guid quoteId,
        AcceptPlatformQuoteCommand command,
        CancellationToken cancellationToken)
    {
        if (!PlatformStaffRoles.CanAcceptQuotes(actorRole))
        {
            throw new CustomerExperienceException(
                "PLATFORM_ROLE_DENIED",
                "Enterprise sözleşmesini yalnızca Owner veya Billing onaylar.");
        }

        var quote = await dbContext.EnterpriseQuoteRequests.SingleOrDefaultAsync(x => x.Id == quoteId, cancellationToken)
            ?? throw new CustomerExperienceException("QUOTE_NOT_FOUND", "Teklif bulunamadı.");
        var now = timeProvider.GetUtcNow();
        try
        {
            quote.Accept(actorUserId, now, command.ContractNote);
        }
        catch (InvalidOperationException)
        {
            throw new CustomerExperienceException("QUOTE_NOT_OPEN", "Bu teklif artık açık değil.");
        }

        var subscription = await GetOrCreateSubscriptionAsync(quote.TenantId, cancellationToken);
        try
        {
            subscription.ConvertToPaid(SubscriptionPlanCodes.Enterprise, now);
            if (command.ExpiresAtUtc is not null)
            {
                subscription.ChangePlan(SubscriptionPlanCodes.Enterprise, now, command.ExpiresAtUtc);
            }

            subscription.SetContractOverrides(
                command.OverrideMaxBranches,
                command.OverrideMaxActiveUsers,
                command.OverrideMaxOrderHistoryHours,
                command.OverrideMaxActiveQrCodes,
                command.ContractNote,
                now);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "EnterpriseQuoteAccepted",
            true,
            now,
            actorUserId,
            quote.TenantId,
            subjectId: quote.Id,
            detail: $"branches={command.OverrideMaxBranches} hours={command.OverrideMaxOrderHistoryHours}"));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapQuoteAsync(quote, cancellationToken);
    }

    public async Task<PlatformQuoteDetailResult> RejectQuoteAsync(
        Guid actorUserId,
        string actorRole,
        Guid quoteId,
        string? decisionNote,
        CancellationToken cancellationToken)
    {
        if (!PlatformStaffRoles.CanRejectQuotes(actorRole))
        {
            throw new CustomerExperienceException(
                "PLATFORM_ROLE_DENIED",
                "Teklifi Owner, Billing veya Support reddedebilir.");
        }

        var quote = await dbContext.EnterpriseQuoteRequests.SingleOrDefaultAsync(x => x.Id == quoteId, cancellationToken)
            ?? throw new CustomerExperienceException("QUOTE_NOT_FOUND", "Teklif bulunamadı.");
        var now = timeProvider.GetUtcNow();
        try
        {
            quote.Reject(actorUserId, now, decisionNote);
        }
        catch (InvalidOperationException)
        {
            throw new CustomerExperienceException("QUOTE_NOT_OPEN", "Bu teklif artık açık değil.");
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.ManagementAuditLogs.Add(new ManagementAuditLog(
            Guid.NewGuid(),
            "EnterpriseQuoteRejected",
            true,
            now,
            actorUserId,
            quote.TenantId,
            subjectId: quote.Id,
            detail: Truncate(decisionNote)));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await MapQuoteAsync(quote, cancellationToken);
    }

    private async Task<PlatformTenantDetailResult> LoadDetailAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tenantId, cancellationToken)
            ?? throw new CustomerExperienceException("TENANT_NOT_FOUND", "Tenant bulunamadı.");
        var restaurantName = await dbContext.Restaurants
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderBy(x => x.Name)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? tenant.Name;
        var billingEmail = await dbContext.ManagementMemberships
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Join(
                dbContext.ManagementUsers.AsNoTracking(),
                membership => membership.UserId,
                user => user.Id,
                (_, user) => user.Email)
            .OrderBy(email => email)
            .FirstOrDefaultAsync(cancellationToken);
        var subscription = await GetOrCreateSubscriptionAsync(tenantId, cancellationToken);
        var usage = await entitlements.GetUsageAsync(tenantId, Guid.Empty, cancellationToken);
        return new PlatformTenantDetailResult(
            tenant.Id,
            tenant.Name,
            restaurantName,
            billingEmail,
            usage.PlanCode,
            usage.IsTrial,
            subscription.StartedAtUtc,
            subscription.ExpiresAtUtc,
            subscription.PurchasedBranchAddonCount,
            usage.ActiveBranchCount,
            usage.FrozenBranchCount,
            usage.ActiveUserCount,
            usage.MaxBranches,
            usage.MaxActiveUsers,
            usage.MaxOrderHistoryHours,
            usage.MaxActiveQrCodes,
            subscription.OverrideMaxBranches,
            subscription.OverrideMaxActiveUsers,
            subscription.OverrideMaxOrderHistoryHours,
            subscription.OverrideMaxActiveQrCodes,
            subscription.ContractNote);
    }

    private async Task<PlatformQuoteDetailResult> MapQuoteAsync(
        EnterpriseQuoteRequest quote,
        CancellationToken cancellationToken)
    {
        var tenantName = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == quote.TenantId)
            .Select(x => x.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? quote.TenantId.ToString();
        return new PlatformQuoteDetailResult(
            quote.Id,
            quote.TenantId,
            tenantName,
            quote.ContactName,
            quote.Email,
            quote.Phone,
            quote.EstimatedBranchCount,
            quote.Note,
            quote.Status,
            quote.CreatedAtUtc,
            quote.ReviewedByUserId,
            quote.ReviewedAtUtc,
            quote.DecisionNote);
    }

    private async Task<TenantSubscription> GetOrCreateSubscriptionAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TenantSubscriptions
            .SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new TenantSubscription(tenantId, SubscriptionPlanCodes.Free, timeProvider.GetUtcNow());
        dbContext.TenantSubscriptions.Add(created);
        return created;
    }

    private static string? Truncate(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= 500 ? value.Trim() : value.Trim()[..500];
}
