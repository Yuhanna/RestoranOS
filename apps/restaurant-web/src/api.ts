import type {
  DiningTable,
  GeneratedQr,
  LoginInput,
  MenuDetail,
  MenuSummary,
  Order,
  OrderStatus,
  ProblemDetails,
  QrPrint,
  Session,
  TableQrCode,
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
    private readonly fetcher: FetchLike = fetch,
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

  private async authorized<T>(path: string, init: RequestInit, retried = false): Promise<T> {
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
