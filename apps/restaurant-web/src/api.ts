import type {
  AnalyticsSummary,
  CloseTableCheckInput,
  CreateMenuPromotionInput,
  CreateNotificationInput,
  DiningTable,
  GeneratedQr,
  LoginInput,
  ManagedNotification,
  MenuDetail,
  MenuPromotion,
  MenuSummary,
  NotificationDispatchResult,
  Order,
  OrderDetail,
  OrderStatus,
  ProblemDetails,
  QrPrint,
  SalesPeriod,
  ServiceRequest,
  Session,
  TableCheck,
  TableCheckCloseResult,
  TableQrCode,
  TopItem,
  TodayDashboard,
  Workspace,
} from "./domain";

const fallbackMessages: Record<number, string> = {
  401: "Oturumunuz sona erdi. Lütfen yeniden giriş yapın.",
  403: "Bu işlem için yetkiniz bulunmuyor.",
  409: "Sipariş başka bir ekranda güncellendi. Liste yenilendi.",
  429: "Çok fazla istek gönderildi. Kısa süre sonra tekrar deneyin.",
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
    public readonly retryAfterSeconds?: number,
  ) {
    super(message);
  }
}

type FetchLike = typeof fetch;

export class ManagementApi {
  private session: Session | null = null;
  private refreshInFlight: Promise<Session> | null = null;

  constructor(
    private readonly baseUrl: string,
    private readonly fetcher: FetchLike = fetch.bind(globalThis),
  ) {}

  getSession = () => this.session;

