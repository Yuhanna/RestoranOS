# Customer Web

Mobile-first QR menu and ordering client for Restaurant OS.

## Run locally

From the repository root:

```powershell
corepack enable
pnpm install
pnpm web:dev
```

Open `http://localhost:5173`. The root route intentionally blocks menu access
without a QR context. Use the on-screen demo action or open:

```text
http://localhost:5173/?qr=demo-marea-table-7
```

`mockCustomerGateway` remains the default development adapter. To use the real
ASP.NET Core API, create an uncommitted `.env.local` file:

```text
VITE_CUSTOMER_API_BASE_URL=https://localhost:7297
```

The HTTP adapter resolves `POST /api/v1/customer/sessions/resolve`, sends
orders to `POST /api/v1/customer/orders` with an `Idempotency-Key` header, and
reads tracking state from `GET /api/v1/customer/orders/{orderId}` using the
`X-Customer-Session` header. It subscribes to
`/hubs/v1/customer-orders` with SignalR and performs a REST resync after every
reconnect. Mock mode remains independent of HTTP and SignalR. The
UI never derives or accepts restaurant, branch, or table identifiers
independently of the resolved server session. The backend database intentionally
has no predictable demo QR seed; provision opaque QR data through trusted
administrative tooling or test fixtures.

## Validate

```powershell
pnpm web:format
pnpm web:lint
pnpm web:typecheck
pnpm web:test
pnpm web:build
```
