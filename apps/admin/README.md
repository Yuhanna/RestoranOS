# Pasa Admin (`admin.pasa.app`)

İç yönetim MVC uygulaması. Restoran paneli (`RestaurantOS.Web` / `app.pasa.app`) ve müşteri QR uygulamasından ayrıdır.

| | |
|--|--|
| Yerel HTTP | `http://localhost:5295` |
| Yerel HTTPS | `https://localhost:7295` |
| Canlı host | `admin.pasa.app` |
| API | `/api/v1/platform/*` |
| Auth | JWT `aud=restaurant-os-platform`, `realm=platform` |

## Visual Studio

Solution Explorer’da **apps/admin (admin.pasa.app) → RestaurantOS.Admin** projesidir. `RestaurantOS.Web` içine karışmaz.

1. Startup: **RestaurantOS.Api** (F5) — HTTP `http://localhost:5183`, HTTPS `https://localhost:7297`
2. **RestaurantOS.Admin** için `https` profili (F5)
3. Tarayıcı: **https://localhost:7295** (veya HTTP `http://localhost:5295`)

Geliştirme girişi: `platform@local.test` / `Local-Dev-Pass!42`. Restoran hesabı (`owner@local.test`) `PLATFORM_ACCESS_DENIED` ile reddedilir.

`RestaurantOS.Web` içindeki `/admin` ve `/platform` geçici iskelettir; yeni Pasa Admin özelliği oraya yazılmaz.

Komut satırı:

```powershell
dotnet run --project apps/admin/RestaurantOS.Admin --launch-profile https
```

`Api:BaseUrl` Development’ta `http://localhost:5183`, diğer ortamda `https://localhost:7297`. API portu değişirse `appsettings*.json` güncellenir.

## Kontroller

```powershell
dotnet build apps/admin/RestaurantOS.Admin/RestaurantOS.Admin.csproj -c Release
dotnet test apps/admin/RestaurantOS.Admin.Tests/RestaurantOS.Admin.Tests.csproj -c Release
```
