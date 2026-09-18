import type { Order } from "./domain";

export type TableOrderGroup = {
  tableId: string;
  tableLabel: string;
  orders: Order[];
  roundCount: number;
  totalMinor: number;
  currency: string;
  hasSubmittedRound: boolean;
  hasIncompleteKitchen: boolean;
};

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

const isEmptyTableId = (tableId: string | null | undefined) =>
  !tableId || tableId === EMPTY_GUID;

const createdAtMs = (order: Order) => {
  const iso = order.createdAtUtc ?? order.statusChangedAtUtc;
  const ms = Date.parse(iso);
  return Number.isNaN(ms) ? 0 : ms;
};

export function isKitchenIncompleteStatus(status: Order["status"]): boolean {
  return status === "submitted" || status === "accepted" || status === "preparing";
}

export function groupOrdersByTable(orders: Order[], recentIds: Set<string>): TableOrderGroup[] {
  const byTable = new Map<string, Order[]>();
  for (const order of orders) {
    const key = isEmptyTableId(order.tableId)
      ? `__untabled:${order.tableLabel || order.id}`
      : order.tableId;
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
      hasIncompleteKitchen: sorted.some((order) => isKitchenIncompleteStatus(order.status)),
    });
  }

  return groups.sort((left, right) => {
    if (left.hasIncompleteKitchen !== right.hasIncompleteKitchen) {
      return left.hasIncompleteKitchen ? -1 : 1;
    }
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
