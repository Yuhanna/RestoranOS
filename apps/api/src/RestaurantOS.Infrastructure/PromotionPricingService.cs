using Microsoft.EntityFrameworkCore;
using RestaurantOS.Domain;

namespace RestaurantOS.Infrastructure;

public static class PromotionPricingService
{
    public static readonly TimeZoneInfo DefaultBranchTimeZone = ResolveIstanbul();

    private static TimeZoneInfo ResolveIstanbul()
    {
        foreach (var id in new[] { "Europe/Istanbul", "Turkey Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
    public static MenuPromotion? ResolveBestPromotion(
        IReadOnlyList<MenuPromotion> promotions,
        Guid menuItemId,
        Guid categoryId,
        DateTimeOffset utcNow,
        TimeZoneInfo? branchTimeZone = null)
    {
        MenuPromotion? best = null;
        long bestDiscount = 0;
        foreach (var promotion in promotions)
        {
            if (!promotion.IsActiveAt(utcNow, branchTimeZone) || !promotion.AppliesTo(menuItemId, categoryId))
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
        TimeZoneInfo? branchTimeZone = null) =>
        promotions.Where(x => x.IsActiveAt(utcNow, branchTimeZone)).ToArray();
}
