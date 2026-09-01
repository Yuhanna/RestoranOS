import { describe, expect, it } from "vitest";
import type { Product } from "../domain/customer";
import { filterProducts } from "./productFilters";

const baseProduct = (overrides: Partial<Product>): Product => ({
  id: "p1",
  categoryId: "mains",
  name: "Demo",
  description: "Açıklama",
  price: { amountMinor: 10_000, currency: "TRY" },
  imageUrl: "",
  imageAlt: "",
  available: true,
  dietaryTags: ["vegan"],
  allergenKeys: ["gluten"],
  mayContainAllergenKeys: ["milk"],
  modifierGroups: [],
  ...overrides,
});

describe("filterProducts", () => {
  const products = [
    baseProduct({ id: "1", name: "Vegan Bowl", dietaryTags: ["vegan", "glutenFree"], allergenKeys: [] }),
    baseProduct({
      id: "2",
      name: "Levrek",
      dietaryTags: ["glutenFree"],
      allergenKeys: ["fish"],
    }),
    baseProduct({ id: "3", name: "Pasta", dietaryTags: ["vegetarian"], allergenKeys: ["gluten", "eggs"] }),
  ];

  it("filters by dietary tag", () => {
    const result = filterProducts(products, {
      categoryId: "all",
      query: "",
      dietaryFilters: new Set(["vegan"]),
      excludedAllergens: new Set(),
    });
    expect(result.map((item) => item.id)).toEqual(["1"]);
  });

  it("excludes items containing blocked allergens", () => {
    const result = filterProducts(products, {
      categoryId: "all",
      query: "",
      dietaryFilters: new Set(),
      excludedAllergens: new Set(["gluten"]),
    });
    expect(result.map((item) => item.id)).toEqual(["1", "2"]);
  });

  it("searches ingredient text", () => {
    const result = filterProducts(
      [baseProduct({ id: "4", ingredients: ["siyah trüf", "parmesan"] })],
      {
        categoryId: "all",
        query: "trüf",
        dietaryFilters: new Set(),
        excludedAllergens: new Set(),
      },
    );
    expect(result).toHaveLength(1);
  });
});
