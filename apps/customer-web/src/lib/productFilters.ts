import type { AllergenKey, DietaryFilterKey, ProductNutrition } from "../domain/menuCatalog";
import type { Product } from "../domain/customer";

export function productMatchesDietaryFilters(
  product: Product,
  activeFilters: ReadonlySet<DietaryFilterKey>,
): boolean {
  if (!activeFilters.size) return true;
  return [...activeFilters].every((filter) => product.dietaryTags.includes(filter));
}

export function productMatchesAllergenExclusions(
  product: Product,
  excludedAllergens: ReadonlySet<AllergenKey>,
): boolean {
  if (!excludedAllergens.size) return true;
  return ![...product.allergenKeys, ...(product.mayContainAllergenKeys ?? [])].some((key) =>
    excludedAllergens.has(key),
  );
}

export function filterProducts(
  products: ReadonlyArray<Product>,
  options: {
    categoryId: string;
    query: string;
    dietaryFilters: ReadonlySet<DietaryFilterKey>;
    excludedAllergens: ReadonlySet<AllergenKey>;
  },
): Product[] {
  const normalizedQuery = options.query.trim().toLocaleLowerCase("tr-TR");
  return products.filter((product) => {
    if (options.categoryId !== "all" && product.categoryId !== options.categoryId) {
      return false;
    }
    if (
      normalizedQuery &&
      !`${product.name} ${product.description} ${(product.ingredients ?? []).join(" ")}`
        .toLocaleLowerCase("tr-TR")
        .includes(normalizedQuery)
    ) {
      return false;
    }
    if (!productMatchesDietaryFilters(product, options.dietaryFilters)) {
      return false;
    }
    if (!productMatchesAllergenExclusions(product, options.excludedAllergens)) {
      return false;
    }
    return true;
  });
}

export function displayBadge(product: Product): string | undefined {
  if (product.badge) return product.badge;
  if (product.isNew) return "Yeni";
  return undefined;
}

export function nutritionSummaryParts(
  nutrition: ProductNutrition | undefined,
  labels: {
    weight: (grams: number) => string;
    volume: (ml: number) => string;
    calories: (kcal: number) => string;
    protein: (grams: number) => string;
    carbs: (grams: number) => string;
    fat: (grams: number) => string;
    sugar: (grams: number) => string;
    salt: (grams: number) => string;
  },
): string[] {
  if (!nutrition) return [];
  return [
    nutrition.weightGrams ? labels.weight(nutrition.weightGrams) : null,
    nutrition.volumeMl ? labels.volume(nutrition.volumeMl) : null,
    nutrition.caloriesKcal ? labels.calories(nutrition.caloriesKcal) : null,
    nutrition.proteinGrams ? labels.protein(nutrition.proteinGrams) : null,
    nutrition.carbsGrams ? labels.carbs(nutrition.carbsGrams) : null,
    nutrition.fatGrams ? labels.fat(nutrition.fatGrams) : null,
    nutrition.sugarGrams ? labels.sugar(nutrition.sugarGrams) : null,
    nutrition.saltGrams ? labels.salt(nutrition.saltGrams) : null,
  ].filter((part): part is string => Boolean(part));
}
