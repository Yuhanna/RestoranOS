# Restaurant Web

Restaurant Web is the authenticated restaurant operations slice. It uses the
real management API by default; there is no bundled mock or demo account.

## Run locally

Start `RestaurantOS.Api` with Visual Studio's `https` profile, then run:

```powershell
corepack enable
pnpm install
pnpm restaurant:dev
```

The Vite server opens on `http://localhost:5174` and proxies `/api` and `/hubs`
to `https://localhost:7297`. This keeps the HttpOnly, Secure, SameSite=Strict
refresh cookie first-party in local development. For a separately hosted API,
set `VITE_MANAGEMENT_API_BASE_URL` to its HTTPS origin and add the frontend
origin to `Cors:AllowedOrigins`.

Login requires an existing management user, tenant membership, and branch.
`Order.View` is required for the list and SignalR connection; `Order.Modify` is
required for status changes. The Masalar screen requires `Table.View`; creating
or rotating QR codes requires `Table.Edit`. Opaque QR tokens appear only in the
generate response, never in list payloads. The Menü screen requires `Menu.View`;
creating categories/products requires `Menu.Edit`; publish/archive requires
`Menu.Publish`.

Access tokens are kept only in JavaScript memory. The refresh token is an
HttpOnly cookie and all auth calls use `credentials: include`. Startup attempts
one refresh; a protected REST 401 triggers one shared refresh and one retry.
Refresh failure clears the in-memory session.

SignalR uses the standard `accessTokenFactory`. Browsers may place the bearer
token in the WebSocket/SSE `access_token` query string because those transports
cannot set an Authorization header. The backend accepts that parameter only on
`/hubs/v1/management-orders`. Do not enable query-string/request-URL logging at
proxies or ingress; redact `access_token` if URL logging is unavoidable. The
hub validates JWT, active membership, `Order.View`, tenant, and branch before
joining a server-selected group. Reconnection refreshes credentials and then
performs a REST resync because SignalR events are not durable.

## Validate

```powershell
pnpm restaurant:format
pnpm restaurant:lint
pnpm restaurant:typecheck
pnpm restaurant:test
pnpm restaurant:build
```
