export interface POSProduct {
  id: string;
  name: string;
  price: number;
  category: string;
  popular?: boolean;
  color?: string;
}

export const CATEGORIES = [
  { id: "quick",    label: "Quick Add",  icon: "⚡" },
  { id: "starters", label: "Starters",  icon: "◎" },
  { id: "mains",    label: "Mains",     icon: "▣" },
  { id: "desserts", label: "Desserts",  icon: "◈" },
  { id: "drinks",   label: "Drinks",    icon: "◷" },
  { id: "set",      label: "Set Menus", icon: "◻" },
];

export const PRODUCTS: POSProduct[] = [
  /* Starters */
  { id: "s1", category: "starters", name: "Steamed Mussels",     price: 185, popular: true },
  { id: "s2", category: "starters", name: "Tasting Amuse-Bouche",price: 220 },
  { id: "s3", category: "starters", name: "Salmon Tartare",      price: 165 },
  { id: "s4", category: "starters", name: "Burrata & Heirloom",  price: 145 },
  { id: "s5", category: "starters", name: "Soup du Jour",        price: 110 },
  { id: "s6", category: "starters", name: "Foie Gras Torchon",   price: 280 },

  /* Mains */
  { id: "m1", category: "mains", name: "Wagyu Tenderloin",     price: 680, popular: true },
  { id: "m2", category: "mains", name: "Mediterranean Sea Bass",price: 420, popular: true },
  { id: "m3", category: "mains", name: "Herb-Crusted Lamb",    price: 520 },
  { id: "m4", category: "mains", name: "Black Truffle Risotto",price: 340, popular: true },
  { id: "m5", category: "mains", name: "Vegetable Tagine",     price: 280 },
  { id: "m6", category: "mains", name: "Grilled Lobster",      price: 780 },
  { id: "m7", category: "mains", name: "Duck Confit",          price: 460 },
  { id: "m8", category: "mains", name: "Pasta Carbonara",      price: 220 },

  /* Desserts */
  { id: "d1", category: "desserts", name: "Chocolate Fondant",  price: 165, popular: true },
  { id: "d2", category: "desserts", name: "Crème Brûlée",      price: 145 },
  { id: "d3", category: "desserts", name: "Artisan Cheese",     price: 185 },
  { id: "d4", category: "desserts", name: "Seasonal Sorbet",    price: 95 },
  { id: "d5", category: "desserts", name: "Tiramisu",          price: 140 },

  /* Drinks */
  { id: "dr1", category: "drinks", name: "Signature Negroni",  price: 145, popular: true },
  { id: "dr2", category: "drinks", name: "Aperol Spritz",      price: 120, popular: true },
  { id: "dr3", category: "drinks", name: "House Wine · Red",   price: 95 },
  { id: "dr4", category: "drinks", name: "House Wine · White", price: 90 },
  { id: "dr5", category: "drinks", name: "Sparkling Water",    price: 55 },
  { id: "dr6", category: "drinks", name: "Still Water",        price: 45 },
  { id: "dr7", category: "drinks", name: "Fresh Juice",        price: 75 },
  { id: "dr8", category: "drinks", name: "Espresso",           price: 60 },
  { id: "dr9", category: "drinks", name: "Soft Drinks",        price: 50 },

  /* Set menus */
  { id: "set1", category: "set", name: "Tasting Menu · 5 Course",   price: 1200, popular: true },
  { id: "set2", category: "set", name: "Chef's Table · 8 Course",   price: 1800 },
  { id: "set3", category: "set", name: "Vegetarian Tasting",        price: 950 },
  { id: "set4", category: "set", name: "Wine Pairing · 5 Glass",    price: 650 },
];

export const QUICK: POSProduct[] = PRODUCTS.filter(p => p.popular);

export const DISCOUNT_PRESETS = [
  { label: "Staff 50%",   type: "pct"   as const, value: 50 },
  { label: "Manager 20%", type: "pct"   as const, value: 20 },
  { label: "Comp 10%",    type: "pct"   as const, value: 10 },
  { label: "₺100 Off",   type: "fixed" as const, value: 100 },
  { label: "₺200 Off",   type: "fixed" as const, value: 200 },
];

export const TABLES = Array.from({ length: 24 }, (_, i) => ({
  n: i + 1,
  occupied: [2,3,5,7,9,12,14,16,18,20,22,23].includes(i + 1),
  covers: [2,3,5,7,9,12,14,16,18,20,22,23].includes(i + 1)
    ? [4,6,2,2,4,4,5,2,2,4,3,6][([2,3,5,7,9,12,14,16,18,20,22,23]).indexOf(i + 1)]
    : 0,
}));
