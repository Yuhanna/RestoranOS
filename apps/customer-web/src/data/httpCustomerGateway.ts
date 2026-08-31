import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import {
  CustomerGatewayError,
  type CustomerGateway,
  type CustomerSession,
  type Order,
  type Product,
  type ServiceRequest,
  type SubmitOrderRequest,
} from "../domain/customer";

const REQUEST_TIMEOUT_MS = 12_000;

const fetchWithTimeout = async (
  input: RequestInfo | URL,
  init: RequestInit | undefined,
  timeoutMs = REQUEST_TIMEOUT_MS,
): Promise<Response> => {
  const timeoutController = new AbortController();
  const timeoutId = window.setTimeout(() => timeoutController.abort(), timeoutMs);
  const upstream = init?.signal;

  if (upstream) {
    if (upstream.aborted) {
      window.clearTimeout(timeoutId);
      throw new DOMException("Aborted", "AbortError");
    }
    upstream.addEventListener("abort", () => timeoutController.abort(), { once: true });
  }

  try {
    return await fetch(input, { ...init, signal: timeoutController.signal });
  } catch (error) {
    if (timeoutController.signal.aborted && !(upstream?.aborted)) {
      throw new CustomerGatewayError(
        "NETWORK",
        "Sunucu yanıt vermedi. API çalışıyor mu ve customer-web yeniden başlatıldı mı?",
      );
    }
    throw error;
  } finally {
    window.clearTimeout(timeoutId);
  }
};

type ProblemDetails = {
  code?: string;
  title?: string;
  detail?: string;
};

const problemCode = (problem: ProblemDetails) =>
  (problem.code ?? problem.title ?? "").trim().toUpperCase();

const problemFor = async (response: Response): Promise<CustomerGatewayError> => {
  const problem = (await response.json().catch(() => ({}))) as ProblemDetails;
  const code = problemCode(problem);

  if (code === "INVALID_QR") {
    return new CustomerGatewayError(
      "INVALID_QR",
      problem.detail ?? "QR token is invalid.",
    );
  }

  if (code === "MENU_UNAVAILABLE") {
    return new CustomerGatewayError(
      "MENU_UNAVAILABLE",
      problem.detail ?? "A published menu is not available.",
    );
  }

  if (code === "SERVICE_REQUEST_OPEN" || code === "SERVICE_REQUEST_COOLDOWN") {
    return new CustomerGatewayError(
      "SERVICE_REQUEST_OPEN",
      problem.detail ?? "This request is already open.",
    );
  }

  if (response.status >= 500 || response.status === 0) {
    return new CustomerGatewayError("NETWORK", problem.detail ?? "Backend is unavailable");
  }

  return new CustomerGatewayError(
    "ORDER_REJECTED",
    problem.detail ?? problem.title ?? `Request failed (${response.status})`,
  );
};

const mapProduct = (raw: Record<string, unknown>): Product => {
  const price = raw.price as { amountMinor?: number; currency?: string } | undefined;
  const pricing = raw.pricing as
    | { list?: { amountMinor?: number }; discount?: { amountMinor?: number }; final?: { amountMinor?: number } }
    | undefined;
  const currency = (price?.currency as "TRY") ?? "TRY";
  const finalMinor = Number(price?.amountMinor ?? pricing?.final?.amountMinor ?? 0);
  const listMinor = Number(pricing?.list?.amountMinor ?? finalMinor);
  const discountMinor = Number(pricing?.discount?.amountMinor ?? 0);
  return {
    id: String(raw.id ?? ""),
    categoryId: String(raw.categoryId ?? ""),
    name: String(raw.name ?? ""),
    description: String(raw.description ?? ""),
    price: { amountMinor: finalMinor, currency },
    listPrice: discountMinor > 0 ? { amountMinor: listMinor, currency } : undefined,
    discount: discountMinor > 0 ? { amountMinor: discountMinor, currency } : undefined,
    promotionLabel: typeof raw.promotionLabel === "string" ? raw.promotionLabel : undefined,
    imageUrl: String(raw.imageUrl ?? ""),
    imageAlt: String(raw.imageAlt ?? raw.name ?? ""),
    available: raw.available !== false,
    badge:
      typeof raw.badge === "string"
        ? raw.badge
        : typeof raw.promotionLabel === "string"
          ? raw.promotionLabel
          : undefined,
    dietaryTags: Array.isArray(raw.dietaryTags) ? (raw.dietaryTags as Product["dietaryTags"]) : [],
    allergens: Array.isArray(raw.allergens) ? (raw.allergens as string[]) : [],
    modifierGroups: Array.isArray(raw.modifierGroups)
      ? (raw.modifierGroups as Product["modifierGroups"])
      : [],
  };
};

const mapOrder = (raw: Record<string, unknown>): Order => {
  const total = raw.total as { amountMinor?: number; currency?: string } | undefined;
  const subtotal = raw.subtotal as { amountMinor?: number; currency?: string } | undefined;
  const discount = raw.discount as { amountMinor?: number; currency?: string } | undefined;
  const currency = (total?.currency as "TRY") ?? "TRY";
  return {
    id: String(raw.id ?? ""),
    displayNumber: String(raw.displayNumber ?? ""),
    status: raw.status as Order["status"],
    statusChangedAt: String(raw.statusChangedAt ?? raw.statusChangedAtUtc ?? ""),
    estimatedReadyAt: String(raw.estimatedReadyAt ?? raw.estimatedReadyAtUtc ?? ""),
    total: {
      amountMinor: Number(total?.amountMinor ?? raw.amountMinor ?? 0),
      currency,
    },
    subtotal: subtotal
      ? { amountMinor: Number(subtotal.amountMinor ?? 0), currency: (subtotal.currency as "TRY") ?? currency }
      : undefined,
    discount: discount?.amountMinor
      ? { amountMinor: Number(discount.amountMinor), currency: (discount.currency as "TRY") ?? currency }
      : undefined,
  };
};

