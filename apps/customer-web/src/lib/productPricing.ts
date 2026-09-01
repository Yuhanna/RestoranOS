import type { CartLine, ModifierOption, Product } from "../domain/customer";

export type ModifierSelections = Record<string, string[]>;

export function findModifierOption(
  product: Product,
  groupId: string,
  optionId: string,
): ModifierOption | undefined {
  return product.modifierGroups
    .find((group) => group.id === groupId)
    ?.options.find((option) => option.id === optionId);
}

export function initialPortionId(product: Product): string | undefined {
  return product.portions?.[0]?.id;
}

export function initialSelections(product: Product): ModifierSelections {
  const selections: ModifierSelections = {};
  for (const group of product.modifierGroups) {
    if (group.required && group.options[0]) {
      selections[group.id] = [group.options[0].id];
    } else {
      selections[group.id] = [];
    }
  }
  return selections;
}

export function modifierOptionIds(selections: ModifierSelections): string[] {
  return Object.values(selections).flat().filter(Boolean);
}

export function basePriceMinor(product: Product, portionId?: string): number {
  const base = product.price.amountMinor;
  if (!portionId || !product.portions?.length) {
    return base;
  }

  const portion = product.portions.find((item) => item.id === portionId);
  if (!portion) {
    return base;
  }

  return Math.round(base * portion.priceMultiplier);
}

export function unitPriceMinor(
  product: Product,
  selections: ModifierSelections,
  portionId?: string,
): number {
  let total = basePriceMinor(product, portionId);
  for (const [groupId, optionIds] of Object.entries(selections)) {
    for (const optionId of optionIds) {
      const option = findModifierOption(product, groupId, optionId);
      if (option) {
        total += option.priceDelta.amountMinor;
      }
    }
  }
  return total;
}

export function lineTotalMinor(line: CartLine): number {
  return unitPriceMinor(line.product, line.selections, line.portionId) * line.quantity;
}

export function cartLineKey(
  product: Product,
  selections: ModifierSelections,
  note: string,
  portionId?: string,
): string {
  const selectionPart = Object.entries(selections)
    .sort(([left], [right]) => left.localeCompare(right))
    .map(([groupId, optionIds]) => `${groupId}:${[...optionIds].sort().join(",")}`)
    .join("|");
  return `${product.id}:${portionId ?? ""}:${selectionPart}:${note.trim()}`;
}

export function selectionLabels(
  product: Product,
  selections: ModifierSelections,
): string[] {
  const labels: string[] = [];
  if (product.portions?.length && product.portions.length > 1) {
    // Portion label added separately in cart UI when needed.
  }

  for (const [groupId, optionIds] of Object.entries(selections)) {
    for (const optionId of optionIds) {
      const name = findModifierOption(product, groupId, optionId)?.name;
      if (name) {
        labels.push(name);
      }
    }
  }
  return labels;
}

export function portionLabel(product: Product, portionId?: string): string | undefined {
  if (!portionId) {
    return undefined;
  }
  return product.portions?.find((portion) => portion.id === portionId)?.name;
}
