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
};
const ok = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });
const realtime: RealtimeClient = {
  async start(_onOrder, _onResync, onState) {
    onState("connected");
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
  await screen.findByText("#101");
  return user;
}

describe("Restaurant operasyon ekranı", () => {
  it("refresh oturumu yoksa login gösterir ve gerçek giriş akışını açar", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(ok({ code: "INVALID_REFRESH" }, 401))
      .mockResolvedValueOnce(ok(session))
      .mockResolvedValueOnce(ok([order]));

    await login(fetcher);

    expect(screen.getByRole("heading", { name: "Operasyon Merkezi" })).toBeInTheDocument();
    expect(screen.getByText("connected")).toBeInTheDocument();
  });

  it("izin verilen durum geçişini uygular", async () => {
    const changed = { ...order, status: "accepted", statusChangedAtUtc: "2026-08-25T12:01:00Z" };
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(ok({}, 401))
      .mockResolvedValueOnce(ok(session))
      .mockResolvedValueOnce(ok([order]))
      .mockResolvedValueOnce(ok(changed));
    const user = await login(fetcher);

    await user.click(screen.getByRole("button", { name: "Siparişe al" }));

    await waitFor(() => expect(screen.getAllByText("ONAYLANDI")).toHaveLength(2));
  });

  async function expectStatusError(status: number, code: string, message: string) {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(ok({}, 401))
      .mockResolvedValueOnce(ok(session))
      .mockResolvedValueOnce(ok([order]))
      .mockResolvedValueOnce(ok({ code, detail: "server detail" }, status));
    if (status === 409) fetcher.mockResolvedValueOnce(ok([order]));
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
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(ok({}, 401))
      .mockResolvedValueOnce(ok(session))
      .mockResolvedValueOnce(ok([order]))
      .mockResolvedValueOnce(ok([]));
    const user = await login(fetcher);

    await user.click(screen.getByRole("button", { name: "Masalar" }));

    expect(await screen.findByRole("heading", { name: "Masalar ve QR kodları" })).toBeInTheDocument();
    expect(fetcher.mock.calls.some((call) => String(call[0]).endsWith("/api/v1/management/tables"))).toBe(
      true,
    );
  });

  it("menü yönetim ekranını açar", async () => {
    const fetcher = vi
      .fn()
      .mockResolvedValueOnce(ok({}, 401))
      .mockResolvedValueOnce(ok(session))
      .mockResolvedValueOnce(ok([order]))
      .mockResolvedValueOnce(ok([]));
    const user = await login(fetcher);

    await user.click(screen.getByRole("button", { name: "Menü" }));

    expect(await screen.findByRole("heading", { name: "Menü taslağı ve yayın" })).toBeInTheDocument();
  });
});
