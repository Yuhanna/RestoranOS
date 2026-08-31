import { afterEach, describe, expect, it, vi } from "vitest";
import { DEMO_QR_TOKEN } from "./mockCustomerGateway";
import { resolveCustomerApiBaseUrl } from "./resolveCustomerApiBaseUrl";

describe("resolveCustomerApiBaseUrl", () => {
  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
  });

  it("always keeps the demo QR on mock data even when env points to the API", () => {
    vi.stubEnv("VITE_CUSTOMER_API_BASE_URL", "http://10.0.20.68:5183");
    vi.stubEnv("DEV", "true");
    vi.stubEnv("MODE", "development");

    expect(resolveCustomerApiBaseUrl(DEMO_QR_TOKEN)).toBeUndefined();
  });

  it("uses same-origin API for real QR tokens during development", () => {
    vi.stubEnv("VITE_CUSTOMER_API_BASE_URL", "http://10.0.20.68:5183");
    vi.stubEnv("DEV", "true");
    vi.stubEnv("MODE", "development");

    expect(resolveCustomerApiBaseUrl("real-table-token-from-management")).toBe("");
  });

  it("uses the configured API host outside development builds", () => {
    vi.stubEnv("VITE_CUSTOMER_API_BASE_URL", "http://10.0.20.68:5183");
    vi.stubEnv("DEV", "false");
    vi.stubEnv("MODE", "production");

    expect(resolveCustomerApiBaseUrl("real-table-token-from-management")).toBe(
      "http://10.0.20.68:5183",
    );
  });

  it("uses the LAN API port when no env is configured in production", () => {
    vi.stubEnv("DEV", "false");
    vi.stubEnv("MODE", "production");
    vi.stubGlobal("window", {
      location: {
        search: "",
        hash: "",
        protocol: "http:",
        hostname: "10.0.20.68",
      },
    });

    expect(resolveCustomerApiBaseUrl("real-table-token-from-management")).toBe(
      "http://10.0.20.68:5183",
    );
  });

  it("keeps mock mode when no QR is present", () => {
    expect(resolveCustomerApiBaseUrl(null)).toBeUndefined();
  });
});
