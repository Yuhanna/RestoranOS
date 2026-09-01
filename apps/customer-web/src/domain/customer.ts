export type Locale = "tr" | "en";
export type Money = Readonly<{ amountMinor: number; currency: "TRY" }>;

export interface PriceBreakdown {
  list: Money;
  discount: Money;
  final: Money;
}

export type {
  AllergenKey,
  DietaryFilterKey,
  DietaryTag,
  ProductBadge,
  ProductNutrition,
  SpiceLevel,
} from "./menuCatalog";
import type {
  AllergenKey,
  DietaryFilterKey,
  DietaryTag,
  ProductBadge,
  ProductNutrition,
  SpiceLevel,
} from "./menuCatalog";
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
  /** 1 = tek seçim (radio). >1 = çoklu eklenti. */
  maxSelections?: number;
  options: ModifierOption[];
}

export interface ProductPortion {
  id: string;
  name: string;
  priceMultiplier: number;
}

export interface Product {
  id: string;
  categoryId: string;
  name: string;
  description: string;
  price: Money;
  listPrice?: Money;
  discount?: Money;
  promotionLabel?: string;
  imageUrl: string;
  imageAlt: string;
  available: boolean;
  badge?: ProductBadge;
  /** Yeni ürün rozeti — badge yoksa otomatik "Yeni" gösterilir. */
  isNew?: boolean;
  dietaryTags: DietaryTag[];
  /** Yapılandırılmış alerjen kodları (EU 14). */
  allergenKeys: AllergenKey[];
  /** Paylaşılan mutfak / cross-contact uyarısı. */
  mayContainAllergenKeys?: AllergenKey[];
  /** Kısa malzeme listesi (FIC / ADDE best practice). */
  ingredients?: string[];
  modifierGroups: ModifierGroup[];
  portions?: ProductPortion[];
  nutrition?: ProductNutrition;
  spiceLevel?: SpiceLevel;
  prepTimeMinutes?: number;
  containsAlcohol?: boolean;
  /** Örn. "2 kişilik", "Paylaşımlık tabak". */
  servingNote?: string;
  /** Pazar fiyatı gibi metin fiyat etiketleri. */
  priceLabel?: string;
  certificationNotes?: string;
}

export interface CustomerMenuSettings {
  showDietaryFilters: boolean;
  dietaryFilterOptions: DietaryFilterKey[];
  showAllergenExclusions: boolean;
  allergenExclusionOptions: AllergenKey[];
  allergenDisclaimer?: string;
  allergenMatrixUrl?: string;
}

export interface CustomerSession {
  sessionToken: string;
  restaurantName: string;
  branchName: string;
  tableLabel: string;
  locale: Locale;
  categories: ReadonlyArray<{ id: string; name: string }>;
  products: ReadonlyArray<Product>;
  customerMenu?: CustomerMenuSettings;
  openServiceRequestTypes?: ReadonlyArray<ServiceRequestType | string>;
  /** Open orders already on this table (survives refresh / QR re-scan). */
  activeOrders?: ReadonlyArray<Order>;
}

export interface CartLine {
  key: string;
  product: Product;
  quantity: number;
  selections: Record<string, string[]>;
  portionId?: string;
  note: string;
}

export interface Order {
  id: string;
  displayNumber: string;
  status: OrderStatus;
  statusChangedAt: string;
  estimatedReadyAt: string;
  total: Money;
  subtotal?: Money;
  discount?: Money;
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
