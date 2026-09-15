import { ApiError, ManagementApi } from "./api";
import type { Order, Session } from "./domain";

const session = (token: string): Session => ({
  accessToken: token,
  expiresAtUtc: "2026-08-25T13:00:00Z",
  userId: "user",
  tenantId: "tenant",
  branchId: "branch",
});

const order: Order = {
  id: "order-1",
  displayNumber: "#101",
  status: "submitted",
  createdAtUtc: "2026-08-25T12:00:00Z",
  statusChangedAtUtc: "2026-08-25T12:00:00Z",
  estimatedReadyAtUtc: "2026-08-25T12:20:00Z",
  amountMinor: 1000,
  currency: "TRY",
  tableId: "table-1",
  tableLabel: "Masa 1",
};

const response = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });

describe("ManagementApi", () => {
  beforeEach(() => localStorage.clear());

  it("giriş yapar, access tokenı bellekte tutar ve cookie credentials kullanır", async () => {
    const fetcher = vi.fn(async () => response(session("access-1")));
    const api = new ManagementApi("", fetcher as typeof fetch);

    await api.login({ email: "a@b.test", password: "secret", tenantId: "t", branchId: "b" });

    expect(api.getSession()?.accessToken).toBe("access-1");
    expect(localStorage.length).toBe(0);
    expect(fetcher).toHaveBeenCalledWith(
      "/api/v1/management/auth/login",
      expect.objectContaining({ credentials: "include" }),
    );
  });

  it("401 sonrası tek refresh yapar ve isteği yalnızca bir kez tekrarlar", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(response(session("old")))
      .mockResolvedValueOnce(response({}, 401))
      .mockResolvedValueOnce(response(session("new")))
      .mockResolvedValueOnce(response([order]));
    const api = new ManagementApi("", fetcher as typeof fetch);
    await api.login({ email: "a", password: "p", tenantId: "t", branchId: "b" });

    await expect(api.getActiveOrders()).resolves.toEqual([order]);

    expect(fetcher).toHaveBeenCalledTimes(4);
    expect(fetcher.mock.calls[2]?.[0]).toBe("/api/v1/management/auth/refresh");
    const retryHeaders = new Headers(fetcher.mock.calls[3]?.[1]?.headers);
    expect(retryHeaders.get("Authorization")).toBe("Bearer new");
  });

  it("eşzamanlı 401 yanıtları için tek refresh uçuşunu paylaşır", async () => {
    let activeCalls = 0;
    let refreshCalls = 0;
    const fetcher = vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.endsWith("/login")) return response(session("old"));
      if (url.endsWith("/refresh")) {
        refreshCalls += 1;
        return response(session("new"));
      }
      activeCalls += 1;
      return activeCalls <= 2 ? response({}, 401) : response([order]);
    });
    const api = new ManagementApi("", fetcher as typeof fetch);
    await api.login({ email: "a", password: "p", tenantId: "t", branchId: "b" });

    await Promise.all([api.getActiveOrders(), api.getActiveOrders()]);

    expect(refreshCalls).toBe(1);
    expect(activeCalls).toBe(4);
  });

  it.each([
    [403, "FORBIDDEN"],
    [409, "ORDER_CONCURRENCY_CONFLICT"],
    [429, "RATE_LIMITED"],
  ])("%i ProblemDetails hatasını korur", async (status, code) => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(response(session("access")))
      .mockResolvedValueOnce(response({ code, detail: "Açıklama" }, status));
    const api = new ManagementApi("", fetcher as typeof fetch);
    await api.login({ email: "a", password: "p", tenantId: "t", branchId: "b" });

    const error = await api.changeStatus(order, "accepted").catch((reason: unknown) => reason);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status, code, message: "Açıklama" });
    const body = JSON.parse(String(fetcher.mock.calls[1]?.[1]?.body)) as Record<string, string>;
    expect(body.expectedStatusChangedAtUtc).toBe(order.statusChangedAtUtc);
  });

  it("masa QR üretimini yetkili POST olarak gönderir", async () => {
    const generated = {
      id: "qr-1",
      tableId: "table-1",
      tableLabel: "Masa 1",
      status: "active",
      token: "opaque-token-value-not-for-storage",
      entryUrl: "http://localhost:5173/?qr=opaque",
      svgMarkup: "<svg></svg>",
      createdAtUtc: "2026-08-25T12:00:00Z",
      revokedAtUtc: null,
    };
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(response(session("access")))
      .mockResolvedValueOnce(response(generated, 201));
    const api = new ManagementApi("", fetcher as typeof fetch);
    await api.login({ email: "a", password: "p", tenantId: "t", branchId: "b" });

    await expect(api.generateQr("table-1")).resolves.toEqual(generated);
    expect(fetcher.mock.calls[1]?.[0]).toBe("/api/v1/management/tables/table-1/qr-codes");
    expect(fetcher.mock.calls[1]?.[1]).toEqual(expect.objectContaining({ method: "POST" }));
  });
});
