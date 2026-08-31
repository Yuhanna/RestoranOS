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

`mockCustomerGateway` remains the default development adapter for the demo QR
(`demo-marea-table-7`). When you scan a real table QR in development, the app
automatically uses the ASP.NET Core API through the Vite dev proxy (`/api` →
`http://127.0.0.1:5183`), including from phones on the same Wi‑Fi/LAN.

To point at a specific API host (for example HTTPS or a remote backend), create
an uncommitted `.env.local` file:

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
