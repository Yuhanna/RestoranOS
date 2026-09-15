import { describe, expect, it } from "vitest";
import type { Product } from "../domain/customer";
import {
  cartLineKey,
  initialSelections,
  lineTotalMinor,
  packageCartLineKey,
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
      kind: "product" as const,
      key: "x",
      product,
      quantity: 2,
      selections: { doneness: ["pan"], truffle: ["extra"] },
      note: "",
    };
    expect(lineTotalMinor(line)).toBe(84_000);
  });

  it("prices lunch packages independently of product modifiers", () => {
    const pkg = {
      id: "lunch",
      name: "Öğle menüsü",
      description: "",
      price: { amountMinor: 19_900, currency: "TRY" as const },
      listPrice: { amountMinor: 24_900, currency: "TRY" as const },
      discount: { amountMinor: 5_000, currency: "TRY" as const },
      components: [],
    };
    expect(packageCartLineKey(pkg, "notsuz")).toBe("package:lunch:notsuz");
    expect(
      lineTotalMinor({
        kind: "package",
        key: "package:lunch:",
        package: pkg,
        quantity: 2,
        note: "",
      }),
    ).toBe(39_800);
  });
});
