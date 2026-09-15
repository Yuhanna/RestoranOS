import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "./App";
import { AdminApi } from "./api";
import type { Catalog, Session, SubscriptionOffer } from "./domain";

const session: Session = {
  accessToken: "access",
  expiresAtUtc: "2026-09-15T13:00:00Z",
  userId: "user",
  tenantId: "00000000-0000-0000-0000-000000000000",
  branchId: "00000000-0000-0000-0000-000000000000",
  realm: "platform",
  roleCode: "Owner",
  email: "platform@local.test",
};

const catalog: Catalog = {
  note: "Yayınlanan fiyat yeni yükseltmelerde geçerlidir.",
  products: [
    {
      productCode: "Pro",
      productKind: "plan",
      displayName: "Pro",
      maxBranches: 2,
      maxTablesPerBranch: null,
      maxActiveUsers: 25,
      canUseLiveOrderPanel: true,
      canUseMultiBranch: true,
      hasPrioritySupport: false,
      prices: [
        {
          id: "price-1",
          productCode: "Pro",
          productKind: "plan",
          displayName: "Pro",
          interval: "month",
          currency: "TRY",
          amountMinor: 249900,
          taxInclusive: true,
          status: "published",
          createdAtUtc: "2026-09-15T00:00:00Z",
          publishedAtUtc: "2026-09-15T00:00:00Z",
          archivedAtUtc: null,
        },
      ],
    },
  ],
};

const offers: SubscriptionOffer[] = [];

const ok = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });

function createFetcher() {
  return vi.fn(async (input: RequestInfo) => {
    const url = typeof input === "string" ? input : input.url;
    if (url.includes("/auth/refresh")) return ok(session);
    if (url.includes("/auth/login")) return ok(session);
    if (url.includes("/catalog")) return ok(catalog);
    if (url.includes("/subscription-offers")) return ok(offers);
    return ok({}, 404);
  });
}

test("login screen is visually a separate admin console", async () => {
  const fetcher = vi.fn(async (input: RequestInfo) => {
    const url = typeof input === "string" ? input : input.url;
    if (url.includes("/auth/refresh")) return new Response("{}", { status: 401 });
    return new Response("{}", { status: 401 });
  });
  render(<App api={new AdminApi("", fetcher)} />);
  expect(await screen.findByRole("heading", { name: "Bu restoran paneli değil." })).toBeInTheDocument();
  expect(screen.getByRole("button", { name: "Admin girişi" })).toBeInTheDocument();
});

test("signed-in chrome shows Pasa Admin and catalog prices", async () => {
  const user = userEvent.setup();
  const fetcher = createFetcher();
  render(<App api={new AdminApi("", fetcher)} />);
  expect(await screen.findByText("İç yönetim · admin.pasa.app")).toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Planlar ve fiyatlar" }));
  expect(await screen.findByText("2.499,00 TRY")).toBeInTheDocument();
  expect(screen.getByText("Rol: Owner · platform")).toBeInTheDocument();
});

test("restaurant account is refused with a distinct admin message", async () => {
  const user = userEvent.setup();
  const fetcher = vi.fn(async (input: RequestInfo) => {
    const url = typeof input === "string" ? input : input.url;
    if (url.includes("/auth/refresh")) return new Response("{}", { status: 401 });
    if (url.includes("/auth/login")) {
      return new Response(JSON.stringify({ code: "PLATFORM_ACCESS_DENIED", detail: "denied" }), {
        status: 403,
        headers: { "Content-Type": "application/json" },
      });
    }
    return new Response("{}", { status: 401 });
  });
  render(<App api={new AdminApi("", fetcher)} />);
  await screen.findByRole("heading", { name: "Bu restoran paneli değil." });
  await user.type(screen.getByLabelText("E-posta"), "owner@local.test");
  await user.type(screen.getByLabelText("Parola"), "password-12345");
  await user.click(screen.getByRole("button", { name: "Admin girişi" }));
  expect(
    await screen.findByText("Bu hesap restoran paneli içindir. Pasa Admin için platform operatörü girişi kullanın."),
  ).toBeInTheDocument();
});

test("billing role can draft but does not see publish", async () => {
  const user = userEvent.setup();
  const billingSession: Session = { ...session, roleCode: "Billing" };
  const draftCatalog: Catalog = {
    ...catalog,
    products: [
      {
        ...catalog.products[0]!,
        prices: [{ ...catalog.products[0]!.prices[0]!, status: "draft", id: "draft-1" }],
      },
    ],
  };
  const fetcher = vi.fn(async (input: RequestInfo) => {
    const url = typeof input === "string" ? input : input.url;
    if (url.includes("/auth/refresh")) {
      return new Response(JSON.stringify(billingSession), {
        status: 200,
        headers: { "Content-Type": "application/json" },
      });
    }
    if (url.includes("/catalog")) return ok(draftCatalog);
    if (url.includes("/subscription-offers")) return ok(offers);
    return ok({}, 404);
  });
  render(<App api={new AdminApi("", fetcher)} />);
  expect(await screen.findByText("Rol: Billing · platform")).toBeInTheDocument();
  await user.click(screen.getByRole("button", { name: "Planlar ve fiyatlar" }));
  expect(await screen.findByRole("button", { name: "Taslak oluştur" })).toBeInTheDocument();
  expect(screen.queryByRole("button", { name: "Yayınla" })).not.toBeInTheDocument();
});
