# Architecture

## Decision

Restaurant OS starts as a modular monolith. A single deployable ASP.NET Core API
hosts independently bounded modules; module extraction is a future option, not
an initial deployment requirement.

## Backend dependency rule

```text
Api -> Application -> Domain
Api -> Infrastructure -> Application -> Domain
```

- `Domain` owns business concepts and invariants and has no infrastructure
  dependency.
- `Application` owns use cases and module contracts.
- `Infrastructure` implements persistence and external concerns.
- `Api` is the composition root and exposes versioned DTO-based HTTP contracts.

Modules must not access another module's persistence internals. Cross-module
work will use explicit application contracts or events as each release adds the
relevant module.

## Persistence

SQL Server is the system of record and Entity Framework Core is the persistence
technology. `RestaurantOsDbContext` now owns the first active customer
experience schema: tenant/restaurant/branch/table, hashed table QR,
short-lived hashed customer sessions, published menus, and idempotent order
snapshots. The schema ships with explicit constraints, indexes, a design-time
factory, and a migration.

Persisted timestamps will use UTC and monetary values will use `decimal` with
an explicit currency. EF entities will never be serialized as public API
responses.

## Multi-tenancy and security

The tenant hierarchy is `Organization -> Restaurant -> Branch`. Tenant context
will be derived from authenticated server-side context; client-provided tenant
identifiers are never authoritative. Every domain endpoint must include
authorization and tenant-isolation tests when introduced.

The foundation supplies Problem Details, correlation IDs, baseline response
headers, and separate liveness/readiness probes. Public customer access is
limited to opaque QR and customer-session tokens whose digests are stored.
Administrative access uses short-lived signed JWT access tokens and rotating
opaque refresh tokens in an HttpOnly/Secure/SameSite=Strict cookie. Only refresh
token digests are persisted. Access-token tenant and branch claims are selectors,
not authority: each protected request revalidates the active user membership,
branch ownership, role, and permission against SQL. Login combines configurable
IP rate limiting with per-user temporary lockout, and refresh-token replay
revokes the complete token family.

Management roles, explicit role permissions, branch-scoped memberships, refresh
sessions, and audit records are persisted in separate `identity` and `audit`
schemas. Order status and its audit record share one EF Core save operation;
SignalR publication occurs only after that operation commits.

## Observability

- `X-Correlation-ID` is accepted when bounded and returned on every response.
- The correlation ID is attached to the logging scope.
- `/health/live` verifies process liveness without external dependencies.
- `/health/ready` verifies SQL Server connectivity.

## Next release order

1. QR provisioning lifecycle and broader management permissions
2. Restaurant management web login and active-order screen
3. TableSession and multi-guest policy
4. Localized menu content and modifiers
5. Broader risk controls and audit-query operations

Each stream must include migrations, DTO contracts, validation, authorization,
tenant isolation, tests, and relevant documentation.
