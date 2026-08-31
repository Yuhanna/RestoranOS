import { DEMO_QR_TOKEN } from "./mockCustomerGateway";

function normalizeQrToken(qrToken: string) {
  try {
    return decodeURIComponent(qrToken.trim());
  } catch {
    return qrToken.trim();
  }
}

export function readQrFromLocation(): string | null {
  if (typeof window === "undefined") {
    return null;
  }

  const fromSearch = new URLSearchParams(window.location.search).get("qr");
  if (fromSearch?.trim()) {
    return normalizeQrToken(fromSearch);
  }

  const hash = window.location.hash.replace(/^#/, "");
  if (!hash) {
    return null;
  }

  const fromHash = new URLSearchParams(hash.startsWith("?") ? hash.slice(1) : hash).get("qr");
  return fromHash?.trim() ? normalizeQrToken(fromHash) : null;
}

function resolveQrToken(qrToken?: string | null): string | null {
  if (qrToken === undefined) {
    return readQrFromLocation();
  }

  if (qrToken === null || !qrToken.trim()) {
    return null;
  }

  return normalizeQrToken(qrToken);
}

/** Demo QR always uses mock data; real table QR uses the backend. */
export function resolveCustomerApiBaseUrl(qrToken?: string | null): string | undefined {
  const normalized = resolveQrToken(qrToken);
  if (!normalized) {
    return undefined;
  }

  if (normalized === DEMO_QR_TOKEN) {
    return undefined;
  }

  if (import.meta.env.MODE === "development") {
    // Same-origin via Vite proxy — works from phones on LAN without opening port 5183.
    return "";
  }

  const fromEnv = import.meta.env.VITE_CUSTOMER_API_BASE_URL?.trim();
  if (fromEnv) {
    return fromEnv;
  }

  if (typeof window === "undefined") {
    return undefined;
  }

  return `${window.location.protocol}//${window.location.hostname}:5183`;
}
