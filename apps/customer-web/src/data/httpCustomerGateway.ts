import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import {
  CustomerGatewayError,
  type CustomerGateway,
  type CustomerSession,
  type Order,
  type ServiceRequest,
  type SubmitOrderRequest,
} from "../domain/customer";

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

const mapOrder = (raw: Record<string, unknown>): Order => {
  const total = raw.total as { amountMinor?: number; currency?: string } | undefined;
  return {
    id: String(raw.id ?? ""),
    displayNumber: String(raw.displayNumber ?? ""),
    status: raw.status as Order["status"],
    statusChangedAt: String(raw.statusChangedAt ?? raw.statusChangedAtUtc ?? ""),
    estimatedReadyAt: String(raw.estimatedReadyAt ?? raw.estimatedReadyAtUtc ?? ""),
    total: {
      amountMinor: Number(total?.amountMinor ?? raw.amountMinor ?? 0),
      currency: (total?.currency as "TRY") ?? "TRY",
    },
  };
};

export const createHttpCustomerGateway = (baseUrl: string): CustomerGateway => {
  const apiBase = baseUrl.replace(/\/+$/, "");

  return {
    async resolveQr(qrToken, signal, locale = "tr") {
      let response: Response;
      try {
        response = await fetch(`${apiBase}/api/v1/customer/sessions/resolve`, {
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
      const session = (await response.json()) as CustomerSession & {
        activeOrders?: Record<string, unknown>[];
      };
      return {
        ...session,
        categories: [
          { id: "all", name: session.locale === "en" ? "All" : "Tümü" },
          ...session.categories,
        ],
        activeOrders: (session.activeOrders ?? []).map((item) =>
          mapOrder(item as Record<string, unknown>),
        ),
        openServiceRequestTypes: session.openServiceRequestTypes ?? [],
      };
    },

    async submitOrder(request: SubmitOrderRequest, signal) {
      let response: Response;
      try {
        response = await fetch(`${apiBase}/api/v1/customer/orders`, {
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
        response = await fetch(`${apiBase}/api/v1/customer/service-requests`, {
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
        response = await fetch(`${apiBase}/api/v1/customer/orders/${orderId}`, {
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
