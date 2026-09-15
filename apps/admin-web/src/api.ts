import type { Catalog, PlanPrice, Session, SubscriptionOffer } from "./domain";

const fallbackMessages: Record<number, string> = {
  401: "Oturumunuz sona erdi. Lütfen yeniden giriş yapın.",
  403: "Bu işlem için yetkiniz bulunmuyor.",
  429: "Çok fazla istek gönderildi. Kısa süre sonra tekrar deneyin.",
};

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
    message: string,
  ) {
    super(message);
  }
}

type FetchLike = typeof fetch;

export class AdminApi {
  private session: Session | null = null;
  private refreshInFlight: Promise<Session> | null = null;

  constructor(
    private readonly baseUrl: string,
    private readonly fetcher: FetchLike = fetch.bind(globalThis),
  ) {}

  getSession = () => this.session;

  async login(email: string, password: string): Promise<Session> {
    const response = await this.fetcher(this.url("/api/v1/platform/auth/login"), {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
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
      await this.fetcher(this.url("/api/v1/platform/auth/logout"), {
        method: "POST",
        credentials: "include",
      });
    } finally {
      this.session = null;
    }
  }

  getCatalog() {
    return this.authorized<Catalog>("/api/v1/platform/catalog");
  }

  createPrice(input: { productCode: string; interval: string; amountMinor: number }) {
    return this.authorized<PlanPrice>("/api/v1/platform/catalog/prices", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ ...input, currency: "TRY", taxInclusive: true }),
    });
  }

  publishPrice(priceId: string) {
    return this.authorized(`/api/v1/platform/catalog/prices/${priceId}/publish`, { method: "POST" });
  }

  archivePrice(priceId: string) {
    return this.authorized(`/api/v1/platform/catalog/prices/${priceId}/archive`, { method: "POST" });
  }

  listOffers() {
    return this.authorized<SubscriptionOffer[]>("/api/v1/platform/subscription-offers");
  }

  createOffer(input: {
    audience: string;
    targetPlanCode: string;
    discountPercent: number;
    durationMonths: number;
    title: string;
    body: string;
  }) {
    return this.authorized<SubscriptionOffer>("/api/v1/platform/subscription-offers", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        ...input,
        startsAtUtc: new Date().toISOString(),
        isActive: true,
      }),
    });
  }

  toggleOffer(id: string, isActive: boolean) {
    return this.authorized<SubscriptionOffer>(`/api/v1/platform/subscription-offers/${id}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ isActive: !isActive }),
    });
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
    if (response.status === 204) return undefined as T;
    return (await response.json()) as T;
  }

  private refresh(): Promise<Session> {
    if (this.refreshInFlight) return this.refreshInFlight;
    this.refreshInFlight = this.fetcher(this.url("/api/v1/platform/auth/refresh"), {
      method: "POST",
      credentials: "include",
    })
      .then(async (response) => {
        if (!response.ok) throw await this.errorFor(response);
        this.session = (await response.json()) as Session;
        return this.session;
      })
      .finally(() => {
        this.refreshInFlight = null;
      });
    return this.refreshInFlight;
  }

  private url(path: string) {
    return `${this.baseUrl}${path}`;
  }

  private async errorFor(response: Response) {
    try {
      const body = (await response.json()) as { title?: string; detail?: string; code?: string };
      return new ApiError(
        response.status,
        body.code ?? "HTTP_ERROR",
        body.detail ?? body.title ?? fallbackMessages[response.status] ?? "İstek tamamlanamadı.",
      );
    } catch {
      return new ApiError(
        response.status,
        "HTTP_ERROR",
        fallbackMessages[response.status] ?? "İstek tamamlanamadı.",
      );
    }
  }
}

export const api = new AdminApi(import.meta.env.VITE_PLATFORM_API_BASE_URL ?? "");
