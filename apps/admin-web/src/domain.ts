export type Session = {
  accessToken: string;
  expiresAtUtc: string;
  userId: string;
  tenantId: string;
  branchId: string;
  realm: string;
  roleCode: string | null;
  email: string | null;
};

export type PlanPrice = {
  id: string;
  productCode: string;
  productKind: string;
  displayName: string;
  interval: string;
  currency: string;
  amountMinor: number;
  taxInclusive: boolean;
  status: string;
  createdAtUtc: string;
  publishedAtUtc: string | null;
  archivedAtUtc: string | null;
};

export type CatalogProduct = {
  productCode: string;
  productKind: string;
  displayName: string;
  maxBranches: number | null;
  maxTablesPerBranch: number | null;
  maxActiveUsers: number | null;
  canUseLiveOrderPanel: boolean | null;
  canUseMultiBranch: boolean | null;
  hasPrioritySupport: boolean | null;
  prices: PlanPrice[];
};

export type Catalog = {
  products: CatalogProduct[];
  note: string;
};

export type SubscriptionOffer = {
  id: string;
  audience: string;
  targetPlanCode: string;
  discountPercent: number;
  durationMonths: number;
  title: string;
  body: string;
  startsAtUtc: string;
  endsAtUtc: string | null;
  isActive: boolean;
};

export type ViewName = "overview" | "catalog" | "campaigns";
