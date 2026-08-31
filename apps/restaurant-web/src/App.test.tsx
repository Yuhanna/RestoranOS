import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "./App";
import { ManagementApi } from "./api";
import type { Order, Session } from "./domain";
import type { RealtimeClient } from "./realtime";

const session: Session = {
  accessToken: "access",
  expiresAtUtc: "2026-08-25T13:00:00Z",
  userId: "user",
  tenantId: "tenant",
  branchId: "branch",
};
const order: Order = {
  id: "00000000-0000-0000-0000-000000000101",
  displayNumber: "#101",
  status: "submitted",
  createdAtUtc: new Date().toISOString(),
  statusChangedAtUtc: "2026-08-25T12:00:00Z",
  estimatedReadyAtUtc: "2099-08-25T12:20:00Z",
  amountMinor: 1000,
  currency: "TRY",
  tableId: "00000000-0000-0000-0000-000000000001",
  tableLabel: "Masa 1",
};
const workspace = {
  tenantId: "tenant",
  restaurantId: "restaurant",
  restaurantName: "Test",
  branchId: "branch",
  branchName: "Main",
  entitlements: null,
};
const orderDetail = {
  ...order,
  tableId: "00000000-0000-0000-0000-000000000201",
  tableLabel: "Masa 1",
  subtotalAmountMinor: 1000,
  discountAmountMinor: 0,
  totalAmountMinor: 1000,
  items: [
    {
      id: "00000000-0000-0000-0000-000000000301",
      menuItemId: "00000000-0000-0000-0000-000000000302",
      name: "Burger",
      quantity: 1,
      listUnitPriceAmountMinor: 1000,
      discountUnitAmountMinor: 0,
      unitPriceAmountMinor: 1000,
      currency: "TRY",
      note: "Az pişmiş",
    },
  ],
  statusHistory: [{ status: "submitted", changedAtUtc: order.statusChangedAtUtc, changedByUserId: null }],
};
const ok = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });

function createManagementFetcher(options?: {
  orders?: Order[];
  changed?: Order;
  statusError?: { status: number; code: string };
  tables?: unknown[];
  menus?: unknown[];
  detail?: typeof orderDetail;
}) {
  const orders = options?.orders ?? [order];
  const changed = options?.changed ?? { ...order, status: "accepted" as const, statusChangedAtUtc: "2026-08-25T12:01:00Z" };
  const detail = options?.detail ?? orderDetail;
  return vi.fn(async (input: RequestInfo, init?: RequestInit) => {
    const url = typeof input === "string" ? input : input.url;
    if (url.includes("/auth/refresh")) return ok({}, 401);
    if (url.includes("/auth/login")) return ok(session);
    if (url.includes("/dashboard/today")) {
      return ok({
        todaysOrderCount: 1,
        todaysRevenueMinor: 0,
        openTablesCount: 1,
        pendingOrdersCount: 1,
        completedOrdersTodayCount: 0,
        currency: "TRY",
        dayStartUtc: "2026-08-31T00:00:00Z",
        dayEndUtc: "2026-09-01T00:00:00Z",
      });
    }
    if (url.includes("/orders/active")) return ok(orders);
    if (url.includes("/management/workspace")) return ok(workspace);
    if (url.includes("/orders/") && url.includes("/status") && init?.method === "PUT") {
      if (options?.statusError) {
        return ok({ code: options.statusError.code, detail: "server detail" }, options.statusError.status);
      }
      return ok(changed);
    }
    if (url.includes("/orders/") && !url.includes("/orders/active")) return ok(detail);
    if (url.includes("/management/tables")) return ok(options?.tables ?? []);
    if (url.includes("/management/menus")) return ok(options?.menus ?? []);
    return ok({});
  });
}
const realtime: RealtimeClient = {
  async start(callbacks) {
    callbacks.onState("connected");
    return async () => undefined;
  },
};

async function login(fetcher: ReturnType<typeof vi.fn>) {
  const user = userEvent.setup();
  render(
    <App
      managementApi={new ManagementApi("", fetcher as typeof fetch)}
      realtimeClient={realtime}
    />,
  );
  await screen.findByRole("heading", { name: "Vardiyaya bağlan" });
  await user.type(screen.getByLabelText("E-posta"), "owner@example.test");
  await user.type(screen.getByLabelText("Parola"), "strong-password");
  await user.type(screen.getByLabelText("Tenant kimliği"), "tenant");
  await user.type(screen.getByLabelText("Şube kimliği"), "branch");
  await user.click(screen.getByRole("button", { name: "Operasyon ekranını aç" }));
  await screen.findByRole("heading", { name: "Operasyon Merkezi" });
  await user.click(screen.getByRole("button", { name: "Siparişler" }));
  await screen.findByText("#101");
  return user;
}

describe("Restaurant operasyon ekranı", () => {
  it("refresh oturumu yoksa login gösterir ve gerçek giriş akışını açar", async () => {
    await login(createManagementFetcher());

    expect(screen.getByRole("heading", { name: "Operasyon Merkezi" })).toBeInTheDocument();
    expect(screen.getByText("connected")).toBeInTheDocument();
  });

  it("izin verilen durum geçişini uygular", async () => {
    const user = await login(createManagementFetcher());

    await user.click(screen.getByRole("button", { name: "Siparişe al" }));

    await waitFor(() => expect(screen.getAllByText("ONAYLANDI")).toHaveLength(3));
  });

  async function expectStatusError(status: number, code: string, message: string) {
    const fetcher = createManagementFetcher({ statusError: { status, code } });
    const user = await login(fetcher);

    await user.click(screen.getByRole("button", { name: "Siparişe al" }));

    await waitFor(() => expect(screen.getByRole("alert")).toHaveTextContent(message));
  }

  it("403 yetki hatasını operatöre açıklar", async () => {
    await expectStatusError(403, "FORBIDDEN", "Order.Modify yetkiniz bulunmuyor");
  });

  it("409 çakışmasında listeyi yeniler ve operatörü uyarır", async () => {
    await expectStatusError(409, "ORDER_CONCURRENCY_CONFLICT", "başka bir ekranda güncellendi");
  });

  it("masa ve QR yönetim ekranını açar", async () => {
    const user = await login(createManagementFetcher());

    await user.click(screen.getByRole("button", { name: "Masalar" }));

    expect(await screen.findByRole("heading", { name: "Masalar ve QR kodları" })).toBeInTheDocument();
  });

  it("menü yönetim ekranını açar", async () => {
    const user = await login(createManagementFetcher());

    await user.click(screen.getByRole("button", { name: "Menü" }));

    expect(await screen.findByRole("heading", { name: "Menü taslağı ve yayın" })).toBeInTheDocument();
  });

  it("sipariş detay panelini açar", async () => {
    const user = await login(createManagementFetcher());

    await user.click(screen.getByRole("button", { name: "Detay" }));

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(await screen.findByText(/Burger/)).toBeInTheDocument();
    expect(screen.getByText(/Az pişmiş/)).toBeInTheDocument();
  });
});
