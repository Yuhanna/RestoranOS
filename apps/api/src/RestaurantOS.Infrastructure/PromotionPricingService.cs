using Microsoft.EntityFrameworkCore;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public static class PromotionPricingService
{
    public static TimeZoneInfo DefaultBranchTimeZone { get; } = ResolveDefaultBranchTimeZone();

    public static MenuPromotion? ResolveBestPromotion(
        IReadOnlyList<MenuPromotion> promotions,
        Guid menuItemId,
        Guid categoryId,
        DateTimeOffset utcNow,
        TimeZoneInfo? branchTimeZone = null)
    {
        var tz = branchTimeZone ?? DefaultBranchTimeZone;
        MenuPromotion? best = null;
        long bestDiscount = 0;
        foreach (var promotion in promotions)
        {
            if (!promotion.IsActiveAt(utcNow, tz) || !promotion.AppliesTo(menuItemId, categoryId))
            {
                continue;
            }

            var sampleDiscount = promotion.DiscountAmount(10_000);
            if (sampleDiscount > bestDiscount)
            {
                best = promotion;
                bestDiscount = sampleDiscount;
            }
        }

        return best;
    }

    public static PriceBreakdown PriceMenuItem(MenuItem item, MenuPromotion? promotion) =>
        PriceBreakdown.FromListPrice(item.Price.AmountMinor, promotion, item.Price.Currency);

    public static IReadOnlyList<MenuPromotion> FilterActivePromotions(
        IEnumerable<MenuPromotion> promotions,
        DateTimeOffset utcNow,
        TimeZoneInfo? branchTimeZone = null)
    {
        var tz = branchTimeZone ?? DefaultBranchTimeZone;
        return promotions.Where(x => x.IsActiveAt(utcNow, tz)).ToArray();
    }

    private static TimeZoneInfo ResolveDefaultBranchTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.Local;
            }
        }
    }
}
