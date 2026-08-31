import { afterEach, describe, expect, it, vi } from "vitest";
import { createHttpCustomerGateway } from "./httpCustomerGateway";

const signalR = vi.hoisted(() => {
  const connection = {
    state: "Connected",
    on: vi.fn(),
    off: vi.fn(),
    onreconnected: vi.fn(),
    onclose: vi.fn(),
    start: vi.fn().mockResolvedValue(undefined),
    stop: vi.fn().mockResolvedValue(undefined),
    invoke: vi.fn().mockResolvedValue(undefined),
  };
  return { connection };
});

vi.mock("@microsoft/signalr", () => ({
  HubConnectionState: { Disconnected: "Disconnected" },
  HubConnectionBuilder: class {
    withUrl() {
      return this;
    }
    withAutomaticReconnect() {
      return this;
    }
    build() {
      return signalR.connection;
    }
  },
}));

afterEach(() => {
  vi.unstubAllGlobals();
  vi.clearAllMocks();
});

describe("http customer gateway", () => {
  it("maps the session contract and adds the local all category", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          sessionToken: "opaque-session-token",
          restaurantName: "Marea",
          branchName: "Nişantaşı",
          tableLabel: "Masa 7",
          locale: "tr",
          categories: [{ id: "cat-1", name: "Ana yemekler" }],
          products: [],
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    const session =
      await createHttpCustomerGateway("http://localhost:5183/").resolveQr("opaque-qr-token");

    expect(session.categories[0]).toEqual({ id: "all", name: "Tümü" });
    expect(JSON.parse(String(fetchMock.mock.calls[0]?.[1]?.body))).toMatchObject({
      qrToken: "opaque-qr-token",
      locale: "tr",
    });
  });

  it("normalizes product payloads from the API contract", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          sessionToken: "opaque-session-token",
          restaurantName: "Marea",
          branchName: "Nişantaşı",
          tableLabel: "Masa 7",
          locale: "tr",
          categories: [{ id: "cat-1", name: "Ana yemekler" }],
          products: [
            {
              id: "product-1",
              categoryId: "cat-1",
              name: "Levrek",
              description: "Izgara",
              price: { amountMinor: 42000, currency: "TRY" },
              imageUrl: "/media/levrek.jpg",
              imageAlt: "Levrek",
              available: true,
            },
          ],
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    const session =
      await createHttpCustomerGateway("http://localhost:5183/").resolveQr("opaque-qr-token");

    expect(session.products).toHaveLength(1);
    expect(session.products[0]).toMatchObject({
      id: "product-1",
      categoryId: "cat-1",
      price: { amountMinor: 42000, currency: "TRY" },
      dietaryTags: [],
      allergens: [],
      modifierGroups: [],
    });
  });

  it("sends idempotency as an HTTP header", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          id: "order-1",
          displayNumber: "#1001",
          status: "submitted",
          statusChangedAt: "2026-08-25T11:59:00Z",
          estimatedReadyAt: "2026-08-25T12:00:00Z",
          total: { amountMinor: 42000, currency: "TRY" },
        }),
        { status: 201, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await createHttpCustomerGateway("http://localhost:5183").submitOrder({
      sessionToken: "opaque-session-token",
      idempotencyKey: "order-attempt-1",
      lines: [{ productId: "product-1", quantity: 1, modifierOptionIds: [] }],
    });

    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5183/api/v1/customer/orders",
      expect.objectContaining({
        headers: expect.objectContaining({ "Idempotency-Key": "order-attempt-1" }),
      }),
    );
  });

  it("reads an order with the customer session header", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      new Response(
        JSON.stringify({
          id: "order-1",
          displayNumber: "#1001",
          status: "accepted",
          statusChangedAt: "2026-08-25T12:01:00Z",
          estimatedReadyAt: "2026-08-25T12:18:00Z",
          total: { amountMinor: 42000, currency: "TRY" },
        }),
        { status: 200, headers: { "Content-Type": "application/json" } },
      ),
    );
    vi.stubGlobal("fetch", fetchMock);

    await createHttpCustomerGateway("http://localhost:5183").getOrder(
      "order-1",
      "opaque-session-token",
    );

    expect(fetchMock).toHaveBeenCalledWith(
      "http://localhost:5183/api/v1/customer/orders/order-1",
      expect.objectContaining({
        headers: { "X-Customer-Session": "opaque-session-token" },
      }),
    );
  });

  it("resubscribes and REST resyncs after reconnect", async () => {
    const latest = {
      id: "order-1",
      displayNumber: "#1001",
      status: "preparing",
      statusChangedAt: "2026-08-25T12:02:00Z",
      estimatedReadyAt: "2026-08-25T12:18:00Z",
      total: { amountMinor: 42000, currency: "TRY" },
    };
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify(latest), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );
    const onOrder = vi.fn();
    const gateway = createHttpCustomerGateway("http://localhost:5183");

    await gateway.watchOrder("order-1", "opaque-session-token", onOrder, vi.fn());
    const reconnect = signalR.connection.onreconnected.mock.calls[0]?.[0] as () => Promise<void>;
    await reconnect();

    expect(signalR.connection.invoke).toHaveBeenLastCalledWith(
      "SubscribeToOrder",
      "order-1",
      "opaque-session-token",
    );
    expect(onOrder).toHaveBeenCalledWith(latest);
  });
});
