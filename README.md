# Restaurant OS

Restaurant OS is a cloud-first, multi-tenant restaurant operating system. The
first commercial release focuses on secure QR-based dine-in ordering while the
codebase preserves clear boundaries for later product streams.

## Current scope

This repository contains the Release 0/1 foundation:

- self-serve restaurant registration (account → restaurant → branch → owner)
- ASP.NET Core API on .NET 10 (`Controllers` / `Models/Dto`)
- ASP.NET Core MVC management site (`RestaurantOS.Web`) that calls the API
- ASP.NET Core MVC Pasa Admin (`RestaurantOS.Admin`, `admin.pasa.app`) that calls `/api/v1/platform`
- setup checklist after signup (tables → QR → menu → publish)
- SQL Server / Entity Framework Core infrastructure
- Problem Details error handling
- liveness and database-readiness health checks
- correlation IDs, structured logging scope, and baseline security headers
- secure opaque QR resolution, customer session, published-menu, idempotent ordering, and realtime order tracking APIs
- API integration tests covering tenant/branch isolation and safe failure behavior
- React 19 / Vite mobile-first customer web
- React 19 / Vite authenticated restaurant operations web
- guarded QR session entry with an explicit mock gateway adapter
- localized menu search and category filtering
- product modifiers, cart, idempotent order submission boundary, and tracking
- shared frontend design tokens and reusable primitives

No future roadmap domain has been implemented prematurely.

## Repository layout

```text
apps/api/src/
  RestaurantOS.Api
    Controllers/
    Models/Dto/
  RestaurantOS.Application
  RestaurantOS.Domain
  RestaurantOS.Infrastructure
apps/api/tests/
  RestaurantOS.Api.Tests
apps/web/
  RestaurantOS.Web          # MVC management UI + server-side API client
apps/admin/
  RestaurantOS.Admin        # Pasa Admin MVC (admin.pasa.app), not the restaurant panel
  RestaurantOS.Admin.Tests
apps/customer-web/
  src
apps/restaurant-web/
  src
packages/design-system/
docs/
  MASTER_CURSOR_DEVELOPMENT_SPECIFICATION.md
```

## Prerequisites

- .NET SDK 10.0.300 or a compatible newer 10.0 feature band
- Microsoft SQL Server or SQL Server LocalDB
- Node.js 22 or newer (only for React apps)
- pnpm 10 (via Corepack; only for React apps)

Override `ConnectionStrings__RestaurantOs` outside local development. Secrets
must be supplied by the deployment environment and must not be committed.

For local development, either keep the non-secret LocalDB default or use .NET
user secrets:

```powershell
dotnet user-secrets set "ConnectionStrings:RestaurantOs" "<your-local-connection-string>" --project apps/api/src/RestaurantOS.Api
dotnet user-secrets set "ManagementAuth:SigningKey" "<at-least-32-random-bytes>" --project apps/api/src/RestaurantOS.Api
dotnet ef database update --project apps/api/src/RestaurantOS.Infrastructure --startup-project apps/api/src/RestaurantOS.Api
```

`ManagementAuth:SigningKey` is intentionally empty in committed configuration;
the API fails closed at startup when it is absent or shorter than 32 UTF-8
bytes. Generate a different high-entropy value for each environment. Access
tokens are signed JWTs with a 10-minute lifetime. Refresh tokens are rotated
opaque values kept only in an HttpOnly, Secure, SameSite=Strict cookie; the
database stores their SHA-256 digests, never plaintext.

To create the first local management membership, first apply migrations and
ensure the selected tenant and branch already exist. Put the one-time inputs in
user secrets, run the Development-only bootstrap command, then remove the
bootstrap password:

```powershell
dotnet user-secrets set "BootstrapAdmin:Email" "owner@local.test" --project apps/api/src/RestaurantOS.Api
dotnet user-secrets set "BootstrapAdmin:Password" "Local-Dev-Pass!42" --project apps/api/src/RestaurantOS.Api
dotnet user-secrets set "BootstrapAdmin:TenantId" "11111111-1111-1111-1111-111111111111" --project apps/api/src/RestaurantOS.Api
dotnet user-secrets set "BootstrapAdmin:BranchId" "33333333-3333-3333-3333-333333333333" --project apps/api/src/RestaurantOS.Api
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet run --project apps/api/src/RestaurantOS.Api --no-launch-profile -- --bootstrap-management-admin
```

Development bootstrap now also creates the demo tenant / restaurant / branch when missing.
Default login values in RestaurantOS.Web match these IDs. After first login works, you may
remove the bootstrap password secret:

```powershell
dotnet user-secrets remove "BootstrapAdmin:Password" --project apps/api/src/RestaurantOS.Api
```

