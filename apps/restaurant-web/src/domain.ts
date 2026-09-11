export type OrderStatus =
  | "submitted"
  | "accepted"
  | "preparing"
  | "ready"
  | "served"
  | "completed"
  | "cancelled";

export type Session = {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  tenantId: string;
  branchId: string;
};

export type LoginInput = {
  email: string;
  password: string;
  tenantId: string;
  branchId: string;
};

export type Order = {
  id: string;
  displayNumber: string;
  status: OrderStatus;
  createdAtUtc: string | null;
  statusChangedAtUtc: string;
  estimatedReadyAtUtc: string;
  amountMinor: number;
  currency: string;
  tableId: string;
  tableLabel: string;
};

export type TodayBranchStat = {
  branchId: string;
  branchName: string;
  todaysOrderCount: number;
  todaysRevenueMinor: number;
  openTablesCount: number;
  pendingOrdersCount: number;
  completedOrdersTodayCount: number;
  isFrozen: boolean;
};

export type TodayDashboard = {
  todaysOrderCount: number;
  todaysRevenueMinor: number;
  openTablesCount: number;
  pendingOrdersCount: number;
  completedOrdersTodayCount: number;
  currency: string;
  dayStartUtc: string;
  dayEndUtc: string;
  canViewFinancials?: boolean;
  scope?: "branch" | "network" | string;
  branchCount?: number;
  branches?: TodayBranchStat[];
};

export type OrderLine = {
  id: string;
  menuItemId: string;
  name: string;
  quantity: number;
  listUnitPriceAmountMinor: number;
  discountUnitAmountMinor: number;
  unitPriceAmountMinor: number;
  currency: string;
  note: string | null;
};

export type OrderStatusHistoryEntry = {
  status: OrderStatus | string;
  changedAtUtc: string;
  changedByUserId: string | null;
};

export type OrderDetail = {
  id: string;
  displayNumber: string;
  status: OrderStatus;
  createdAtUtc: string;
  statusChangedAtUtc: string;
  estimatedReadyAtUtc: string;
  tableId: string;
  tableLabel: string;
  subtotalAmountMinor: number;
  discountAmountMinor: number;
  totalAmountMinor: number;
  currency: string;
  items: OrderLine[];
  statusHistory: OrderStatusHistoryEntry[];
};

export type ServiceRequestType = "waiter" | "bill" | string;

export type ServiceRequest = {
  id: string;
  tableId: string;
  tableLabel: string;
  type: ServiceRequestType;
  status: string;
  note: string | null;
  createdAtUtc: string;
  completedAtUtc: string | null;
};

export type WorkspaceEntitlements = {
  planCode: string;
  planDisplayName: string;
  isTrial: boolean;
  trialEndsAtUtc: string | null;
  tableCount?: number;
  maxTablesPerBranch?: number | null;
  activeQrCount?: number;
  maxActiveQrCodes?: number | null;
  maxConcurrentLiveSessions?: number | null;
  canUseLiveOrderPanel?: boolean;
  canUsePromotions?: boolean;
  canUseAnalytics?: boolean;
  canUseMenuThemes?: boolean;
  canUseMultiBranch?: boolean;
  canUseBrandWatermark?: boolean;
  canViewFinancialAnalytics?: boolean;
  warnings?: string[];
};

export type AudienceNotification = {
  id: string;
  title: string;
  body: string;
  actionUrl?: string | null;
};

export type SubscriptionOffer = {
  id: string;
  targetPlanCode: string;
  discountPercent: number;
  durationMonths: number;
  title: string;
  body: string;
};

export type Workspace = {
  tenantId: string;
  restaurantId: string;
  restaurantName: string;
  branchId: string;
  branchName: string;
  entitlements: WorkspaceEntitlements | null;
  permissions?: string[];
  canViewFinancialAnalytics?: boolean;
  notifications?: AudienceNotification[];
  subscriptionOffers?: SubscriptionOffer[];
  brandLogoUrl?: string | null;
  brandLogoAlt?: string | null;
  showBrandWatermark?: boolean;
  brandWatermarkIntensity?: string;
};

export type AnalyticsSummary = {
  grossSalesMinor: number;
  estimatedCostMinor: number | null;
  grossProfitMinor: number | null;
  cancelledSalesMinor: number;
  completedOrderCount: number;
  cancelledOrderCount: number;
  openServiceRequestCount: number;
  averageTicketMinor: number;
  currency: string;
  canViewFinancials: boolean;
};

