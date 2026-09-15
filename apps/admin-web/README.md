# Admin Web (`admin.pasa.app`)

Pasa iç yönetim SPA’sı. Restoran paneli (`apps/restaurant-web`) ve müşteri QR uygulamasından ayrıdır.

| | |
|--|--|
| Yerel | `http://localhost:5175` |
| Canlı host | `admin.pasa.app` |
| API | `/api/v1/platform/*` |
| Auth | Ayrı JWT audience (`restaurant-os-platform`), tenant/şube claim’i yok |

## Çalıştırma

API’yi Visual Studio `https` veya `http://127.0.0.1:5183` ile açın, sonra:

```powershell
corepack enable
pnpm install
pnpm admin:dev
```

Geliştirme girişi: `platform@local.test` (parola bootstrap ayarından). Restoran hesabı (`owner@local.test`) reddedilir.

## Kontroller

```powershell
pnpm admin:format
pnpm admin:lint
pnpm admin:typecheck
pnpm admin:test
pnpm admin:build
```

Spec’teki `apps/platform-web` adı bu uygulamaya karşılık gelir; MVC `/platform` geçici iskelettir, yeni özellik buraya yazılır.
