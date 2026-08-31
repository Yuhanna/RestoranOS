import { createHttpCustomerGateway } from "./httpCustomerGateway";
import { mockCustomerGateway } from "./mockCustomerGateway";
import { resolveCustomerApiBaseUrl } from "./resolveCustomerApiBaseUrl";
import type { CustomerGateway } from "../domain/customer";

const gatewayCache = new Map<string, CustomerGateway>();

/** Resolve on demand; reuse gateway instances so effects are not aborted every render. */
export function getCustomerGateway(qrToken?: string | null): CustomerGateway {
  const apiBaseUrl = resolveCustomerApiBaseUrl(qrToken);
  const cacheKey = apiBaseUrl ?? "__mock__";
  const cached = gatewayCache.get(cacheKey);
  if (cached) {
    return cached;
  }

  const gateway =
    apiBaseUrl !== undefined ? createHttpCustomerGateway(apiBaseUrl) : mockCustomerGateway;
  gatewayCache.set(cacheKey, gateway);
  return gateway;
}
