import { describe, expect, it } from "vitest";
import type { Product } from "../domain/customer";
import {
  cartLineKey,
  initialSelections,
  lineTotalMinor,
  unitPriceMinor,
} from "./productPricing";

const product: Product = {
  id: "p1",
  categoryId: "mains",
  name: "Demo",
  description: "",
  price: { amountMinor: 34_000, currency: "TRY" },
  imageUrl: "",
  imageAlt: "",
  available: true,
  dietaryTags: [],
  allergenKeys: [],
  modifierGroups: [
    {
      id: "truffle",
      name: "Trüf miktarı",
      required: false,
      options: [
        { id: "standard", name: "Standart", priceDelta: { amountMinor: 0, currency: "TRY" } },
        { id: "extra", name: "Ekstra trüf", priceDelta: { amountMinor: 8_000, currency: "TRY" } },
      ],
    },
    {
      id: "doneness",
      name: "Pişirme şekli",
      required: true,
      options: [
        { id: "pan", name: "Tavada", priceDelta: { amountMinor: 0, currency: "TRY" } },
        { id: "wood", name: "Odun ateşinde", priceDelta: { amountMinor: 0, currency: "TRY" } },
      ],
    },
  ],
};

describe("productPricing", () => {
  it("preselects required modifier groups", () => {
    expect(initialSelections(product)).toEqual({ doneness: ["pan"], truffle: [] });
  });

  it("adds optional modifier deltas to unit price", () => {
    const selections = { ...initialSelections(product), truffle: ["extra"] };
    expect(unitPriceMinor(product, selections)).toBe(42_000);
  });

  it("builds stable cart keys from selections", () => {
    const key = cartLineKey(product, { doneness: ["pan"], truffle: ["extra"] }, "Az tuzlu");
    expect(key).toContain("doneness:pan");
    expect(key).toContain("truffle:extra");
    expect(key).toContain("Az tuzlu");
  });

  it("multiplies configured unit price by quantity", () => {
    const line = {
      key: "x",
      product,
      quantity: 2,
      selections: { doneness: ["pan"], truffle: ["extra"] },
      note: "",
    };
    expect(lineTotalMinor(line)).toBe(84_000);
  });
});
