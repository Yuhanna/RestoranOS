import {
  CustomerGatewayError,
  type CustomerGateway,
  type CustomerSession,
  type Money,
  type Product,
  type SubmitOrderRequest,
} from "../domain/customer";

const tryMoney = (amountMinor: number): Money => ({ amountMinor, currency: "TRY" });

const products: Product[] = [
  {
    id: "m1",
    categoryId: "mains",
    name: "Izgara Levrek",
    description: "Safran velouté, rezene salatası ve limonlu kapari tereyağı",
    price: tryMoney(42_000),
    imageUrl:
      "https://images.unsplash.com/photo-1676471926534-d5c9771909fa?w=900&h=700&fit=crop&auto=format",
    imageAlt: "Rezene ve yeşilliklerle servis edilen ızgara levrek",
    available: true,
    badge: "Çok sevilen",
    dietaryTags: ["glutenFree"],
    allergens: ["Balık", "Süt ürünü"],
    modifierGroups: [
      {
        id: "preparation",
        name: "Pişirme şekli",
        required: true,
        options: [
          { id: "pan", name: "Tavada", priceDelta: tryMoney(0) },
          { id: "grill", name: "Odun ateşinde", priceDelta: tryMoney(0) },
        ],
      },
    ],
  },
  {
    id: "m2",
    categoryId: "mains",
    name: "Trüflü Risotto",
    description: "Arborio pirinci, 36 aylık parmesan ve taze siyah trüf",
    price: tryMoney(34_000),
    imageUrl:
      "https://images.unsplash.com/photo-1643879397174-4f10ac503566?w=900&h=700&fit=crop&auto=format",
    imageAlt: "Parmesan ve siyah trüfle tamamlanmış kremalı risotto",
    available: true,
    dietaryTags: ["vegetarian", "glutenFree"],
    allergens: ["Süt ürünü"],
    modifierGroups: [
      {
        id: "truffle",
        name: "Trüf miktarı",
        required: false,
        options: [
          { id: "standard", name: "Standart", priceDelta: tryMoney(0) },
          { id: "extra", name: "Ekstra trüf", priceDelta: tryMoney(8_000) },
        ],
      },
    ],
  },
  {
    id: "s1",
    categoryId: "starters",
    name: "Ege Otları",
    description: "Mevsim otları, turunç vinaigrette ve kavrulmuş badem",
    price: tryMoney(18_500),
    imageUrl:
      "https://images.unsplash.com/photo-1540420773420-3366772f4999?w=900&h=700&fit=crop&auto=format",
    imageAlt: "Turunç ve bademle hazırlanmış yeşil Ege otları salatası",
    available: true,
    badge: "Şefin seçimi",
    dietaryTags: ["vegan", "glutenFree"],
    allergens: ["Badem"],
    modifierGroups: [],
  },
  {
    id: "d1",
    categoryId: "desserts",
    name: "Çikolatalı Fondan",
    description: "Bitter çikolata, tuzlu karamel ve vanilyalı dondurma",
    price: tryMoney(16_500),
    imageUrl:
      "https://images.unsplash.com/photo-1626263468007-a9e0cf83f1ac?w=900&h=700&fit=crop&auto=format",
    imageAlt: "Vanilyalı dondurmayla sunulan akışkan çikolatalı fondan",
    available: true,
    dietaryTags: ["vegetarian"],
    allergens: ["Gluten", "Süt ürünü", "Yumurta"],
    modifierGroups: [],
  },
  {
    id: "b1",
    categoryId: "drinks",
    name: "Gül & Greyfurt",
    description: "Gül şurubu, pembe greyfurt, soda ve taze biberiye",
    price: tryMoney(12_000),
    imageUrl:
      "https://images.unsplash.com/photo-1605270012917-bf157c5a9541?w=900&h=700&fit=crop&auto=format",
    imageAlt: "Greyfurt dilimi ve biberiyeyle servis edilen pembe içecek",
    available: false,
    dietaryTags: ["vegan", "glutenFree"],
    allergens: [],
    modifierGroups: [],
  },
];

