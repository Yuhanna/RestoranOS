using Microsoft.EntityFrameworkCore;
using RestaurantOS.Application;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public sealed class PlatformSubscriptionOfferService(RestaurantOsDbContext dbContext) : IPlatformSubscriptionOfferService
{
    public async Task<IReadOnlyList<ManagedSubscriptionOfferResult>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.SubscriptionOffers
            .AsNoTracking()
            .OrderByDescending(x => x.StartsAtUtc)
            .Select(x => Map(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<ManagedSubscriptionOfferResult> CreateAsync(
        CreateSubscriptionOfferCommand command,
        CancellationToken cancellationToken)
    {
        SubscriptionOffer entity;
        try
        {
            entity = new SubscriptionOffer(
                Guid.NewGuid(),
                command.Audience,
                command.TargetPlanCode,
                command.DiscountPercent,
                command.DurationMonths,
                command.Title,
                command.Body,
                command.StartsAtUtc,
                command.EndsAtUtc,
                command.IsActive);
        }
        catch (ArgumentException exception)
        {
            throw new CustomerExperienceException("VALIDATION_ERROR", exception.Message);
        }

        dbContext.SubscriptionOffers.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ManagedSubscriptionOfferResult> SetActiveAsync(
        Guid offerId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.SubscriptionOffers.SingleOrDefaultAsync(
            x => x.Id == offerId,
            cancellationToken)
            ?? throw new CustomerExperienceException("OFFER_NOT_FOUND", "Subscription offer was not found.");

        entity.SetActive(isActive);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    private static ManagedSubscriptionOfferResult Map(SubscriptionOffer offer) =>
        new(
            offer.Id,
            offer.Audience,
            offer.TargetPlanCode,
            offer.DiscountPercent,
            offer.DurationMonths,
            offer.Title,
            offer.Body,
            offer.StartsAtUtc,
            offer.EndsAtUtc,
            offer.IsActive);
}
