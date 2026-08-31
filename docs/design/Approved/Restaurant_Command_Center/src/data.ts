export type DietaryTag = "V" | "VG" | "GF" | "DF";
export type AllergenKey = "gluten" | "dairy" | "eggs" | "nuts" | "shellfish" | "fish" | "soy" | "sulphites";

export interface Modifier {
  id: string;
  label: string;
  priceAddon?: number;
}

export interface ModifierGroup {
  id: string;
  label: string;
  required: boolean;
  multi: boolean;
  options: Modifier[];
}

export interface MenuItem {
  id: string;
  category: string;
  name: string;
  desc: string;
  longDesc: string;
  price: number;
  img: string;
  tags: DietaryTag[];
  allergens: AllergenKey[];
  badge?: string;
  modifiers: ModifierGroup[];
  popular?: boolean;
}

export const RESTAURANT = {
  name: "Marea",
  branch: "Nişantaşı",
  table: 7,
  logo: "M",
  accent: "#C4622D",
  tagline: "Modern Mediterranean",
};

export const CATEGORIES = [
  { id: "all", label: "All" },
  { id: "starters", label: "Starters" },
  { id: "mains", label: "Mains" },
  { id: "desserts", label: "Desserts" },
  { id: "drinks", label: "Drinks" },
];

