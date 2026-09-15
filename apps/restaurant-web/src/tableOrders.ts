import type { Order } from "./domain";

export type TableOrderGroup = {
  tableId: string;
  tableLabel: string;
  orders: Order[];
  roundCount: number;
  totalMinor: number;
  currency: string;
  hasSubmittedRound: boolean;
};

const createdAtMs = (order: Order) => {
  const iso = order.createdAtUtc ?? order.statusChangedAtUtc;
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? 0 : ms;
};

export function groupOrdersByTable(orders: Order[], recentIds: Set<string>): TableOrderGroup[] {
  const byTable = new Map<string, Order[]>();
  for (const order of orders) {
    const key = order.tableId || `__untabled:${order.id}`;
    const existing = byTable.get(key);
    if (existing) {
      existing.push(order);
    } else {
      byTable.set(key, [order]);
    }
  }

  const groups: TableOrderGroup[] = [];
  for (const [tableId, tableOrders] of byTable) {
    const sorted = [...tableOrders].sort((left, right) => createdAtMs(left) - createdAtMs(right));
    groups.push({
      tableId,
      tableLabel: sorted[0]?.tableLabel || "Masa",
      orders: sorted,
      roundCount: sorted.length,
      totalMinor: sorted.reduce((sum, order) => sum + order.amountMinor, 0),
      currency: sorted[0]?.currency || "TRY",
      hasSubmittedRound: sorted.some((order) => order.status === "submitted" || recentIds.has(order.id)),
    });
  }

  return groups.sort((left, right) => {
    if (left.hasSubmittedRound !== right.hasSubmittedRound) {
      return left.hasSubmittedRound ? -1 : 1;
    }
    return left.tableLabel.localeCompare(right.tableLabel, "tr");
  });
}

export function formatOrderMoney(amountMinor: number, currency: string): string {
  return (amountMinor / 100).toLocaleString("tr-TR", {
    style: "currency",
    currency: currency || "TRY",
  });
}

export function roundLabel(index: number, roundCount: number): string {
  if (roundCount <= 1) {
    return "Tek sipariş";
  }
  return `${index + 1}. tur`;
}