export type SalesPeriod = {
  periodStartUtc: string;
  grossSalesMinor: number;
  grossProfitMinor: number | null;
  orderCount: number;
};

export type TopItem = {
  menuItemId: string;
  name: string;
  quantitySold: number;
  revenueMinor: number;
  estimatedCostMinor: number | null;
  grossProfitMinor: number | null;
};

export type ProblemDetails = {
  status?: number;
  title?: string;
  detail?: string;
  code?: string;
};

export type DiningTable = {
  id: string;
  label: string;
  isActive: boolean;
  activeQrCount: number;
  operationalStatus: "available" | "occupied" | "has_pending_order" | string;
};

export type QrCodeStatus = "active" | "inactive" | "revoked";

export type TableQrCode = {
  id: string;
  tableId: string;
  status: QrCodeStatus;
  createdAtUtc: string;
  revokedAtUtc: string | null;
};

export type GeneratedQr = TableQrCode & {
  tableLabel: string;
  token: string;
  entryUrl: string;
  svgMarkup: string;
};

export type QrPrint = {
  id: string;
  tableId: string;
  tableLabel: string;
  status: QrCodeStatus;
  entryUrl: string;
  svgMarkup: string;
};

export type MenuLifecycle = "draft" | "published" | "archived";

export type MenuSummary = {
  id: string;
  name: string;
  lifecycle: MenuLifecycle;
  publishedAtUtc: string | null;
  categoryCount: number;
  itemCount: number;
};

export type MenuTextTranslation = {
  locale: string;
  name: string;
  description: string;
};

export type MenuCategoryRecord = {
  id: string;
  menuId: string;
  name: string;
  sortOrder: number;
  translations: MenuTextTranslation[];
};

export type MenuProductRecord = {
  id: string;
  menuId: string;
  categoryId: string;
  name: string;
  description: string;
  amountMinor: number;
  currency: string;
  isAvailable: boolean;
  sortOrder: number;
  translations: MenuTextTranslation[];
};

export type MenuDetail = MenuSummary & {
  categories: MenuCategoryRecord[];
  items: MenuProductRecord[];
};

export type MenuPromotionScope = "all_menu" | "category" | "product";
export type MenuPromotionDiscountKind = "percent" | "fixed_minor";
export type SubscriptionAudience = "all" | "free" | "pro" | "trial" | "non_pro";

export type MenuPromotion = {
  id: string;
  name: string;
  scope: MenuPromotionScope;
  discountKind: MenuPromotionDiscountKind;
  discountValue: number;
  startsAtUtc: string;
  endsAtUtc: string | null;
  dailyStartLocal: string | null;
  dailyEndLocal: string | null;
  categoryId: string | null;
  menuItemId: string | null;
  isActive: boolean;
  /** Bitmask: bit0=Sun … bit6=Sat. Null = every day. */
  daysOfWeekMask?: number | null;
};

export type CreateMenuPromotionInput = {
  name: string;
  scope: MenuPromotionScope;
  discountKind: MenuPromotionDiscountKind;
  discountValue: number;
  startsAtUtc: string;
  endsAtUtc?: string | null;
  dailyStartLocal?: string | null;
  dailyEndLocal?: string | null;
  categoryId?: string | null;
  menuItemId?: string | null;
  isActive?: boolean;
  daysOfWeekMask?: number | null;
};

export type ManagedNotification = {
  id: string;
  tenantId: string | null;
  audience: SubscriptionAudience | string;
  title: string;
  body: string;
  actionUrl: string | null;
  startsAtUtc: string;
  endsAtUtc: string | null;
  isActive: boolean;
  lastDispatchedAtUtc: string | null;
};

export type CreateNotificationInput = {
  audience: SubscriptionAudience | string;
  title: string;
  body: string;
  startsAtUtc: string;
  endsAtUtc?: string | null;
  actionUrl?: string | null;
  isActive?: boolean;
  broadcastToAllTenants?: boolean;
};

export type NotificationDispatchResult = {
  emailSentCount: number;
  pushSentCount: number;
  recipientEmails: string[];
};

export const nextStatuses: Record<OrderStatus, OrderStatus[]> = {
  submitted: ["accepted", "preparing", "ready", "served", "cancelled"],
  accepted: ["preparing", "ready", "served", "cancelled"],
  preparing: ["ready", "served", "cancelled"],
  ready: ["served"],
  served: [],
  completed: [],
  cancelled: [],
};