export const MENU: MenuItem[] = [
  {
    id: "s1",
    category: "starters",
    name: "Steamed Mussels",
    desc: "White wine, shallots, fresh herbs, grilled sourdough",
    longDesc: "Wild-caught Aegean mussels steamed in a fragrant white wine broth with shallots, garlic, fresh thyme, and flat-leaf parsley. Served with grilled sourdough for dipping.",
    price: 185,
    img: "https://images.unsplash.com/photo-1785031728396-c88b166a9517?w=800&h=600&fit=crop&auto=format",
    tags: ["DF"],
    allergens: ["shellfish", "gluten", "sulphites"],
    badge: "Chef's Pick",
    popular: true,
    modifiers: [
      {
        id: "bread",
        label: "Bread",
        required: false,
        multi: false,
        options: [
          { id: "sourdough", label: "Sourdough (included)" },
          { id: "gf-bread", label: "Gluten-free bread", priceAddon: 15 },
          { id: "no-bread", label: "No bread" },
        ],
      },
    ],
  },
  {
    id: "s2",
    category: "starters",
    name: "Tasting Amuse-Bouche",
    desc: "Chef's daily selection of three seasonal bites",
    longDesc: "A rotating trio of seasonal small plates prepared by the kitchen team — showcasing the best produce of the day. Changes daily based on market availability.",
    price: 220,
    img: "https://images.unsplash.com/photo-1758384077555-36242d3f2b4d?w=800&h=600&fit=crop&auto=format",
    tags: ["GF"],
    allergens: ["dairy", "eggs"],
    badge: "New",
    modifiers: [],
  },
  {
    id: "m1",
    category: "mains",
    name: "Wagyu Beef Tenderloin",
    desc: "A5 wagyu, truffle jus, seasonal microgreens, roasted garlic",
    longDesc: "Pan-seared Japanese A5 wagyu tenderloin rested to perfection, finished with a rich black truffle jus and garnished with seasonal microgreens and confit garlic.",
    price: 680,
    img: "https://images.unsplash.com/photo-1663530761401-15eefb544889?w=800&h=600&fit=crop&auto=format",
    tags: ["GF"],
    allergens: ["dairy", "sulphites"],
    badge: "Chef's Selection",
    popular: true,
    modifiers: [
      {
        id: "doneness",
        label: "Doneness",
        required: true,
        multi: false,
        options: [
          { id: "rare", label: "Rare" },
          { id: "medium-rare", label: "Medium Rare" },
          { id: "medium", label: "Medium" },
          { id: "well-done", label: "Well Done" },
        ],
      },
      {
        id: "sauce",
        label: "Sauce",
        required: true,
        multi: false,
        options: [
          { id: "truffle-jus", label: "Truffle Jus (included)" },
          { id: "bearnaise", label: "Béarnaise" },
          { id: "pepper", label: "Pepper Sauce" },
        ],
      },
      {
        id: "side",
        label: "Side",
        required: false,
        multi: false,
        options: [
          { id: "truffle-fries", label: "Truffle Fries", priceAddon: 45 },
          { id: "vegetables", label: "Seasonal Vegetables" },
          { id: "mash", label: "Truffle Mashed Potato" },
        ],
      },
    ],
  },
  {
    id: "m2",
    category: "mains",
    name: "Mediterranean Sea Bass",
    desc: "Saffron velouté, fennel salad, lemon caper butter",
    longDesc: "Whole Mediterranean sea bass, line-caught and pan-roasted until crisp. Served on a bed of saffron velouté with shaved fennel salad and warm lemon-caper butter sauce.",
    price: 420,
    img: "https://images.unsplash.com/photo-1676471926534-d5c9771909fa?w=800&h=600&fit=crop&auto=format",
    tags: ["GF"],
    allergens: ["fish", "dairy", "sulphites"],
    popular: true,
    modifiers: [
      {
        id: "prep",
        label: "Preparation",
        required: true,
        multi: false,
        options: [
          { id: "pan-seared", label: "Pan-Seared" },
          { id: "grilled", label: "Wood Grilled" },
          { id: "steamed", label: "Lightly Steamed" },
        ],
      },
      {
        id: "extra",
        label: "Extras",
        required: false,
        multi: true,
        options: [
          { id: "extra-sauce", label: "Extra Sauce", priceAddon: 20 },
          { id: "side-salad", label: "Side Salad", priceAddon: 35 },
        ],
      },
    ],
  },
  {
    id: "m3",
    category: "mains",
    name: "Herb-Crusted Lamb",
    desc: "Frenched rack, rosemary jus, root vegetable purée",
    longDesc: "Three-bone rack of Anatolian lamb with a Dijon and herb crust, slow-roasted and rested. Accompanied by silky root vegetable purée and a rosemary reduction.",
    price: 520,
    img: "https://images.unsplash.com/photo-1785502108275-3fa68c2ed7bd?w=800&h=600&fit=crop&auto=format",
    tags: ["GF"],
    allergens: ["dairy", "gluten", "sulphites"],
    modifiers: [
      {
        id: "doneness",
        label: "Doneness",
        required: true,
        multi: false,
        options: [
          { id: "pink", label: "Pink (recommended)" },
          { id: "medium", label: "Medium" },
          { id: "well", label: "Well Done" },
        ],
      },
    ],
  },
  {
    id: "m4",
    category: "mains",
    name: "Black Truffle Risotto",
    desc: "Arborio, 36-month Parmigiano, fresh black truffle",
    longDesc: "Hand-stirred Arborio risotto finished with cold butter and 36-month aged Parmigiano Reggiano. Generously shaved with fresh Périgord black truffle at the table.",
    price: 340,
    img: "https://images.unsplash.com/photo-1643879397174-4f10ac503566?w=800&h=600&fit=crop&auto=format",
    tags: ["V", "GF"],
    allergens: ["dairy"],
    modifiers: [
      {
        id: "truffle",
        label: "Truffle",
        required: false,
        multi: false,
        options: [
          { id: "standard", label: "Standard shaving (included)" },
          { id: "extra", label: "Extra truffle", priceAddon: 80 },
        ],
      },
    ],
  },
  {
    id: "d1",
    category: "desserts",
    name: "Chocolate Fondant",
    desc: "Valrhona 70%, salted caramel core, vanilla gelato",
    longDesc: "Valrhona 70% dark chocolate fondant with a liquid salted caramel centre. Served warm with house-made vanilla bean gelato and Maldon sea salt flakes.",
    price: 165,
    img: "https://images.unsplash.com/photo-1626263468007-a9e0cf83f1ac?w=800&h=600&fit=crop&auto=format",
    tags: ["V"],
    allergens: ["gluten", "dairy", "eggs", "nuts"],
    popular: true,
    modifiers: [
      {
        id: "gelato",
        label: "Gelato",
        required: false,
        multi: false,
        options: [
          { id: "vanilla", label: "Vanilla Bean (included)" },
          { id: "pistachio", label: "Pistachio" },
          { id: "none", label: "No gelato" },
        ],
      },
    ],
  },
  {
    id: "dr1",
    category: "drinks",
    name: "Signature Negroni",
    desc: "Gin, Campari, vermouth, orange peel, single ice sphere",
    longDesc: "Our take on the classic — Hendrick's gin, Campari, and Martini Rosso stirred to perfection and served over a hand-carved ice sphere. Garnished with flamed orange peel.",
    price: 145,
    img: "https://images.unsplash.com/photo-1500217052183-bc01eee1a74e?w=800&h=600&fit=crop&auto=format",
    tags: ["VG", "GF"],
    allergens: ["sulphites"],
    badge: "Popular",
    modifiers: [
      {
        id: "strength",
        label: "Strength",
        required: false,
        multi: false,
        options: [
          { id: "classic", label: "Classic" },
          { id: "light", label: "Light on spirits" },
        ],
      },
    ],
  },
  {
    id: "dr2",
    category: "drinks",
    name: "Rose Aperitivo Spritz",
    desc: "Aperol, rosé prosecco, elderflower, grapefruit",
    longDesc: "A house-favourite aperitivo: Aperol and elderflower cordial topped with chilled rosé prosecco, fresh grapefruit juice, and a sprig of rosemary.",
    price: 120,
    img: "https://images.unsplash.com/photo-1605270012917-bf157c5a9541?w=800&h=600&fit=crop&auto=format",
    tags: ["VG", "GF"],
    allergens: ["sulphites"],
    modifiers: [],
  },
];

export const ALLERGEN_LABELS: Record<AllergenKey, string> = {
  gluten: "Gluten",
  dairy: "Dairy",
  eggs: "Eggs",
  nuts: "Tree Nuts",
  shellfish: "Shellfish",
  fish: "Fish",
  soy: "Soy",
  sulphites: "Sulphites",
};

export const DIETARY_LABELS: Record<DietaryTag, { label: string; color: string; bg: string }> = {
  V: { label: "Vegetarian", color: "#166534", bg: "#DCFCE7" },
  VG: { label: "Vegan", color: "#14532D", bg: "#BBF7D0" },
  GF: { label: "Gluten-free", color: "#854D0E", bg: "#FEF9C3" },
  DF: { label: "Dairy-free", color: "#1D4ED8", bg: "#DBEAFE" },
};
