import { HubConnectionBuilder, HubConnectionState } from "@microsoft/signalr";
import {
  CustomerGatewayError,
  type CustomerGateway,
  type CustomerMenuSettings,
  type CustomerSession,
  type LunchPackage,
  type Order,
  type Product,
  type ServiceRequest,
  type SubmitOrderRequest,
} from "../domain/customer";
import { resolveProductMediaUrl } from "../lib/resolveProductMediaUrl";

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

  if (code === "INVALID_SESSION") {
    return new CustomerGatewayError(
      "INVALID_SESSION",
      problem.detail ?? "Customer session is invalid.",
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

  if (code === "ORDER_RATE_LIMITED" || code === "ORDER_BLOCKED") {
    return new CustomerGatewayError(
      code === "ORDER_BLOCKED" ? "ORDER_BLOCKED" : "ORDER_RATE_LIMITED",
      problem.detail ??
        "Çok hızlı sipariş verdiniz. Birkaç saniye bekleyip yeniden deneyin.",
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

const mapCustomerMenu = (raw: unknown): CustomerMenuSettings | undefined => {
  if (!raw || typeof raw !== "object") {
    return undefined;
  }

  const settings = raw as Record<string, unknown>;
  return {
    showDietaryFilters: settings.showDietaryFilters === true,
    dietaryFilterOptions: Array.isArray(settings.dietaryFilterOptions)
      ? (settings.dietaryFilterOptions as CustomerMenuSettings["dietaryFilterOptions"])
      : [],
    showAllergenExclusions: settings.showAllergenExclusions === true,
    allergenExclusionOptions: Array.isArray(settings.allergenExclusionOptions)
      ? (settings.allergenExclusionOptions as CustomerMenuSettings["allergenExclusionOptions"])
      : [],
    showProductNutrition: settings.showProductNutrition === true,
    showProductAllergens: settings.showProductAllergens === true,
    showProductModifiers: settings.showProductModifiers === true,
    allergenDisclaimer:
      typeof settings.allergenDisclaimer === "string" ? settings.allergenDisclaimer : undefined,
    allergenMatrixUrl:
      typeof settings.allergenMatrixUrl === "string" ? settings.allergenMatrixUrl : undefined,
  };
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
    discountKind: typeof raw.discountKind === "string" ? raw.discountKind : undefined,
    discountValue: typeof raw.discountValue === "number" ? raw.discountValue : undefined,
    discountPercent: typeof raw.discountPercent === "number" ? raw.discountPercent : undefined,
    promotionEndsAtUtc:
      typeof raw.promotionEndsAtUtc === "string" ? raw.promotionEndsAtUtc : undefined,
    imageUrl: resolveProductMediaUrl(String(raw.imageUrl ?? "")),
    imageAlt: String(raw.imageAlt ?? raw.name ?? ""),
    available: raw.available !== false,
    badge:
      typeof raw.badge === "string"
        ? raw.badge
        : typeof raw.promotionLabel === "string"
          ? raw.promotionLabel
          : undefined,
    dietaryTags: Array.isArray(raw.dietaryTags) ? (raw.dietaryTags as Product["dietaryTags"]) : [],
    customLabels: Array.isArray(raw.customLabels)
      ? raw.customLabels.map((label) => String(label).trim()).filter(Boolean)
      : [],
    allergenKeys: Array.isArray(raw.allergenKeys)
      ? (raw.allergenKeys as Product["allergenKeys"])
      : [],
    mayContainAllergenKeys: Array.isArray(raw.mayContainAllergenKeys)
      ? (raw.mayContainAllergenKeys as Product["mayContainAllergenKeys"])
      : undefined,
    ingredients: Array.isArray(raw.ingredients) ? (raw.ingredients as string[]) : undefined,
    isNew: raw.isNew === true,
    spiceLevel:
      typeof raw.spiceLevel === "number" ? (raw.spiceLevel as Product["spiceLevel"]) : undefined,
    prepTimeMinutes:
      typeof raw.prepTimeMinutes === "number" ? raw.prepTimeMinutes : undefined,
    containsAlcohol: raw.containsAlcohol === true,
    servingNote: typeof raw.servingNote === "string" ? raw.servingNote : undefined,
    modifierGroups: Array.isArray(raw.modifierGroups)
      ? (raw.modifierGroups as Product["modifierGroups"])
      : [],
    portions: Array.isArray(raw.portions)
      ? (raw.portions as Product["portions"])
      : undefined,
    priceLabel: typeof raw.priceLabel === "string" ? raw.priceLabel : undefined,
    certificationNotes:
      typeof raw.certificationNotes === "string" ? raw.certificationNotes : undefined,
    nutrition:
      raw.nutrition && typeof raw.nutrition === "object"
        ? {
            weightGrams:
              typeof (raw.nutrition as { weightGrams?: unknown }).weightGrams === "number"
                ? (raw.nutrition as { weightGrams: number }).weightGrams
                : undefined,
            volumeMl:
              typeof (raw.nutrition as { volumeMl?: unknown }).volumeMl === "number"
                ? (raw.nutrition as { volumeMl: number }).volumeMl
                : undefined,
            caloriesKcal:
              typeof (raw.nutrition as { caloriesKcal?: unknown }).caloriesKcal === "number"
                ? (raw.nutrition as { caloriesKcal: number }).caloriesKcal
                : undefined,
            proteinGrams:
              typeof (raw.nutrition as { proteinGrams?: unknown }).proteinGrams === "number"
                ? (raw.nutrition as { proteinGrams: number }).proteinGrams
                : undefined,
            carbsGrams:
              typeof (raw.nutrition as { carbsGrams?: unknown }).carbsGrams === "number"
                ? (raw.nutrition as { carbsGrams: number }).carbsGrams
                : undefined,
            fatGrams:
              typeof (raw.nutrition as { fatGrams?: unknown }).fatGrams === "number"
                ? (raw.nutrition as { fatGrams: number }).fatGrams
                : undefined,
            sugarGrams:
              typeof (raw.nutrition as { sugarGrams?: unknown }).sugarGrams === "number"
                ? (raw.nutrition as { sugarGrams: number }).sugarGrams
                : undefined,
            saltGrams:
              typeof (raw.nutrition as { saltGrams?: unknown }).saltGrams === "number"
                ? (raw.nutrition as { saltGrams: number }).saltGrams
                : undefined,
          }
        : undefined,
  };
};

const mapPackage = (raw: Record<string, unknown>): LunchPackage => {
  const price = (raw.price as { amountMinor?: number; currency?: string } | undefined) ?? {};
  const listPrice = (raw.listPrice as { amountMinor?: number; currency?: string } | undefined) ?? {};
  const discount = (raw.discount as { amountMinor?: number; currency?: string } | undefined) ?? {};
  const currency = (price.currency as "TRY") ?? "TRY";
  const priceMinor = Number(price.amountMinor ?? 0);
  const listMinor = Number(listPrice.amountMinor ?? priceMinor);
  const discountMinor = Number(discount.amountMinor ?? Math.max(0, listMinor - priceMinor));
  return {
    id: String(raw.id ?? ""),
    name: String(raw.name ?? ""),
    description: String(raw.description ?? ""),
    price: { amountMinor: priceMinor, currency },
    listPrice: { amountMinor: listMinor, currency },
    discount: { amountMinor: discountMinor, currency },
    components: Array.isArray(raw.components)
      ? raw.components.map((item) => {
          const component = item as Record<string, unknown>;
          return {
            menuItemId: String(component.menuItemId ?? ""),
            name: String(component.name ?? ""),
            slotLabel: typeof component.slotLabel === "string" ? component.slotLabel : null,
            listAmountMinor: Number(component.listAmountMinor ?? 0),
            imageUrl: typeof component.imageUrl === "string" ? component.imageUrl : undefined,
            imageAlt: typeof component.imageAlt === "string" ? component.imageAlt : undefined,
            description: typeof component.description === "string" ? component.description : undefined,
          };
        })
      : [],
    dailyStartLocal: typeof raw.dailyStartLocal === "string" ? raw.dailyStartLocal : null,
    dailyEndLocal: typeof raw.dailyEndLocal === "string" ? raw.dailyEndLocal : null,
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

      const body = payload as Record<string, unknown>;
      const products = (Array.isArray(body.products) ? body.products : []).map((item) =>
        mapProduct(item as Record<string, unknown>),
      );
      const packages = (Array.isArray(body.packages) ? body.packages : []).map((item) =>
        mapPackage(item as Record<string, unknown>),
      );
      return {
        sessionToken: String(body.sessionToken ?? ""),
        restaurantName: String(body.restaurantName ?? "Restoran"),
        branchName: String(body.branchName ?? ""),
        tableLabel: String(body.tableLabel ?? ""),
        locale: body.locale === "en" ? "en" : "tr",
        categories: [
          { id: "all", name: body.locale === "en" ? "All" : "Tümü" },
          ...((Array.isArray(body.categories)
            ? body.categories
            : []) as CustomerSession["categories"]),
        ],
        products,
        packages,
        customerMenu: mapCustomerMenu(body.customerMenu),
        activeOrders: (Array.isArray(body.activeOrders) ? body.activeOrders : []).map((item) =>
          mapOrder(item as Record<string, unknown>),
        ),
        openServiceRequestTypes: Array.isArray(body.openServiceRequestTypes)
          ? (body.openServiceRequestTypes as CustomerSession["openServiceRequestTypes"])
          : [],
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
