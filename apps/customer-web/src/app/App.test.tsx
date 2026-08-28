import { cleanup, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it } from "vitest";
import { DEMO_QR_TOKEN, mockCustomerGateway } from "../data/mockCustomerGateway";
import { App } from "./App";

afterEach(() => {
  cleanup();
  sessionStorage.clear();
  localStorage.clear();
});

describe("customer experience", () => {
  it("requires a QR context before exposing the menu", () => {
    render(<App gateway={mockCustomerGateway} qrToken={null} />);

    expect(screen.getByRole("heading", { name: "Menüye masanızdan ulaşın" })).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Özenle hazırlandı." })).not.toBeInTheDocument();
  });

  it("rejects an invalid QR token", async () => {
    render(<App gateway={mockCustomerGateway} qrToken="invalid-token" />);

    expect(
      await screen.findByRole("heading", { name: "Bu QR kodu kullanılamıyor" }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "Özenle hazırlandı." })).not.toBeInTheDocument();
  });

  it("resolves a valid QR and filters the menu", async () => {
    const user = userEvent.setup();
    render(<App gateway={mockCustomerGateway} qrToken={DEMO_QR_TOKEN} />);

    expect(await screen.findByText(/Masa 7/)).toBeInTheDocument();
    await user.type(screen.getByRole("searchbox", { name: "Menüde ara" }), "risotto");

    expect(screen.getByRole("button", { name: "Trüflü Risotto" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Izgara Levrek" })).not.toBeInTheDocument();
  });

  it("adds a configured product and submits an order", async () => {
    const user = userEvent.setup();
    render(<App gateway={mockCustomerGateway} qrToken={DEMO_QR_TOKEN} />);

    await user.click(await screen.findByRole("button", { name: "Izgara Levrek" }));
    const productDialog = screen.getByRole("dialog", { name: "Izgara Levrek" });
    await user.click(within(productDialog).getByRole("button", { name: /Siparişe ekle/ }));
    await user.click(screen.getByRole("button", { name: /Siparişimi gör/ }));

    const cartDialog = screen.getByRole("dialog", { name: "Siparişiniz" });
    expect(within(cartDialog).getByText("Izgara Levrek")).toBeInTheDocument();
    await user.click(within(cartDialog).getByRole("button", { name: "Siparişi mutfağa gönder" }));

    expect(await screen.findByRole("heading", { name: "Siparişiniz alındı" })).toBeInTheDocument();
    expect(screen.getByText("#1042")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Menüye dön" }));
    expect(screen.getByRole("heading", { name: "Özenle hazırlandı." })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Siparişi takip et: #1042" }));
    expect(await screen.findByRole("heading", { name: "Siparişiniz alındı" })).toBeInTheDocument();
  });

  it("keeps the track-order button after remount (refresh / re-scan)", async () => {
    const user = userEvent.setup();
    const { unmount } = render(<App gateway={mockCustomerGateway} qrToken={DEMO_QR_TOKEN} />);

    await user.click(await screen.findByRole("button", { name: "Izgara Levrek" }));
    const productDialog = screen.getByRole("dialog", { name: "Izgara Levrek" });
    await user.click(within(productDialog).getByRole("button", { name: /Siparişe ekle/ }));
    await user.click(screen.getByRole("button", { name: /Siparişimi gör/ }));
    await user.click(
      within(screen.getByRole("dialog", { name: "Siparişiniz" })).getByRole("button", {
        name: "Siparişi mutfağa gönder",
      }),
    );
    expect(await screen.findByRole("heading", { name: "Siparişiniz alındı" })).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Menüye dön" }));
    expect(screen.getByRole("button", { name: "Siparişi takip et: #1042" })).toBeInTheDocument();

    unmount();
    render(<App gateway={mockCustomerGateway} qrToken={DEMO_QR_TOKEN} />);

    expect(await screen.findByRole("button", { name: "Siparişi takip et: #1042" })).toBeInTheDocument();
  });
});
