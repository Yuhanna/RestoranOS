# Oturum modeli (Release 1)

Spec §26–§28 ile uyum özeti.

## TableSession

Domain’de `CustomerSession` entity’si **TableSession** (masa oturumu) olarak modellenir:

- QR çözümünde oluşturulur
- Menü ve sipariş API’si `sessionToken` ile bu oturuma bağlanır
- `CustomerOrders.CustomerSessionId` → TableSession kimliği

## GuestSession

Spec §26 guest alanları:

| Alan | Uygulama |
|------|----------|
| TableSessionId | `GuestSession.TableSessionId` → `CustomerSession.Id` |
| DeviceIdentifier | İsteğe bağlı `X-Device-Id` |
| IpHash | SHA-256 hash (ham IP saklanmaz) |
| Language | QR locale |
| RiskScore | Başlangıç 0; ileride guard ile genişletilebilir |
| Status | `Active` / `Closed` |

QR çözümünde TableSession ile birlikte bir GuestSession oluşturulur. Siparişte `CustomerOrders.GuestSessionId` doldurulur.

## Sonraki adımlar

- Grup siparişi için TableSession başına birden fazla GuestSession
- `OrderChannel`, `FulfillmentType` alanları (spec §29)
- Geniş state machine (spec §30)
