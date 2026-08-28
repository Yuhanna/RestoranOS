export type Locale = "tr" | "en";
export type Money = Readonly<{ amountMinor: number; currency: "TRY" }>;
export type DietaryTag = "vegetarian" | "vegan" | "glutenFree";
export type OrderStatus =
  | "submitted"
  | "accepted"
  | "preparing"
  | "ready"
  | "completed"
  | "cancelled";

export type ServiceRequestType =
  | "waiter"
  | "bill"
  | "water"
  | "cutlery"
  | "napkin"
  | "other";

export interface ServiceRequest {
  id: string;
  tableId: string;
  tableLabel: string;
  type: ServiceRequestType;
  status: "open" | "completed" | "cancelled";
  note?: string | null;
  createdAtUtc: string;
}

export interface ModifierOption {
  id: string;
  name: string;
  priceDelta: Money;
}

export interface ModifierGroup {
  id: string;
  name: string;
  required: boolean;
  options: ModifierOption[];
}

export interface Product {
  id: string;
  categoryId: string;
  name: string;
  description: string;
  price: Money;
  imageUrl: string;
  imageAlt: string;
  available: boolean;
  badge?: string;
  dietaryTags: DietaryTag[];
  allergens: string[];
  modifierGroups: ModifierGroup[];
}

export interface CustomerSession {
  sessionToken: string;
  restaurantName: string;
  branchName: string;
  tableLabel: string;
  locale: Locale;
  categories: ReadonlyArray<{ id: string; name: string }>;
  products: ReadonlyArray<Product>;
  openServiceRequestTypes?: ReadonlyArray<ServiceRequestType | string>;
  /** Open orders already on this table (survives refresh / QR re-scan). */
  activeOrders?: ReadonlyArray<Order>;
}

export interface CartLine {
  key: string;
  product: Product;
  quantity: number;
  selections: Record<string, string>;
  note: string;
}

export interface Order {
  id: string;
  displayNumber: string;
  status: OrderStatus;
  statusChangedAt: string;
  estimatedReadyAt: string;
  total: Money;
}

export interface SubmitOrderRequest {
  sessionToken: string;
  idempotencyKey: string;
  lines: ReadonlyArray<{
    productId: string;
    quantity: number;
    modifierOptionIds: string[];
    note?: string;
  }>;
}

export interface CustomerGateway {
  resolveQr(qrToken: string, signal?: AbortSignal, locale?: Locale): Promise<CustomerSession>;
  submitOrder(request: SubmitOrderRequest, signal?: AbortSignal): Promise<Order>;
  getOrder(orderId: string, sessionToken: string, signal?: AbortSignal): Promise<Order>;
  createServiceRequest(
    sessionToken: string,
    type: ServiceRequestType,
    note?: string,
    signal?: AbortSignal,
  ): Promise<ServiceRequest>;
  watchOrder(
    orderId: string,
    sessionToken: string,
    onOrder: (order: Order) => void,
    onError: () => void,
  ): Promise<() => Promise<void>>;
}

export class CustomerGatewayError extends Error {
  constructor(
    public readonly code:
      | "INVALID_QR"
      | "MENU_UNAVAILABLE"
      | "NETWORK"
      | "ORDER_REJECTED"
      | "SERVICE_REQUEST_OPEN",
    message: string,
  ) {
    super(message);
    this.name = "CustomerGatewayError";
  }
}