The bootstrap command cannot run outside `Development` and does not create a
production user, tenant, branch, or committed password.

The QR and customer-session tokens are random opaque values. Only SHA-256
digests are stored; restaurant, branch, and table identifiers must never be
used as QR tokens.

## Validate

```powershell
dotnet restore RestaurantOS.slnx
dotnet format RestaurantOS.slnx --verify-no-changes
dotnet build RestaurantOS.slnx --no-restore
dotnet test RestaurantOS.slnx --no-build
dotnet list RestaurantOS.slnx package --vulnerable --include-transitive
```

Run the API:

```powershell
dotnet run --project apps/api/src/RestaurantOS.Api --launch-profile https
```

Run the MVC management site (server-side API client; default API base
`https://localhost:7297`):

```powershell
dotnet run --project apps/web/RestaurantOS.Web --launch-profile https
```

Run Pasa Admin (separate MVC; not `RestaurantOS.Web`):

```powershell
dotnet run --project apps/admin/RestaurantOS.Admin --launch-profile https
```

- API HTTPS: `https://localhost:7297` (HTTP `http://localhost:5183`)
- Web HTTPS: `https://localhost:7288` (HTTP `http://localhost:5288`)
- Admin HTTPS: `https://localhost:7295` (HTTP `http://localhost:5295`)

- `GET /api/v1/system` — versioned service metadata
- `POST /api/v1/customer/sessions/resolve` — validates an opaque QR, creates a
  short-lived customer session, and returns only the branch's latest published menu
- `POST /api/v1/customer/orders` — creates an order using `Idempotency-Key`
  and a customer session token
- `GET /api/v1/customer/orders/{orderId}` — reads only an order owned by the
  opaque token in `X-Customer-Session`
- `POST /api/v1/management/auth/login` — verifies credentials and a server-side
  tenant/branch membership; returns a short-lived access token
- `POST /api/v1/management/auth/refresh` — rotates the HttpOnly refresh cookie;
  reuse revokes the complete token family
- `POST /api/v1/management/auth/logout` — revokes the refresh-token family
- `GET /api/v1/management/orders/active` — requires `Order.View` and revalidates
  active membership/permission in the database
- `PUT /api/v1/management/orders/{orderId}/status` — requires `Order.Modify`,
  enforces claim scope against server-side membership and order tenant/branch,
  and writes the status change plus audit record atomically before SignalR
- `GET /api/v1/management/tables` — requires `Table.View`
- `POST /api/v1/management/tables` — requires `Table.Edit`; labels are unique per branch
- `PATCH /api/v1/management/tables/{tableId}` — rename or activate/deactivate
- `GET /api/v1/management/tables/{tableId}/qr-codes` — lists statuses only; never returns tokens
- `POST /api/v1/management/tables/{tableId}/qr-codes` — creates an opaque QR, deactivates the previous active code, and returns the plaintext token once
- `POST /api/v1/management/qr-codes/{id}/activate|deactivate|revoke`
- `GET /api/v1/management/qr-codes/{id}/print` — decrypts the stored payload for SVG print; revoked codes cannot be printed
- `GET/POST /api/v1/management/menus` — `Menu.View` / `Menu.Edit`; drafts start unpublished
- `GET/PATCH /api/v1/management/menus/{id}`
- `POST /api/v1/management/menus/{id}/publish|unpublish|archive` — `Menu.Publish`; publish requires an available product and unpublishes other live menus in the branch
- `POST /api/v1/management/menus/{id}/categories` and `PATCH /api/v1/management/menu-categories/{id}`
- `POST /api/v1/management/menus/{id}/items` and `PATCH /api/v1/management/menu-items/{id}`
- `PUT /api/v1/management/menu-categories/{id}/translations/{locale}` — `tr` or `en`
- `PUT /api/v1/management/menu-items/{id}/translations/{locale}` — customer QR resolve falls back to `tr` then catalog text
- `/hubs/v1/customer-orders` — SignalR hub; `SubscribeToOrder(orderId,
  sessionToken)` verifies the active customer session and order ownership before
  joining an order group, then emits `orderStatusChanged`
- `/hubs/v1/management-orders` — authenticated SignalR hub; validates active
  membership and `Order.View`, then joins only the token's tenant/branch group
- `GET /health/live` — process liveness
- `GET /health/ready` — SQL Server readiness

## Microsoft Visual Studio (TR / EN)

### Türkçe — yarın açma adımları

1. `RestaurantOS.slnx` dosyasını Visual Studio 2026 (veya .NET 10 + `.slnx`
   destekleyen sürüm) ile açın.
