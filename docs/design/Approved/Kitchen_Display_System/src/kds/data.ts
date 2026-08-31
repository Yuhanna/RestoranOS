export type KDSStatus = "new" | "preparing" | "ready" | "delayed";
export type StationId = "all" | "grill" | "pasta" | "cold" | "dessert" | "drinks";

export interface KDSItem {
  id: string;
  name: string;
  qty: number;
  modifiers: string[];
  note?: string;
}

export interface KDSOrder {
  id: string;
  shortId: string;
  table: string;
  channel: "dine-in" | "delivery" | "takeaway";
  channelLabel?: string;
  covers: number;
  station: Exclude<StationId, "all">;
  status: KDSStatus;
  priority: "normal" | "high" | "vip";
  items: KDSItem[];
  kitchenNote?: string;
  allergyNote?: string;
  placedSecondsAgo: number;
  etaSeconds: number;
  waiter: string;
}

export const STATION_META: Record<StationId, { label: string; color: string; colorDim: string; icon: string }> = {
  all:     { label: "All Stations", color: "#A1A1AA",  colorDim: "rgba(161,161,170,0.12)", icon: "⊞" },
  grill:   { label: "Grill",        color: "#FB923C",  colorDim: "rgba(251,146,60,0.12)",  icon: "🔥" },
  pasta:   { label: "Pasta",        color: "#FACC15",  colorDim: "rgba(250,204,21,0.12)",  icon: "◎" },
  cold:    { label: "Cold Prep",    color: "#38BDF8",  colorDim: "rgba(56,189,248,0.12)",  icon: "❄" },
  dessert: { label: "Desserts",     color: "#C084FC",  colorDim: "rgba(192,132,252,0.12)", icon: "◈" },
  drinks:  { label: "Drinks",       color: "#34D399",  colorDim: "rgba(52,211,153,0.12)",  icon: "◷" },
};

