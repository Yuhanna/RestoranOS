import { describe, expect, it } from "vitest";
import type { Product } from "../domain/customer";
import {
  formatPromoEndsAt,
  productHasPromo,
  promoHeadline,
  promoPercentBadge,
  promoSavingsMoney,
} from "./promoOffer";

const base: Product = {
  id: "p1",
  categoryId: "mains",
  name: "Risotto",
  description: "",
  price: { amountMinor: 20_000, currency: "TRY" },
  imageUrl: "",
  imageAlt: "",
  available: true,
  dietaryTags: [],
  allergenKeys: [],
  modifierGroups: [],
};

describe("promoOffer", () => {
  it("treats list-vs-final gap as a promotion", () => {
    const product: Product = {
      ...base,
      listPrice: { amountMinor: 25_000, currency: "TRY" },
      discount: { amountMinor: 5_000, currency: "TRY" },
      promotionLabel: "Öğle menüsü",
    };

    expect(productHasPromo(product)).toBe(true);
    expect(promoSavingsMoney(product)).toEqual({ amountMinor: 5_000, currency: "TRY" });
    expect(promoPercentBadge(product)).toBe("%20");
    expect(promoHeadline(product)).toBe("%20");
  });

  it("returns no promo when list and final match", () => {
    expect(productHasPromo(base)).toBe(false);
    expect(promoSavingsMoney(base)).toBeNull();
    expect(promoPercentBadge(base)).toBeNull();
    expect(promoHeadline(base)).toBe("Kampanya");
  });

  it("formats a campaign end time", () => {
    expect(formatPromoEndsAt("2026-09-15T18:30:00.1234567Z")).toEqual(expect.any(String));
    expect(formatPromoEndsAt(undefined)).toBeNull();
    expect(formatPromoEndsAt("not-a-date")).toBeNull();
  });
});
