/**
 * Menü ürün kataloğu — EU 1169/2011 Annex II ve yaygın dijital menü platformları
 * (Toast, Lightspeed, OrderViaChat) ile hizalı yapılandırılmış alanlar.
 */

/** AB Annex II — 14 zorunlu alerjen (filtreleme ve uyumluluk için kod). */
export type AllergenKey =
  | "gluten"
  | "crustaceans"
  | "eggs"
  | "fish"
  | "peanuts"
  | "soybeans"
  | "milk"
  | "treeNuts"
  | "celery"
  | "mustard"
  | "sesame"
  | "sulphites"
  | "lupin"
  | "molluscs";

export type DietaryTag =
  | "vegetarian"
  | "vegan"
  | "glutenFree"
  | "dairyFree"
  | "nutFree"
  | "halal"
  | "kosher"
  | "jain";

/** 0 = acı yok, 3 = çok acı (Toast / Asian menu pattern). */
export type SpiceLevel = 0 | 1 | 2 | 3;

export type ProductBadge = "Çok sevilen" | "Şefin seçimi" | "Yeni" | (string & {});

/** Opsiyonel porsiyon / besin — yalnızca tanımlı alanlar gösterilir. */
export interface ProductNutrition {
  weightGrams?: number;
  volumeMl?: number;
  caloriesKcal?: number;
  proteinGrams?: number;
  carbsGrams?: number;
  fatGrams?: number;
  sugarGrams?: number;
  saltGrams?: number;
}

export type DietaryFilterKey = DietaryTag;