export const createHttpCustomerGateway = (baseUrl: string): CustomerGateway => {
  const apiBase = baseUrl.replace(/\/+$/, "");

  return {
    async resolveQr(qrToken, signal, locale = "tr") {
      let response: Response;
      try {
        response = await fetchWithTimeout(`${apiBase}/api/v1/customer/sessions/resolve`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ qrToken, locale }),
          signal,
        });
      } catch (error) {
        if (error instanceof DOMException && error.name === "AbortError") throw error;
        throw new CustomerGatewayError("NETWORK", "Backend is unavailable");
      }

      if (!response.ok) throw await problemFor(response);

      let payload: unknown;
      try {
        payload = await response.json();
      } catch {
        throw new CustomerGatewayError(
          "NETWORK",
          "Backend returned an invalid response. Is the API running?",
        );
      }

      const session = payload as CustomerSession & {
        activeOrders?: Record<string, unknown>[];
      };
      const products = (session.products ?? []).map((item) =>
        mapProduct(item as Record<string, unknown>),
      );
      return {
        sessionToken: String(session.sessionToken ?? ""),
        restaurantName: String(session.restaurantName ?? "Restoran"),
        branchName: String(session.branchName ?? ""),
        tableLabel: String(session.tableLabel ?? ""),
        locale: session.locale === "en" ? "en" : "tr",
        categories: [
          { id: "all", name: session.locale === "en" ? "All" : "Tümü" },
          ...(session.categories ?? []),
        ],
        products,
        activeOrders: (session.activeOrders ?? []).map((item) =>
          mapOrder(item as Record<string, unknown>),
        ),
        openServiceRequestTypes: session.openServiceRequestTypes ?? [],
      };
    },

    async submitOrder(request: SubmitOrderRequest, signal) {
      let response: Response;
      try {
        response = await fetchWithTimeout(`${apiBase}/api/v1/customer/orders`, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "Idempotency-Key": request.idempotencyKey,
          },
          body: JSON.stringify({
            sessionToken: request.sessionToken,
            lines: request.lines,
          }),
          signal,
        });
      } catch (error) {
        if (error instanceof DOMException && error.name === "AbortError") throw error;
        throw new CustomerGatewayError("NETWORK", "Backend is unavailable");
      }

      if (!response.ok) throw await problemFor(response);
      return mapOrder((await response.json()) as Record<string, unknown>);
    },

    async createServiceRequest(sessionToken, type, note, signal) {
      let response: Response;
      try {
        response = await fetchWithTimeout(`${apiBase}/api/v1/customer/service-requests`, {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ sessionToken, type, note }),
          signal,
        });
      } catch (error) {
        if (error instanceof DOMException && error.name === "AbortError") throw error;
        throw new CustomerGatewayError("NETWORK", "Backend is unavailable");
      }

      if (!response.ok) throw await problemFor(response);
      return (await response.json()) as ServiceRequest;
    },

    async getOrder(orderId, sessionToken, signal) {
      let response: Response;
      try {
        response = await fetchWithTimeout(`${apiBase}/api/v1/customer/orders/${orderId}`, {
          headers: { "X-Customer-Session": sessionToken },
          signal,
        });
      } catch (error) {
        if (error instanceof DOMException && error.name === "AbortError") throw error;
        throw new CustomerGatewayError("NETWORK", "Backend is unavailable");
      }

      if (!response.ok) throw await problemFor(response);
      return mapOrder((await response.json()) as Record<string, unknown>);
    },

    async watchOrder(orderId, sessionToken, onOrder, onError) {
      const gateway = createHttpCustomerGateway(apiBase);
      const connection = new HubConnectionBuilder()
        .withUrl(`${apiBase}/hubs/v1/customer-orders`)
        .withAutomaticReconnect()
        .build();
      const receive = (order: Order) => onOrder(order);
      connection.on("orderStatusChanged", receive);
      connection.onreconnected(async () => {
        try {
          await connection.invoke("SubscribeToOrder", orderId, sessionToken);
          onOrder(await gateway.getOrder(orderId, sessionToken));
        } catch {
          onError();
        }
      });
      connection.onclose(onError);

      try {
        await connection.start();
        // Pass Guid-shaped string; hub binds to Guid. Do not send a 3rd CancellationToken —
        // the JS client protocol only has these two arguments.
        await connection.invoke("SubscribeToOrder", orderId, sessionToken);
      } catch (error) {
        await connection.stop();
        const detail = error instanceof Error ? error.message : "Realtime connection is unavailable";
        throw new CustomerGatewayError("NETWORK", detail);
      }

      return async () => {
        connection.off("orderStatusChanged", receive);
        if (connection.state !== HubConnectionState.Disconnected) await connection.stop();
      };
    },
  };
};
