/** Shared API/UI contract literals for Restaurant OS frontends. */

export const orderStatuses = [
  "submitted",
  "accepted",
  "preparing",
  "ready",
  "completed",
  "cancelled",
] as const;

export type OrderStatus = (typeof orderStatuses)[number];

export const tableOperationalStatuses = [
  "available",
  "occupied",
  "has_pending_order",
] as const;

export type TableOperationalStatus = (typeof tableOperationalStatuses)[number];

export const orderChannels = ["DINE_IN_QR"] as const;
export type OrderChannel = (typeof orderChannels)[number];

export const fulfillmentTypes = ["DINE_IN"] as const;
export type FulfillmentType = (typeof fulfillmentTypes)[number];
