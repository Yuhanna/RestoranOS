# Platform Web (Release 1 interim)

Spec hedefi ayrı bir `platform-web` SPA’sıdır. Release 1’de platform operasyonları **MVC** üzerinden sunulur:

| Route | Açıklama |
|-------|----------|
| `/platform/account/login` | Platform yöneticisi girişi |
| `/platform` | Platform ana sayfa |
| `/platform/notifications` | Tenant bildirimleri |
| `/platform/subscription-offers` | Abonelik teklifleri |

API: `apps/api/src/RestaurantOS.Api/Controllers/PlatformControllers.cs`

Release 2+ için bu klasör ayrı Vite/React uygulamasına taşınabilir; `packages/contracts` paylaşılan sözleşmeleri içerir.
