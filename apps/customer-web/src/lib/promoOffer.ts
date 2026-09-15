import type { Money, Product } from "../domain/customer";

export function productHasPromo(product: Product): boolean {
  const discountMinor = product.discount?.amountMinor ?? 0;
  const listMinor = product.listPrice?.amountMinor ?? 0;
  return discountMinor > 0 || (listMinor > 0 && listMinor > product.price.amountMinor);
}

export function promoSavingsMoney(product: Product): Money | null {
  if (!productHasPromo(product)) {
    return null;
  }

  if (product.discount && product.discount.amountMinor > 0) {
    return product.discount;
  }

  const listMinor = product.listPrice?.amountMinor ?? 0;
  const saved = listMinor - product.price.amountMinor;
  if (saved <= 0) {
    return null;
  }

  return { amountMinor: saved, currency: product.price.currency };
}

export function promoPercentBadge(product: Product): string | null {
  const percent = promoPercent(product);
  return percent === null ? null : `%${percent}`;
}

export function promoHeadline(product: Product): string {
  return promoPercentBadge(product) ?? product.promotionLabel ?? "Kampanya";
}

export function formatPromoEndsAt(iso?: string): string | null {
  if (!iso) {
    return null;
  }

  const normalized = iso.replace(/(\.\d{3})\d+/, "$1");
  const date = new Date(normalized);
  if (Number.isNaN(date.getTime())) {
    return null;
  }

  return new Intl.DateTimeFormat("tr-TR", {
    day: "numeric",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  }).format(date);
}

function promoPercent(product: Product): number | null {
  if (typeof product.discountPercent === "number" && product.discountPercent > 0) {
    return Math.round(product.discountPercent);
  }

  if (product.discountKind === "percent" && typeof product.discountValue === "number" && product.discountValue > 0) {
    return Math.round(product.discountValue);
  }

  const listMinor = product.listPrice?.amountMinor ?? 0;
  if (listMinor <= product.price.amountMinor) {
    return null;
  }

  return Math.round(((listMinor - product.price.amountMinor) / listMinor) * 100);
}
