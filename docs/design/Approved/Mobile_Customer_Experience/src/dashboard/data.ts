export const BRANCH = { name: "Marea", location: "Nişantaşı", shift: "Dinner", manager: "Ayşe Kaya" };

export const STATS = {
  revenue: { today: 47_380, yesterday: 40_150, target: 52_000 },
  orders: { active: 23, completed: 84, delayed: 5, cancelled: 2 },
  tables: { total: 24, occupied: 16, available: 8, reserved: 3 },
  eta: { accuracy: 87, avgDelay: 4.2, benchmark: 90 },
  covers: { seated: 61, avgSpend: 776 },
};

export const REVENUE_HOURLY = [
  { hour: "17:00", revenue: 0 },
  { hour: "17:30", revenue: 1_240 },
  { hour: "18:00", revenue: 3_800 },
  { hour: "18:30", revenue: 6_420 },
  { hour: "19:00", revenue: 9_870 },
  { hour: "19:30", revenue: 14_200 },
  { hour: "20:00", revenue: 19_550 },
  { hour: "20:30", revenue: 25_800 },
  { hour: "21:00", revenue: 33_120 },
  { hour: "21:30", revenue: 39_600 },
  { hour: "22:00", revenue: 47_380 },
];

export const YESTERDAY_HOURLY = [
  { hour: "17:00", revenue: 0 },
  { hour: "17:30", revenue: 980 },
  { hour: "18:00", revenue: 3_100 },
  { hour: "18:30", revenue: 5_800 },
  { hour: "19:00", revenue: 8_400 },
  { hour: "19:30", revenue: 12_100 },
  { hour: "20:00", revenue: 17_300 },
  { hour: "20:30", revenue: 22_600 },
  { hour: "21:00", revenue: 28_900 },
  { hour: "21:30", revenue: 34_500 },
  { hour: "22:00", revenue: 40_150 },
];

export type StationStatus = "idle" | "normal" | "busy" | "critical";
export interface Station {
  id: string;
  name: string;
  load: number;
  queue: number;
  status: StationStatus;
  chef: string;
  avgTime: number;
}

export const STATIONS: Station[] = [
  { id: "grill", name: "Grill", load: 78, queue: 3, status: "busy", chef: "M. Yıldız", avgTime: 18 },
  { id: "fryer", name: "Fryer", load: 42, queue: 2, status: "normal", chef: "K. Demir", avgTime: 8 },
  { id: "pasta", name: "Pasta & Risotto", load: 94, queue: 5, status: "critical", chef: "A. Şahin", avgTime: 22 },
  { id: "cold", name: "Cold Prep", load: 28, queue: 1, status: "normal", chef: "B. Arslan", avgTime: 6 },
  { id: "dessert", name: "Desserts", load: 55, queue: 2, status: "normal", chef: "E. Koç", avgTime: 12 },
];

export type OrderStatus = "received" | "preparing" | "ready" | "served" | "delayed" | "cancelled";
export interface LiveOrder {
  id: string;
  table: number;
  covers: number;
  items: string[];
  status: OrderStatus;
  placedAt: string;
  etaMin: number;
  overdueMin?: number;
  waiter: string;
  total: number;
  station: string;
}

export const LIVE_ORDERS: LiveOrder[] = [
  { id: "ORD-2841", table: 12, covers: 4, items: ["Wagyu Tenderloin ×2", "Sea Bass"], status: "delayed", placedAt: "21:08", etaMin: 25, overdueMin: 8, waiter: "Fatma", total: 1_780, station: "grill" },
  { id: "ORD-2840", table: 7, covers: 2, items: ["Herb-Crusted Lamb"], status: "delayed", placedAt: "21:14", etaMin: 22, overdueMin: 4, waiter: "Emre", total: 520, station: "grill" },
  { id: "ORD-2839", table: 3, covers: 6, items: ["Risotto ×2", "Sea Bass ×2", "Starters ×4"], status: "preparing", placedAt: "21:22", etaMin: 14, waiter: "Selin", total: 2_340, station: "pasta" },
  { id: "ORD-2838", table: 18, covers: 2, items: ["Salmon Tartare", "Wagyu"], status: "ready", placedAt: "21:18", etaMin: 0, waiter: "Can", total: 890, station: "cold" },
  { id: "ORD-2837", table: 9, covers: 4, items: ["Mussels ×2", "Amuse-Bouche"], status: "ready", placedAt: "21:16", etaMin: 0, waiter: "Fatma", total: 820, station: "cold" },
  { id: "ORD-2836", table: 22, covers: 3, items: ["Tasting Menu ×3"], status: "preparing", placedAt: "21:26", etaMin: 18, waiter: "Emre", total: 1_950, station: "pasta" },
  { id: "ORD-2835", table: 5, covers: 2, items: ["Fondant ×2", "Spritz ×2"], status: "preparing", placedAt: "21:30", etaMin: 9, waiter: "Selin", total: 530, station: "dessert" },
  { id: "ORD-2834", table: 14, covers: 5, items: ["Mixed Grill", "Risotto ×2"], status: "preparing", placedAt: "21:28", etaMin: 16, waiter: "Can", total: 1_460, station: "grill" },
];