  async login(input: LoginInput): Promise<Session> {
    const response = await this.fetcher(this.url("/api/v1/management/auth/login"), {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
    if (!response.ok) throw await this.errorFor(response);
    this.session = (await response.json()) as Session;
    return this.session;
  }

  async restore(): Promise<Session> {
    return this.refresh();
  }

  async logout(): Promise<void> {
    try {
      await this.fetcher(this.url("/api/v1/management/auth/logout"), {
        method: "POST",
        credentials: "include",
      });
    } finally {
      this.session = null;
    }
  }

  async getActiveOrders(signal?: AbortSignal): Promise<Order[]> {
    return this.authorized<Order[]>("/api/v1/management/orders/active", { signal });
  }

  async getTodayDashboard(signal?: AbortSignal): Promise<TodayDashboard> {
    return this.authorized<TodayDashboard>("/api/v1/management/dashboard/today", { signal });
  }

  async getOrder(orderId: string, signal?: AbortSignal): Promise<OrderDetail> {
    return this.authorized<OrderDetail>(`/api/v1/management/orders/${orderId}`, { signal });
  }

  async changeStatus(order: Order, status: OrderStatus): Promise<Order> {
    return this.authorized<Order>(`/api/v1/management/orders/${order.id}/status`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        status,
        expectedStatusChangedAtUtc: order.statusChangedAtUtc,
      }),
    });
  }

  async getTableCheck(tableId: string, signal?: AbortSignal): Promise<TableCheck> {
    const check = await this.authorized<TableCheck>(`/api/v1/management/tables/${tableId}/check`, { signal });
    return {
      ...check,
      rounds: (check.rounds ?? []).map((round) => ({
        ...round,
        items: round.items ?? [],
      })),
    };
  }

  async closeTableCheck(tableId: string, input: CloseTableCheckInput): Promise<TableCheckCloseResult> {
    return this.authorized<TableCheckCloseResult>(`/api/v1/management/tables/${tableId}/close-check`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        tender: input.tender,
        confirmIncompleteKitchen: Boolean(input.confirmIncompleteKitchen),
        note: input.note?.trim() || null,
      }),
    });
  }

  async listTables(): Promise<DiningTable[]> {
    return this.authorized<DiningTable[]>("/api/v1/management/tables");
  }

  async createTable(label: string): Promise<DiningTable> {
    return this.authorized<DiningTable>("/api/v1/management/tables", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ label }),
    });
  }

  async updateTable(tableId: string, patch: { label?: string; isActive?: boolean }): Promise<DiningTable> {
    return this.authorized<DiningTable>(`/api/v1/management/tables/${tableId}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(patch),
    });
  }

  async listQrCodes(tableId: string): Promise<TableQrCode[]> {
    return this.authorized<TableQrCode[]>(`/api/v1/management/tables/${tableId}/qr-codes`);
  }

  async generateQr(tableId: string): Promise<GeneratedQr> {
    return this.authorized<GeneratedQr>(`/api/v1/management/tables/${tableId}/qr-codes`, {
      method: "POST",
    });
  }

  async changeQrStatus(qrCodeId: string, action: "activate" | "deactivate" | "revoke"): Promise<TableQrCode> {
    return this.authorized<TableQrCode>(`/api/v1/management/qr-codes/${qrCodeId}/${action}`, {
      method: "POST",
    });
  }

  async printQr(qrCodeId: string): Promise<QrPrint> {
    return this.authorized<QrPrint>(`/api/v1/management/qr-codes/${qrCodeId}/print`);
  }

  async listMenus(): Promise<MenuSummary[]> {
    return this.authorized<MenuSummary[]>("/api/v1/management/menus");
  }

  async getMenu(menuId: string): Promise<MenuDetail> {
    return this.authorized<MenuDetail>(`/api/v1/management/menus/${menuId}`);
  }

  async createMenu(name: string): Promise<MenuSummary> {
    return this.authorized<MenuSummary>("/api/v1/management/menus", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name }),
    });
  }

  async publishMenu(menuId: string): Promise<MenuSummary> {
    return this.authorized<MenuSummary>(`/api/v1/management/menus/${menuId}/publish`, { method: "POST" });
  }

  async unpublishMenu(menuId: string): Promise<MenuSummary> {
    return this.authorized<MenuSummary>(`/api/v1/management/menus/${menuId}/unpublish`, { method: "POST" });
  }

  async archiveMenu(menuId: string): Promise<MenuSummary> {
    return this.authorized<MenuSummary>(`/api/v1/management/menus/${menuId}/archive`, { method: "POST" });
  }

  async addCategory(menuId: string, name: string, sortOrder: number) {
    return this.authorized(`/api/v1/management/menus/${menuId}/categories`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name, sortOrder }),
    });
  }

  async addItem(
    menuId: string,
    input: {
      categoryId: string;
      name: string;
      description: string;
      amountMinor: number;
      isAvailable: boolean;
      sortOrder: number;
    },
  ) {
    return this.authorized(`/api/v1/management/menus/${menuId}/items`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  }

  async updateItemAvailability(itemId: string, isAvailable: boolean) {
    return this.authorized(`/api/v1/management/menu-items/${itemId}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ isAvailable }),
    });
  }

  async upsertItemTranslation(itemId: string, locale: string, name: string, description = "") {
    return this.authorized(`/api/v1/management/menu-items/${itemId}/translations/${locale}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ name, description }),
    });
  }

  async getWorkspace(): Promise<Workspace> {
    return this.authorized<Workspace>("/api/v1/management/workspace");
  }

  async listOpenServiceRequests(): Promise<ServiceRequest[]> {
    return this.authorized<ServiceRequest[]>("/api/v1/management/service-requests/open");
  }

  async completeServiceRequest(requestId: string): Promise<ServiceRequest> {
    return this.authorized<ServiceRequest>(`/api/v1/management/service-requests/${requestId}/complete`, {
      method: "POST",
    });
  }

  async checkoutPlan(planCode: "Pro"): Promise<Workspace["entitlements"]> {
    return this.authorized("/api/v1/management/subscription/checkout", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ planCode }),
    });
  }

  async getAnalyticsSummary(fromUtc?: string, toUtc?: string): Promise<AnalyticsSummary> {
    const params = new URLSearchParams();
    if (fromUtc) params.set("fromUtc", fromUtc);
    if (toUtc) params.set("toUtc", toUtc);
    const query = params.toString();
    return this.authorized<AnalyticsSummary>(
      `/api/v1/management/analytics/summary${query ? `?${query}` : ""}`,
    );
  }

  async getSalesByPeriod(fromUtc?: string, toUtc?: string, granularity = "day"): Promise<SalesPeriod[]> {
    const params = new URLSearchParams({ granularity });
    if (fromUtc) params.set("fromUtc", fromUtc);
    if (toUtc) params.set("toUtc", toUtc);
    return this.authorized<SalesPeriod[]>(`/api/v1/management/analytics/sales-by-period?${params}`);
  }

  async getTopItems(fromUtc?: string, toUtc?: string, limit = 10): Promise<TopItem[]> {
    const params = new URLSearchParams({ limit: String(limit) });
    if (fromUtc) params.set("fromUtc", fromUtc);
    if (toUtc) params.set("toUtc", toUtc);
    return this.authorized<TopItem[]>(`/api/v1/management/analytics/top-items?${params}`);
  }

  async listMenuPromotions(): Promise<MenuPromotion[]> {
    return this.authorized<MenuPromotion[]>("/api/v1/management/promotions/menu");
  }

  async createMenuPromotion(input: CreateMenuPromotionInput) {
    return this.authorized<MenuPromotion>("/api/v1/management/promotions/menu", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  }

  async updateMenuPromotion(
    promotionId: string,
    input: { name?: string; isActive?: boolean; endsAtUtc?: string | null },
  ) {
    return this.authorized<MenuPromotion>(`/api/v1/management/promotions/menu/${promotionId}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  }

  async listManagedNotifications(): Promise<ManagedNotification[]> {
    return this.authorized<ManagedNotification[]>("/api/v1/management/notifications");
  }

  async createManagedNotification(input: CreateNotificationInput) {
    return this.authorized<ManagedNotification>("/api/v1/management/notifications", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  }

  async dispatchNotification(notificationId: string): Promise<NotificationDispatchResult> {
    return this.authorized<NotificationDispatchResult>(
      `/api/v1/management/notifications/${notificationId}/dispatch`,
      { method: "POST" },
    );
  }

  private async authorized<T>(path: string, init: RequestInit = {}, retried = false): Promise<T> {
    if (!this.session) throw new ApiError(401, "NO_SESSION", fallbackMessages[401]!);
    const headers = new Headers(init.headers);
    headers.set("Authorization", `Bearer ${this.session.accessToken}`);
    const response = await this.fetcher(this.url(path), {
      ...init,
      headers,
      credentials: "include",
    });
    if (response.status === 401 && !retried) {
      try {
        await this.refresh();
      } catch {
        this.session = null;
        throw new ApiError(401, "SESSION_EXPIRED", fallbackMessages[401]!);
      }
      return this.authorized<T>(path, init, true);
    }
    if (!response.ok) throw await this.errorFor(response);
    return (await response.json()) as T;
  }

  private refresh(): Promise<Session> {
    if (this.refreshInFlight) return this.refreshInFlight;
    this.refreshInFlight = this.fetcher(this.url("/api/v1/management/auth/refresh"), {
      method: "POST",
      credentials: "include",
    })
      .then(async (response) => {
        if (!response.ok) throw await this.errorFor(response);
        this.session = (await response.json()) as Session;
        return this.session;
      })
      .catch((error: unknown) => {
        this.session = null;
        throw error;
      })
      .finally(() => {
        this.refreshInFlight = null;
      });
    return this.refreshInFlight;
  }

  private async errorFor(response: Response): Promise<ApiError> {
    const problem = (await response.json().catch(() => ({}))) as ProblemDetails;
    const retryHeader = response.headers.get("Retry-After");
    const retryAfterSeconds = retryHeader ? Number.parseInt(retryHeader, 10) : undefined;
    return new ApiError(
      response.status,
      problem.code ?? problem.title ?? "REQUEST_FAILED",
      problem.detail ?? fallbackMessages[response.status] ?? "İstek tamamlanamadı.",
      Number.isFinite(retryAfterSeconds) ? retryAfterSeconds : undefined,
    );
  }

  private url(path: string) {
    return `${this.baseUrl.replace(/\/+$/, "")}${path}`;
  }
}

export const api = new ManagementApi(import.meta.env.VITE_MANAGEMENT_API_BASE_URL ?? "");
