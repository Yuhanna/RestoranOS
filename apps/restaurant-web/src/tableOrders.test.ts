import { describe, expect, it } from "vitest";
import type { Order } from "./domain";
import { formatOrderMoney, groupOrdersByTable, roundLabel } from "./tableOrders";

const order = (
  overrides: Partial<Order> & Pick<Order, "id" | "tableId" | "tableLabel">,
): Order => ({
  displayNumber: `#${overrides.id.slice(-3)}`,
  status: "accepted",
  createdAtUtc: "2026-09-15T12:00:00Z",
  statusChangedAtUtc: "2026-09-15T12:00:00Z",
  estimatedReadyAtUtc: "2026-09-15T12:20:00Z",
  amountMinor: 1000,
  currency: "TRY",
  ...overrides,
});

describe("groupOrdersByTable", () => {
  it("groups same-table rounds, sums the ticket, and flags a new submitted round", () => {
    const first = order({
      id: "o1",
      tableId: "t1",
      tableLabel: "Masa 1",
      createdAtUtc: "2026-09-15T12:00:00Z",
      amountMinor: 2500,
    });
    const second = order({
      id: "o2",
      tableId: "t1",
      tableLabel: "Masa 1",
      status: "submitted",
      createdAtUtc: "2026-09-15T12:10:00Z",
      amountMinor: 1500,
    });
    const other = order({
      id: "o3",
      tableId: "t2",
      tableLabel: "Masa 2",
      amountMinor: 800,
    });

    const groups = groupOrdersByTable([other, second, first], new Set());

    expect(groups).toHaveLength(2);
    expect(groups[0]?.tableId).toBe("t1");
    expect(groups[0]?.roundCount).toBe(2);
    expect(groups[0]?.totalMinor).toBe(4000);
    expect(groups[0]?.hasSubmittedRound).toBe(true);
    expect(groups[0]?.orders.map((item) => item.id)).toEqual(["o1", "o2"]);
    expect(groups[1]?.tableId).toBe("t2");
    expect(groups[1]?.hasSubmittedRound).toBe(false);
  });

  it("treats recently arrived orders as a new round even after status moves on", () => {
    const preparing = order({
      id: "recent",
      tableId: "t9",
      tableLabel: "Masa 9",
      status: "preparing",
    });

    const [group] = groupOrdersByTable([preparing], new Set(["recent"]));
    expect(group?.hasSubmittedRound).toBe(true);
  });
});

describe("roundLabel", () => {
  it("labels a single ticket without a round number", () => {
    expect(roundLabel(0, 1)).toBe("Tek sipariş");
  });

  it("numbers multi-round tables from the earliest order", () => {
    expect(roundLabel(0, 2)).toBe("1. tur");
    expect(roundLabel(1, 2)).toBe("2. tur");
  });
});

describe("formatOrderMoney", () => {
  it("formats minor units in Turkish lira", () => {
    expect(formatOrderMoney(1250, "TRY")).toMatch(/12[,.]50/);
  });
});