export const INITIAL_ORDERS: KDSOrder[] = [
  {
    id: "ORD-2841", shortId: "841", table: "T12", channel: "dine-in", covers: 4,
    station: "grill", status: "delayed", priority: "normal",
    placedSecondsAgo: 1980, etaSeconds: -480,
    waiter: "Fatma",
    allergyNote: "Guest 2 — severe dairy allergy",
    items: [
      { id: "i1", name: "Wagyu Tenderloin", qty: 2, modifiers: ["Medium Rare", "Truffle Jus", "Truffle Fries"] },
      { id: "i2", name: "Sea Bass", qty: 1, modifiers: ["Pan-Seared", "No dairy sauce"] },
    ],
  },
  {
    id: "ORD-2840", shortId: "840", table: "T7", channel: "dine-in", covers: 2,
    station: "grill", status: "delayed", priority: "vip",
    placedSecondsAgo: 1560, etaSeconds: -240,
    waiter: "Emre",
    items: [
      { id: "i3", name: "Herb-Crusted Lamb", qty: 1, modifiers: ["Pink (recommended)", "Extra rosemary jus"] },
    ],
    kitchenNote: "VIP table — owner of Bosphorus Group",
  },
  {
    id: "ORD-2839", shortId: "839", table: "T3", channel: "dine-in", covers: 6,
    station: "pasta", status: "preparing", priority: "normal",
    placedSecondsAgo: 900, etaSeconds: 480,
    waiter: "Selin",
    items: [
      { id: "i4", name: "Black Truffle Risotto", qty: 2, modifiers: ["Extra truffle shaving"] },
      { id: "i5", name: "Mediterranean Sea Bass", qty: 2, modifiers: ["Wood Grilled", "Side Salad"] },
    ],
  },
  {
    id: "ORD-2838", shortId: "838", table: "T18", channel: "dine-in", covers: 2,
    station: "cold", status: "ready", priority: "normal",
    placedSecondsAgo: 720, etaSeconds: 0,
    waiter: "Can",
    items: [
      { id: "i6", name: "Salmon Tartare", qty: 1, modifiers: ["Extra capers", "No onion"] },
      { id: "i7", name: "Tasting Amuse-Bouche", qty: 2, modifiers: [] },
    ],
  },
  {
    id: "ORD-2837", shortId: "837", table: "T9", channel: "dine-in", covers: 4,
    station: "cold", status: "ready", priority: "normal",
    placedSecondsAgo: 660, etaSeconds: 0,
    waiter: "Fatma",
    items: [
      { id: "i8", name: "Steamed Mussels", qty: 2, modifiers: ["Sourdough (included)"] },
    ],
  },
  {
    id: "ORD-2836", shortId: "836", table: "T22", channel: "dine-in", covers: 3,
    station: "pasta", status: "preparing", priority: "high",
    placedSecondsAgo: 540, etaSeconds: 660,
    waiter: "Emre",
    items: [
      { id: "i9", name: "Black Truffle Risotto", qty: 3, modifiers: ["Standard shaving"] },
    ],
    kitchenNote: "Fire with T22 mains — they have starter already",
  },
  {
    id: "ORD-2835", shortId: "835", table: "T5", channel: "dine-in", covers: 2,
    station: "dessert", status: "preparing", priority: "normal",
    placedSecondsAgo: 420, etaSeconds: 300,
    waiter: "Selin",
    items: [
      { id: "i10", name: "Chocolate Fondant", qty: 2, modifiers: ["Vanilla Bean gelato", "Extra Maldon salt"] },
    ],
  },
  {
    id: "ORD-2834", shortId: "834", table: "T14", channel: "dine-in", covers: 5,
    station: "grill", status: "preparing", priority: "normal",
    placedSecondsAgo: 360, etaSeconds: 720,
    waiter: "Can",
    items: [
      { id: "i11", name: "Wagyu Tenderloin", qty: 1, modifiers: ["Medium", "Béarnaise", "Seasonal Vegetables"] },
      { id: "i12", name: "Herb-Crusted Lamb", qty: 2, modifiers: ["Medium"] },
    ],
  },
  {
    id: "ORD-2833", shortId: "833", table: "Getir #482", channel: "delivery", channelLabel: "Getir",
    covers: 1, station: "pasta", status: "preparing", priority: "normal",
    placedSecondsAgo: 240, etaSeconds: 900,
    waiter: "—",
    items: [
      { id: "i13", name: "Black Truffle Risotto", qty: 1, modifiers: ["Standard shaving"] },
    ],
    kitchenNote: "Delivery pickup at 22:18",
  },
  {
    id: "ORD-2832", shortId: "832", table: "T1", channel: "dine-in", covers: 2,
    station: "drinks", status: "new", priority: "normal",
    placedSecondsAgo: 35, etaSeconds: 300,
    waiter: "Fatma",
    items: [
      { id: "i14", name: "Signature Negroni", qty: 2, modifiers: ["Classic"] },
      { id: "i15", name: "Rose Aperitivo Spritz", qty: 1, modifiers: [] },
    ],
  },
  {
    id: "ORD-2831", shortId: "831", table: "T16", channel: "dine-in", covers: 2,
    station: "grill", status: "new", priority: "normal",
    placedSecondsAgo: 18, etaSeconds: 1200,
    waiter: "Emre",
    items: [
      { id: "i16", name: "Wagyu Tenderloin", qty: 2, modifiers: ["Rare", "Pepper Sauce", "Truffle Fries"] },
    ],
    allergyNote: "Guest 1 — nut allergy",
  },
  {
    id: "ORD-2830", shortId: "830", table: "T20", channel: "dine-in", covers: 4,
    station: "dessert", status: "new", priority: "high",
    placedSecondsAgo: 8, etaSeconds: 840,
    waiter: "Can",
    items: [
      { id: "i17", name: "Chocolate Fondant", qty: 2, modifiers: ["Pistachio gelato"] },
      { id: "i18", name: "Crème Brûlée", qty: 2, modifiers: ["Extra caramelised"] },
    ],
    kitchenNote: "Birthday celebration — add complimentary macarons",
  },
];
