import { HubConnectionState, type HubConnection } from "@microsoft/signalr";
import { ManagementApi } from "./api";
import type { Order, Session } from "./domain";
import { SignalRRealtimeClient } from "./realtime";

it("yeniden bağlantıdan sonra REST ile tam eşitleme yapar", async () => {
  const session: Session = {
    accessToken: "access",
    expiresAtUtc: "2026-08-25T13:00:00Z",
    userId: "u",
    tenantId: "t",
    branchId: "b",
  };
  const orders: Order[] = [
    {
      id: "order",
      displayNumber: "#1",
      status: "preparing",
      createdAtUtc: "2026-08-25T12:00:00Z",
      statusChangedAtUtc: "2026-08-25T12:05:00Z",
      estimatedReadyAtUtc: "2026-08-25T12:20:00Z",
      amountMinor: 1,
      currency: "TRY",
    },
  ];
  const fetcher = vi
    .fn()
    .mockResolvedValueOnce(new Response(JSON.stringify(session)))
    .mockResolvedValueOnce(new Response(JSON.stringify(orders)));
  const api = new ManagementApi("", fetcher as typeof fetch);
  await api.login({ email: "a", password: "p", tenantId: "t", branchId: "b" });

  let reconnected: (() => void | Promise<void>) | undefined;
  const fakeConnection = {
    state: HubConnectionState.Connected,
    on: vi.fn(),
    off: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn((callback: () => void | Promise<void>) => {
      reconnected = callback;
    }),
    onclose: vi.fn(),
    start: vi.fn(async () => undefined),
    stop: vi.fn(async () => undefined),
  } as unknown as HubConnection;
  const resync = vi.fn();
  const client = new SignalRRealtimeClient("", api, () => fakeConnection);

  await client.start(vi.fn(), resync, vi.fn());
  await reconnected?.();

  expect(api.getSession()?.accessToken).toBe("access");
  expect(resync).toHaveBeenCalledWith(orders);
  expect(fetcher).toHaveBeenCalledWith(
    "/api/v1/management/orders/active",
    expect.objectContaining({ credentials: "include" }),
  );
});
