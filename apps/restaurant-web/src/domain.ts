export type OrderStatus =
  | "submitted"
  | "accepted"
  | "preparing"
  | "ready"
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

export const nextStatuses: Record<OrderStatus, OrderStatus[]> = {
  submitted: ["accepted", "preparing", "ready", "completed", "cancelled"],
  accepted: ["preparing", "ready", "completed", "cancelled"],
  preparing: ["ready", "completed", "cancelled"],
  ready: ["completed"],
  completed: [],
  cancelled: [],
};