const session: CustomerSession = {
  sessionToken: "mock-session-7",
  restaurantName: "Marea",
  branchName: "Nişantaşı",
  tableLabel: "Masa 7",
  locale: "tr",
  categories: [
    { id: "all", name: "Tümü" },
    { id: "starters", name: "Başlangıçlar" },
    { id: "mains", name: "Ana yemekler" },
    { id: "desserts", name: "Tatlılar" },
    { id: "drinks", name: "İçecekler" },
  ],
  products,
  openServiceRequestTypes: [],
};

const openServiceRequests = new Set<string>();
const submittedOrders = new Map<string, Awaited<ReturnType<CustomerGateway["submitOrder"]>>>();

const delay = (signal?: AbortSignal) =>
  new Promise<void>((resolve, reject) => {
    const timer = window.setTimeout(resolve, 80);
    signal?.addEventListener(
      "abort",
      () => {
        window.clearTimeout(timer);
        reject(new DOMException("Aborted", "AbortError"));
      },
      { once: true },
    );
  });

export const DEMO_QR_TOKEN = "demo-marea-table-7";

export const mockCustomerGateway: CustomerGateway = {
  async resolveQr(qrToken, signal) {
    await delay(signal);
    if (qrToken !== DEMO_QR_TOKEN) {
      throw new CustomerGatewayError("INVALID_QR", "QR token is invalid");
    }
    const activeOrders = [...submittedOrders.values()].filter(
      (order) => order.status !== "completed" && order.status !== "cancelled",
    );
    return {
      ...structuredClone(session),
      sessionToken: `mock-session-${crypto.randomUUID()}`,
      activeOrders: structuredClone(activeOrders),
      openServiceRequestTypes: [...openServiceRequests],
    };
  },

  async submitOrder(request: SubmitOrderRequest, signal) {
    await delay(signal);
    const existing = submittedOrders.get(request.idempotencyKey);
    if (existing) return existing;

    const totalMinor = request.lines.reduce((total, line) => {
      const product = products.find((item) => item.id === line.productId);
      if (!product?.available) {
        throw new CustomerGatewayError("ORDER_REJECTED", "Product is unavailable");
      }
      const modifiers = product.modifierGroups.flatMap((group) => group.options);
      const optionTotal = line.modifierOptionIds.reduce(
        (sum, optionId) =>
          sum + (modifiers.find((option) => option.id === optionId)?.priceDelta.amountMinor ?? 0),
        0,
      );
      return total + (product.price.amountMinor + optionTotal) * line.quantity;
    }, 0);

    const order = {
      id: crypto.randomUUID(),
      displayNumber: "#1042",
      status: "submitted" as const,
      statusChangedAt: new Date().toISOString(),
      estimatedReadyAt: new Date(Date.now() + 18 * 60_000).toISOString(),
      total: tryMoney(totalMinor),
    };
    submittedOrders.set(request.idempotencyKey, order);
    return order;
  },

  async getOrder(orderId, _sessionToken, signal) {
    await delay(signal);
    const order = [...submittedOrders.values()].find((item) => item.id === orderId);
    if (!order) throw new CustomerGatewayError("ORDER_REJECTED", "Order was not found");
    return structuredClone(order);
  },

  async createServiceRequest(sessionToken, type, note, signal) {
    await delay(signal);
    if (!sessionToken) throw new CustomerGatewayError("ORDER_REJECTED", "Session required");
    if (openServiceRequests.has(type)) {
      throw new CustomerGatewayError("SERVICE_REQUEST_OPEN", "Already open");
    }
    openServiceRequests.add(type);
    return {
      id: crypto.randomUUID(),
      tableId: "table-1",
      tableLabel: "Masa 1",
      type,
      status: "open" as const,
      note: note ?? null,
      createdAtUtc: new Date().toISOString(),
    };
  },

  async watchOrder() {
    return async () => {};
  },
};