export type TableStatus = "available" | "reserved" | "seated" | "ordering" | "eating" | "bill-requested" | "delayed";
export interface TableData {
  n: number;
  status: TableStatus;
  covers?: number;
  waiter?: string;
  openMin?: number;
  flag?: string;
}

export const TABLES: TableData[] = [
  { n: 1, status: "available" },
  { n: 2, status: "eating", covers: 4, waiter: "Emre", openMin: 52 },
  { n: 3, status: "eating", covers: 6, waiter: "Selin", openMin: 38, flag: "high-spend" },
  { n: 4, status: "reserved" },
  { n: 5, status: "eating", covers: 2, waiter: "Selin", openMin: 24 },
  { n: 6, status: "available" },
  { n: 7, status: "delayed", covers: 2, waiter: "Emre", openMin: 61, flag: "delayed" },
  { n: 8, status: "bill-requested", covers: 3, waiter: "Can", openMin: 88 },
  { n: 9, status: "eating", covers: 4, waiter: "Fatma", openMin: 40 },
  { n: 10, status: "available" },
  { n: 11, status: "ordering", covers: 2, waiter: "Can", openMin: 8 },
  { n: 12, status: "delayed", covers: 4, waiter: "Fatma", openMin: 73, flag: "delayed" },
  { n: 13, status: "available" },
  { n: 14, status: "eating", covers: 5, waiter: "Can", openMin: 28 },
  { n: 15, status: "reserved" },
  { n: 16, status: "eating", covers: 2, waiter: "Emre", openMin: 44 },
  { n: 17, status: "available" },
  { n: 18, status: "eating", covers: 2, waiter: "Can", openMin: 35 },
  { n: 19, status: "available" },
  { n: 20, status: "eating", covers: 4, waiter: "Fatma", openMin: 66 },
  { n: 21, status: "available" },
  { n: 22, status: "eating", covers: 3, waiter: "Emre", openMin: 20 },
  { n: 23, status: "bill-requested", covers: 6, waiter: "Selin", openMin: 95 },
  { n: 24, status: "reserved" },
];

export type IntegrationStatus = "online" | "degraded" | "offline";
export interface Integration {
  id: string;
  name: string;
  category: string;
  status: IntegrationStatus;
  latency?: number;
  lastSync?: string;
  detail?: string;
}

export const INTEGRATIONS: Integration[] = [
  { id: "pos", name: "iKas POS", category: "Point of Sale", status: "online", latency: 42, lastSync: "22:00" },
  { id: "payment", name: "Stripe Terminal", category: "Payments", status: "online", latency: 68, lastSync: "22:00" },
  { id: "delivery", name: "Getir Food", category: "Delivery", status: "degraded", latency: 1_840, lastSync: "21:48", detail: "High latency · orders may delay" },
  { id: "inventory", name: "MarketMan", category: "Inventory", status: "online", latency: 124, lastSync: "21:55" },
  { id: "loyalty", name: "Stamp Me", category: "Loyalty", status: "offline", detail: "API key expired · renewal required" },
  { id: "reservation", name: "SevenRooms", category: "Reservations", status: "online", latency: 88, lastSync: "22:00" },
];

export const ALERTS = [
  { id: "a1", level: "critical" as const, message: "Table 12 order overdue by 8 min", action: "View order", time: "21:58" },
  { id: "a2", level: "critical" as const, message: "Pasta station at 94% capacity", action: "View kitchen", time: "21:55" },
  { id: "a3", level: "warning" as const, message: "Getir Food integration degraded — high latency", action: "View status", time: "21:48" },
  { id: "a4", level: "warning" as const, message: "Stamp Me loyalty offline — API key expired", action: "Fix now", time: "20:30" },
  { id: "a5", level: "info" as const, message: "Table 23 requesting bill · ₺2,140", action: "Send waiter", time: "22:01" },
];