2. **RestaurantOS.Api** için user-secrets: `ManagementAuth:SigningKey` en az
   32 UTF-8 bayt olacak şekilde ayarlayın
   (`Manage User Secrets` veya aşağıdaki `dotnet user-secrets` komutu).
3. Tüm migration'ları uygulayın:

```powershell
dotnet ef database update --project apps/api/src/RestaurantOS.Infrastructure --startup-project apps/api/src/RestaurantOS.Api
```

4. Multiple startup: Solution Properties → Multiple startup projects →
   **RestaurantOS.Api**, **RestaurantOS.Web** ve (Pasa Admin için)
   **RestaurantOS.Admin** için Action = Start.
   `https` launch profile seçili olsun
   (Api: `https://localhost:7297`, Web: `https://localhost:7288`,
   Admin: `https://localhost:7295`).
   Alternatif: Api'ye F5, sonra Web veya Admin'e F5.
5. **Test > Test Explorer** → **Run All** (35+ entegrasyon testi).

### English — open and run tomorrow

1. Open `RestaurantOS.slnx` in Visual Studio 2026 (or a build that supports
   .NET 10 and `.slnx`).
2. Set `ManagementAuth:SigningKey` via user secrets on **RestaurantOS.Api**
   (at least 32 UTF-8 bytes).
3. Apply **all** EF migrations:

```powershell
dotnet ef database update --project apps/api/src/RestaurantOS.Infrastructure --startup-project apps/api/src/RestaurantOS.Api
```

4. Configure **multiple startup projects**: start **RestaurantOS.Api**,
   **RestaurantOS.Web**, and (for Pasa Admin) **RestaurantOS.Admin** with the
   `https` profiles (Api `https://localhost:7297`, Web `https://localhost:7288`,
   Admin `https://localhost:7295`).
   Or press F5 on Api, then F5 on Web or Admin.
5. Open **Test Explorer** and **Run All** (35+ tests).

`RestaurantOS.Web` keeps the access token in ASP.NET session and the API
refresh cookie in a **server-side** `CookieContainer` (ZiraatApp-style). The
browser never sees the API refresh cookie. Configure `Api:BaseUrl` in
`apps/web/RestaurantOS.Web/appsettings.json` if the API port changes.
`RestaurantOS.Admin` uses the same pattern against `/api/v1/platform/auth`
(`apps/admin/RestaurantOS.Admin/appsettings.json`). Do not add Pasa Admin
features to `RestaurantOS.Web` `/admin` or `/platform`.

Pending migrations include customer foundation, order lifecycle, management
security, table QR, menu archive, and menu translations.

The API launch profile also serves the SignalR hubs. Trust the ASP.NET Core
development certificate if the browser asks. Restaurant Web's Vite proxy still
targets `https://localhost:7297` by default. React apps remain available and
are not replaced by the MVC site. Pasa Admin is the separate MVC project
`RestaurantOS.Admin`; the React `apps/admin-web` tree is frozen.

The management order-status endpoint derives tenant and branch from the signed
access token, revalidates the membership and permission in SQL on every request,
and then calls `ICustomerExperienceService.ChangeOrderStatusAsync`. The service
enforces lifecycle transitions, optimistic concurrency, atomic audit
persistence, and post-commit SignalR publication.

SignalR uses the in-process backplane by default. For multiple API instances,
set the optional `ConnectionStrings__SignalRRedis` deployment secret to enable
the Redis backplane. Do not set it for ordinary local development. Reconnects
resubscribe and then REST-resync because SignalR delivery is not durable.
The management hub uses SignalR's standard `accessTokenFactory`; browser
WebSocket/SSE transports can carry the bearer token as `access_token` in the
query string. The API accepts this only for the management hub path. Production
reverse proxies must redact that parameter and must not log full hub query
strings.

Install and run Customer Web / Restaurant Web (React):

```powershell
corepack enable
pnpm install
pnpm web:dev
pnpm restaurant:dev
```

The menu requires QR context. Use the demo entry shown at
`http://localhost:5173`, or navigate directly to
`http://localhost:5173/?qr=demo-marea-table-7`.

Validate Customer Web:

```powershell
pnpm web:format
pnpm web:lint
pnpm web:typecheck
pnpm web:test
pnpm web:build
pnpm restaurant:format
pnpm restaurant:lint
pnpm restaurant:typecheck
pnpm restaurant:test
pnpm restaurant:build
```

The frontend keeps mock mode by default. Set `VITE_CUSTOMER_API_BASE_URL` to
the API origin to use the real HTTP adapter. See
[`apps/customer-web/README.md`](apps/customer-web/README.md).

Architecture and delivery boundaries are documented in
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).
