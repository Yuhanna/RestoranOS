import { createHttpCustomerGateway } from "./httpCustomerGateway";
import { mockCustomerGateway } from "./mockCustomerGateway";

const apiBaseUrl = import.meta.env.VITE_CUSTOMER_API_BASE_URL?.trim();

export const customerGateway = apiBaseUrl
  ? createHttpCustomerGateway(apiBaseUrl)
  : mockCustomerGateway;
