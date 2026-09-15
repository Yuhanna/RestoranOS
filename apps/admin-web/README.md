# Admin Web (`admin.pasa.app`)

Pasa iç yönetim SPA’sı. Restoran paneli (`apps/restaurant-web`) ve müşteri QR uygulamasından ayrıdır.

| | |
|--|--|
| Yerel | `http://localhost:5175` |
| Canlı host | `admin.pasa.app` |
| API | `/api/v1/platform/*` |
| Auth | Ayrı JWT audience (`restaurant-os-platform`), tenant/şube claim’i yok |

## Visual Studio

Bu uygulama `RestaurantOS.Web` (MVC, `app.pasa.app` / `localhost:5288`) **değildir**. Solution Explorer’da **apps/admin-web (admin.pasa.app)** klasöründedir.

1. Startup: **RestaurantOS.Api** (F5) — `http://localhost:5183`
2. `PasaAdmin.esproj` dosyasına çift tıklayın veya Visual Studio Terminal’de repo kökünden:

```powershell
corepack enable
pnpm install
pnpm admin:dev
```

3. Tarayıcı: **http://localhost:5175** (canlıda **admin.pasa.app**)

Geliştirme girişi: `platform@local.test` (parola `BootstrapAdmin:Password` user-secret). Restoran hesabı (`owner@local.test`) reddedilir.

MVC `http://localhost:5288/platform` geçici iskelettir; yeni Pasa Admin özellikleri buraya yazılmaz.

## Kontroller

```powershell
pnpm admin:format
pnpm admin:lint
pnpm admin:typecheck
pnpm admin:test
pnpm admin:build
```

Spec’teki `apps/platform-web` adı bu uygulamaya karşılık gelir; MVC `/platform` geçici iskelettir, yeni özellik buraya yazılır.
