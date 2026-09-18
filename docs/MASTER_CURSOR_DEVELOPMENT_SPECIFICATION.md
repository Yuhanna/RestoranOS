# RESTAURANT OS
## Master Cursor Development Specification v1.0

### Belge amacı

Bu belge Restaurant OS projesinin:

- ürün vizyonunu
- ilk ticari sürümünü
- teknik mimarisini
- domain sınırlarını
- güvenlik modelini
- QR mimarisini
- UX prensiplerini
- web/mobile mimarisini
- gelecekteki POS/ÖKC/marketplace/stok/ödeme/AI genişlemelerini
- test yaklaşımını
- Cursor geliştirme kurallarını
- release sırasını

tanımlar.

Bu belge, Cursor tarafından geliştirilecek projenin ana teknik ve ürün referansıdır.

---

# 1. ANA ÜRÜN TANIMI

Restaurant OS, QR siparişi ile başlayan ancak uzun vadede restoranın tüm operasyonunu yönetebilen:

**Cloud-first / Web-first / Multi-tenant Restaurant Operating System**

olacaktır.

Ürün:

> yalnızca QR menü değildir.

Ürün uzun vadede:

```text
QR Ordering
+
Table Management
+
Guest Sessions
+
Waiter
+
Kitchen
+
KDS
+
POS
+
Fiscal/ÖKC
+
Payment
+
Marketplace
+
Website Ordering
+
Inventory
+
Purchasing
+
Recipe
+
Cost
+
Staff
+
CRM
+
Loyalty
+
Analytics
+
Benchmark
+
AI
```

alanlarını kapsayacaktır.

Ancak ilk release bunların yalnızca küçük bir bölümünü çalıştıracaktır.

---

# 2. ANA GELİŞTİRME STRATEJİSİ

## Temel prensip

**Small first release, enterprise-ready foundation.**

İlk sürüm küçük olacaktır.

Fakat aşağıdaki alanlar baştan doğru tasarlanacaktır:

- tenant
- restaurant
- branch
- table
- QR
- menu
- localization
- session
- order
- payment boundary
- integration boundary
- security
- audit
- analytics
- future channel support

Gelecekteki özelliklerin tamamının çalışan kodunu ilk sürümde yazmak yasaktır.

Gelecekteki özelliklerin eklenmesini engelleyecek kötü mimari kurmak da yasaktır.

---

# 3. HEDEF

İlk ticari ürün şu problemi kusursuz çözmelidir:

> Restoran müşterisi masadaki QR kodu okutur, uygulama indirmeden menüyü görür, ürün seçer, sipariş verir ve siparişinin durumunu takip eder.

Restoran ise:

> QR'dan gelen siparişi hızlı şekilde görür ve durumunu yönetir.

---

# 4. İLK TİCARİ RELEASE'İN KAPSAMI

İlk release:

```text
Restaurant
Branch
User
Table
QR
Menu
Localization
Guest Session
Table Session
Cart
Order
Order Status
Basic ETA
Customer Tracking
Restaurant Order Dashboard
Basic Table Dashboard
Basic Security
Basic Reports
```

içerecektir.

---

# 5. İLK RELEASE'DE OLMAYACAK AMA MİMARİDE HAZIR OLACAK MODÜLLER

Bunlar ilk release'de görünmeyecek veya aktif edilmeyecektir:

```text
Waiter Mobile
Fixed Tablet
KDS Advanced
Digital Payment
Split Bill
Tips
POS
Fiscal / ÖKC
Marketplace
Website Ordering
Pickup
Delivery
Inventory
Recipe
Cost
Purchasing
Supplier
Staff Management
CRM
Loyalty
Benchmark
Restaurant Health
AI
Reservation
White Label
Hotel / Resort
```

Bu modüllerin domain/API/extension sınırları gerektiğinde dokümante edilecektir.

---

# 6. PROJE YAPISI

Tek repository kullanılacaktır.

```text
restaurant-os/
│
├── apps/
│   ├── api/
│   ├── customer-web/
│   ├── restaurant-web/
│   ├── platform-web/   (interim README → apps/admin)
│   ├── admin/          (Pasa Admin MVC, admin.pasa.app)
│   ├── staff-mobile/
│   └── edge-agent/
│
├── packages/
│   ├── contracts/
│   ├── shared-types/
│   ├── localization/
│   ├── design-system/
│   └── tooling/
│
├── docs/
│   ├── MASTER_CURSOR_DEVELOPMENT_SPECIFICATION.md
│   ├── PRODUCT.md
│   ├── ARCHITECTURE.md
│   ├── DOMAIN_MODEL.md
│   ├── API.md
│   ├── DATABASE.md
│   ├── SECURITY.md
│   ├── OFFLINE.md
│   ├── INTEGRATIONS.md
│   ├── DESIGN_SYSTEM.md
│   ├── UX.md
│   ├── MVP.md
│   ├── ROADMAP.md
│   └── ADR/
│
├── infra/
│   ├── docker/
│   ├── ci/
│   └── deployment/
│
└── README.md
```

---

# 7. UYGULAMA SAYISI

Başlangıçta repository içinde:

1. API
2. Customer Web
3. Restaurant Web
4. Platform Web
5. Staff Mobile
6. Edge Agent

bulunacaktır.

Ancak Cursor bunların tamamını aynı anda geliştirmeyecektir.

---

# 8. CURSOR ÇALIŞMA PRENSİBİ

Bir anda yalnızca **bir ana release / feature stream** aktif olacaktır.

Cursor:

- gelecekteki feature'ları kendiliğinden geliştirmeyecek
- başka release'e geçmeyecek
- scope'u genişletmeyecek
- “hazır gelmişken” başka domainleri değiştirmeyecek

Mevcut görev tamamlanmadan yeni büyük göreve başlanmayacaktır.

---

# 9. TECHNOLOGY BASELINE

## Backend

```text
C#
ASP.NET Core
Entity Framework Core
```

## Database

```text
Microsoft SQL Server
```

MSSQL kullanılacaktır.

PostgreSQL'e geçiş zorunlu değildir.

---

## Cache

```text
Redis
```

---

## Realtime

```text
SignalR
```

---

## Web

```text
React
TypeScript
```

---

## Mobile

```text
Flutter
Dart
SQLite
```

Android ilk hedef olacaktır.

Aynı Flutter codebase iOS'a hazır tutulacaktır.

---

## Background

```text
.NET Worker
```

---

## Messaging

Başlangıç:

```text
Transactional Outbox
+
Worker
```

RabbitMQ yalnızca gerçek ihtiyaç ortaya çıktığında eklenecektir.

---

## Edge

```text
.NET Worker / Windows Service
```

---

# 10. MİMARİ YAKLAŞIM

Başlangıç:

**Modular Monolith**

olacaktır.

Microservice kullanılmayacaktır.

Ancak domain sınırları gelecekte ayrı servis yapılabilecek şekilde çizilecektir.

---

# 11. ANA DOMAIN'LER

```text
Identity
Tenant
Restaurant
Branch
Table
Menu
Ordering
Kitchen
Payment
Fiscal
Inventory
Purchasing
Staff
CRM
Loyalty
Subscription
Integration
Analytics
Benchmark
AI
Notification
Security
Audit
```

İlk release'te yalnızca gereken domainler aktif olarak implement edilir.

---

# 12. DOMAIN BOUNDARY

Bir domain başka bir domainin database yapısına doğrudan erişemez.

Yanlış:

```text
OrderService
→ InventoryDbContext
→ modify Inventory
```

Doğru:

```text
Order
→ Domain/Application Event
→ Inventory
```

veya açık bir application contract.

---

# 13. MULTI-TENANCY

Temel:

```text
Organization
   ↓
Restaurant
   ↓
Branch
```

olacaktır.

Bir organization birden fazla restoran/şube içerebilir.

---

# 14. TENANT ISOLATION

Tenant izolasyonu UI güvenliği değildir.

Aşağıdaki katmanlarda korunacaktır:

```text
Authentication
Authorization
Application
Query
Command
Database
```

Client tarafından gönderilen RestaurantId güvenilir kabul edilmeyecektir.

---

# 15. KULLANICI ROLLERİ

İlk release:

```text
PlatformAdmin
RestaurantOwner
RestaurantManager
```

Gelecekte:

```text
Cashier
Waiter
KitchenStaff
InventoryManager
Viewer
```

eklenti olarak eklenebilir.

---

# 16. PERMISSION MODELİ

Rol dışında permission desteği olmalıdır.

Örnek:

```text
Restaurant.View
Restaurant.Edit

Menu.View
Menu.Edit
Menu.Publish

Order.View
Order.Create
Order.Modify
Order.Cancel

Table.View
Table.Edit

Report.View
```

---

# 17. QR MİMARİSİ

QR:

> Permanent Physical Table Gateway

olacaktır.

QR doğrudan güvenlik olarak kabul edilmeyecektir.

---

# 18. QR FLOW

```text
SCAN QR
   ↓
QR Resolver
   ↓
Validate QR
   ↓
Restaurant
   ↓
Branch
   ↓
Table
   ↓
Create/Resume TableSession
   ↓
Create GuestSession
   ↓
Issue Session Context
   ↓
Open Customer Web
   ↓
Menu
```

---

# 19. MÜŞTERİ RESTORAN / ŞUBE / MASA SEÇMEYECEK

Geçerli QR'dan sonra:

```text
Restaurant
Branch
Table
```

otomatik belirlenir.

Customer:

- restoran seçemez
- şube değiştiremez
- masa değiştiremez

---

# 20. QR OLMADAN MENÜ ERİŞİMİ

Ana Customer Web route'u doğrudan menü göstermeyecektir.

Örneğin:

```text
customer-web/
```

açıldığında:

```text
QR_SESSION_REQUIRED
```

gösterilir.

Müşteriye:

> Masanızdaki QR kodu okutun.

uyarısı verilir.

---

# 21. MENÜ API'Sİ DOĞRUDAN PUBLIC OLMAYACAK

Yanlış:

```text
GET /api/restaurants/{id}/menu
```

gibi herkesin kullanabileceği doğrudan customer menu endpoint'i.

Doğru:

```text
QR
 ↓
Session
 ↓
Customer Session Token
 ↓
Customer Menu API
```

Menu erişimi geçerli customer context üzerinden yapılmalıdır.

---

# 22. QR FİZİKSEL OLARAK OKUTULDU MU?

Sistem şunu iddia etmeyecektir:

> “Bu linkin kameradan okutulduğunu biliyorum.”

Bu web ortamında güvenilir şekilde kanıtlanamaz.

Sistem bunun yerine:

```text
Valid QR token
+
Valid Table
+
Valid GuestSession
+
Session Policy
+
Risk Control
```

kullanacaktır.

---

# 23. QR TOKEN

QR içine mümkün olduğunca:

- tahmin edilemez
- rastgele
- yeterince uzun
- opaque

bir token konulmalıdır.

RestaurantId/TableId sıradan integer olarak QR içine konulmamalıdır.

---

# 24. QR REVOCATION

QR:

```text
ACTIVE
INACTIVE
REVOKED
```

olabilir.

Revoke edilmiş QR yeniden customer session oluşturmamalıdır.

---

# 25. BİRDEN FAZLA QR

Aynı masa için birden fazla QR desteklenebilir.

Örneğin:

```text
QR-001 ACTIVE
QR-002 INACTIVE
QR-003 REVOKED
```

Ama QR rotation ana güvenlik mekanizması değildir.

---

# 26. GUEST SESSION

Fields:

```text
Id
TableSessionId
DeviceIdentifier
IpHash
Language
CreatedAt
LastActivityAt
RiskScore
Status
```

Gereksiz kişisel veri tutulmayacaktır.

---

# 27. TABLE SESSION

MVP'de TableSession bulunacaktır.

Örnek:

```text
Table 12
Session 10052
```

Order'lar bu session'a bağlanır.

---

# 28. GUEST SESSION / TABLE SESSION AYRIMI

```text
TableSession
   ├── GuestSession A
   ├── GuestSession B
   └── Orders
```

İlk sürümde tek guest ağırlıklı kullanılabilir.

Ancak veri modeli ileride group ordering'i desteklemelidir.

---

# 29. ORDER

Order entity'de geleceğe yönelik:

```text
OrderChannel
FulfillmentType
RestaurantId
BranchId
TableId
TableSessionId
GuestSessionId
CustomerId nullable
ExternalOrderId nullable
```

alanları için uygun tasarım yapılmalıdır.

İlk release'te:

```text
OrderChannel = DINE_IN_QR
FulfillmentType = DINE_IN
```

kullanılır.

---

# 30. ORDER STATE MACHINE

```text
DRAFT
SUBMITTED
RECEIVED
CONFIRMED
PREPARING
READY
SERVED
COMPLETED
CANCELLED
REJECTED
```

MVP'de kullanılan state'ler sınırlı olabilir.

Ancak state machine gelecekte genişletilebilir.

---

# 31. ORDER ITEM SNAPSHOT

OrderItem geçmişi değişmemelidir.

Snapshot:

```text
ProductName
UnitPrice
TaxRate
Discount
Modifier
ModifierPrice
```

saklanmalıdır.

---

# 32. MENU

MVP:

```text
Menu
Category
Product
ProductTranslation
ProductImage
Availability
Price
```

---

# 33. GELECEK MENÜ MODELİ

Daha sonra:

```text
ProductVariant
ModifierGroup
ModifierOption
Ingredient
Allergen
Nutrition
DietaryTag
ScheduledMenu
ChannelProduct
```

eklenebilecek şekilde tasarlanacaktır.

---

# 34. MENU VERSIONING

Menü:

```text
DRAFT
PUBLISHED
ARCHIVED
```

mantığına hazır olmalıdır.

İlk sürümde minimum implementasyon yapılabilir.

---

# 35. MULTILINGUAL

Kod altyapısı localization-ready olacaktır.

İlk release:

```text
TR
EN
```

ile başlayabilir.

İkinci dalgada:

```text
DE
RU
AR
FA
BG
```

eklenebilir.

Ancak veri modeli baştan çoklu dil destekleyecektir.

---

# 36. RTL

Arabic/Persian:

```text
RTL
```

olacağı için layout:

- direction-aware
- icon-aware
- text-aware

tasarlanacaktır.

RTL “sonradan ekleme” değildir.

---

# 37. PRODUCT TRANSLATION

```text
Product
ProductTranslation[]
```

kullanılacaktır.

Translation:

```text
LanguageCode
Name
Description
```

---

# 38. CUSTOMER WEB

Customer Web:

**mobile-first**

olacaktır.

Ama tablet ve desktop'ta da bozulmayacaktır.

---

# 39. CUSTOMER WEB DESIGN PRINCIPLES

Customer:

- QR sonrası hemen context görmeli
- restaurant/branch/table otomatik görünmeli
- kolay menü gezmeli
- büyük touch target kullanmalı
- hızlı sipariş vermeli
- hata durumunda ne olduğunu anlamalı

---

# 40. CUSTOMER MENÜ

Ana ekran:

```text
Logo
Restaurant
Branch • Table
Search
Categories
Products
Sticky Cart
```

---

# 41. PRODUCT CARD

Product card:

```text
Image
Name
Description
Price
Availability
Optional dietary information
Add button
```

olmalıdır.

---

# 42. CUSTOMER CART

Cart:

```text
Items
Modifiers
Quantity
Notes
Subtotal
```

göstermelidir.

---

# 43. ORDER TRACKING

Müşteri:

```text
Order received
Preparing
Ready
Served
```

durumlarını görebilir.

---

# 44. BASIC ETA

İlk sürümde:

```text
Estimated preparation:
15 min
```

veya:

```text
Estimated ready:
19:42
```

gösterilebilir.

İlk ETA basit rule-based olacaktır.

AI kullanılmayacaktır.

---

# 45. ETA MİMARİSİ

İlk sürümde:

```text
Restaurant default preparation time
```

veya basit product preparation time kullanılır.

Ancak veri modeli:

```text
EstimatedAt
EstimatedReadyAt
ActualReadyAt
```

gibi geleceğe hazır olmalıdır.

---

# 46. RESTAURANT WEB

İlk sürüm navigation:

```text
Dashboard
Orders
Tables
Menu
Settings
```

---

# 47. RESTAURANT DASHBOARD

İlk sürüm:

```text
Today's Orders
Today's Revenue
Open Tables
Pending Orders
Completed Orders
```

göstermelidir.

Gelişmiş intelligence daha sonra.

---

# 48. LIVE ORDERS

Ana operasyon ekranı:

```text
Order
Table
Time
Status
Total
```

göstermelidir.

---

# 49. ORDER DETAIL

Restaurant:

```text
Order number
Table
Items
Modifiers
Notes
Amount
Created time
Status history
```

görebilir.

---

# 50. TABLES

Restaurant:

```text
Table 1
Table 2
Table 3
...
```

görebilir.

Table status:

```text
AVAILABLE
OCCUPIED
HAS_PENDING_ORDER
```

gibi basit olabilir.

---

# 51. MENU MANAGEMENT

Restaurant:

```text
Create Category
Create Product
Edit Product
Price
Availability
Image
```

yapabilir.

---

# 52. QR MANAGEMENT

Restaurant:

```text
Generate QR
Activate QR
Deactivate QR
Revoke QR
Download
Print
```

yapabilir.

---

# 53. BASIC SECURITY

MVP'de:

```text
JWT
Refresh Token
RBAC
Tenant Isolation
Rate Limiting
QR Session Requirement
Audit Log
```

uygulanacaktır.

---

# 54. RATE LIMITING

Başlangıç parametreleri configurable:

```text
Guest:
2 order / minute

Device:
5 order / 5 min

IP:
20 order / 5 min
```

Bunlar değiştirilebilir olmalıdır.

---

# 55. RATE LIMITING HARD RULE DEĞİL

Restaurant Wi-Fi nedeniyle aynı public IP'den birçok müşteri gelebilir.

Dolayısıyla IP tek başına block sebebi olmamalıdır.

---

# 56. RISK ENGINE

MVP'de minimal risk sistemi:

```text
High order frequency
Many table attempts
Repeated failed requests
Repeated invalid QR/session attempts
```

kullanabilir.

Advanced risk scoring daha sonra eklenir.

---

# 57. AUDIT LOG

MVP:

```text
Login
Failed Login
Create QR
Revoke QR
Create Order
Modify Order
Cancel Order
Status Change
```

loglamalıdır.

---

# 58. PAYMENT BOUNDARY

MVP'de online payment olmayabilir.

Ancak architecture:

```text
IPaymentProvider
PaymentIntent
PaymentAttempt
PaymentStatus
Refund
```

gibi future-ready abstraction'a sahip olmalıdır.

Henüz implement edilmeyen feature için gereksiz UI yapılmayacaktır.

---

# 59. MARKETPLACE BOUNDARY

MVP'de external marketplace entegrasyonu yoktur.

Ancak Order modeli:

```text
OrderChannel
ExternalOrderId
```

gibi alanları desteklemelidir.

Integration Engine dokümante edilir.

Kodunun tamamı yazılmaz.

---

# 60. POS/FISCAL BOUNDARY

MVP'de POS/ÖKC yok.

Ancak gelecekte:

```text
Payment
Fiscal
POS
Edge
```

ayrımını koruyacak mimari kurulmalıdır.

---

# 61. EDGE BOUNDARY

MVP'de Edge Agent minimum skeleton olabilir.

Örneğin:

```text
Health
Registration
Secure Device Identity
```

gibi temel yapı.

Yazıcı/POS/ÖKC entegrasyonları daha sonraki release'tedir.

---

# 62. DATABASE PREPARATION

MVP'de sadece gerekli tablolar oluşturulacaktır.

Ancak database naming/foreign key/index strategy gelecekteki alanlarla çakışmayacak şekilde belirlenmelidir.

“Gelecekte lazım olur” diye yüzlerce boş tablo oluşturulmayacaktır.

---

# 63. MONEY

Tüm parasal alanlar:

```text
decimal
```

olmalıdır.

Float/double kullanılmayacaktır.

---

# 64. TIME

Database:

```text
UTC
```

Restaurant display:

```text
Restaurant Time Zone
```

ile gösterilir.

---

# 65. IDENTIFIERS

Internal identifiers tahmin edilebilir sequential external IDs olarak tasarlanmamalıdır.

QR/security token'ları ayrıca opaque/random olmalıdır.

---

# 66. API

Customer API ile Restaurant Admin API mantıksal olarak ayrılmalıdır.

Örneğin:

```text
/api/customer/...
/api/restaurant/...
/api/admin/...
```

veya domain bazlı başka temiz bir yapı kullanılabilir.

---

# 67. API DTO

EF Core entities doğrudan response olarak dönülmeyecektir.

Her public API:

```text
Request DTO
Response DTO
```

kullanacaktır.

---

# 68. API PAGINATION

Liste endpoint'leri büyük veri döndürmeyecektir.

Pagination zorunludur.

---

# 69. API VALIDATION

Validation:

- backend
- server side

zorunludur.

Frontend validation yalnızca UX içindir.

---

# 70. ERROR HANDLING

Standart Problem Details yaklaşımı kullanılabilir.

Technical stack traces production response'ta gösterilmeyecektir.

---

# 71. DATABASE TRANSACTION

Bir business operation birden fazla DB değiştiriyorsa transaction boundary açık şekilde belirlenmelidir.

---

# 72. OUTBOX

Henüz marketplace/payment olmasa bile architecture dokümantasyonu:

```text
Transactional Outbox
```

yaklaşımını kullanmalıdır.

İlk release'te yalnızca gerçekten gereken eventlerde uygulanabilir.

---

# 73. OFFLINE

İlk müşteri web'i:

**offline order göndermeyecektir.**

Müşteriye internet yoksa:

> İnternet bağlantısı gerekli.

gösterilir.

---

# 74. STAFF OFFLINE

Flutter staff application daha sonra:

```text
SQLite
Sync Queue
Idempotency
Retry
```

ile offline çalışacaktır.

---

# 75. DESIGN SOURCE OF TRUTH

Figma:

**design source of truth**

olacaktır.

Approved design files:

```text
docs/design/**/approved/
```

altında tutulacaktır.

Cursor bu dosyaları referans alacaktır.

---

# 76. UI TASARIMI

## Customer

Premium, sade, hızlı, hospitality-focused.

## Restaurant

Modern SaaS + operations.

## KDS

High contrast + high speed.

## POS

Touch + speed.

## Staff

One-hand + tablet friendly.

---

# 77. DESIGN SYSTEM

Central:

```text
Colors
Typography
Spacing
Radius
Elevation
Buttons
Inputs
Cards
Tables
Badges
Dialogs
Navigation
```

---

# 78. DESIGN RULE

Cursor:

- rastgele renk kullanmayacak
- rastgele spacing kullanmayacak
- yeni button oluşturmayacak
- mevcut component varsa onu kullanacak
- kullanıcıya görünen text'i hard-code etmeyecek

---

# 79. Figma REFERANS SİSTEMİ

Önerilen:

```text
docs/design/
├── DESIGN_SYSTEM.md
├── DESIGN_TOKENS.json
│
├── customer-web/
│   ├── CUSTOMER_WEB.md
│   └── approved/
│       ├── 01-qr-entry.png
│       ├── 02-menu.png
│       ├── 03-product.png
│       ├── 04-cart.png
│       └── 05-order-tracking.png
│
└── restaurant-web/
    ├── RESTAURANT_WEB.md
    └── approved/
        ├── 01-dashboard.png
        ├── 02-orders.png
        ├── 03-order-detail.png
        ├── 04-tables.png
        └── 05-menu.png
```

---

# 80. STORYBOOK

Web reusable component'ler için Storybook kullanılabilir.

Componentler:

```text
Button
Input
Select
Modal
Card
Table
Badge
OrderCard
TableCard
StatusBadge
MoneyDisplay
```

---

# 81. RESPONSIVE

Customer:

```text
mobile-first
```

Restaurant:

```text
tablet + desktop
```

Tasarımlar mobile/tablet/desktop test edilmelidir.

---

# 82. RTL

RTL:

- text
- icon
- navigation
- padding
- alignment

ile birlikte test edilmelidir.

---

# 83. ACCESSIBILITY

Minimum:

```text
Contrast
Keyboard navigation
Focus state
Touch target size
Screen reader label
Text scaling
Reduced motion
```

---

# 84. TEST STRATEGY

MVP:

```text
Unit
Integration
API
E2E
Security
```

gerektirir.

---

# 85. CRITICAL E2E

Tek temel akış:

```text
Restaurant Register
→ Create Branch
→ Create Table
→ Generate QR
→ Customer scans QR
→ Session created
→ Menu opens
→ Cart
→ Order
→ Restaurant receives
→ Restaurant changes status
→ Customer sees status
```

Bu akış kırılmamalıdır.

---

# 86. QR SECURITY TEST

Test:

```text
No QR
→ Menu denied

Invalid QR
→ Denied

Revoked QR
→ Denied

Valid QR
→ Session created

Expired session
→ Refresh/renew policy

Another table token
→ Cannot modify current table

Manual URL manipulation
→ Cannot switch table
```

---

# 87. TENANT SECURITY TEST

Restaurant A:

```text
cannot read Restaurant B
```

Her endpoint için test edilmelidir.

---

# 88. ORDER DUPLICATION TEST

Aynı idempotency request:

```text
2 times
```

gönderildiğinde:

```text
1 order
```

oluşmalıdır.

---

# 89. PERFORMANCE TEST

MVP'nin temel senaryosunda:

```text
Multiple customers
Multiple tables
Concurrent orders
```

test edilmelidir.

Gerçek limitler ölçülerek belirlenmelidir.

---

# 90. CURSOR ANA TALİMATI

Cursor'a aşağıdaki kurallar kesin olarak uygulanmalıdır:

```text
You are the principal engineer of Restaurant OS.

This project is intentionally developed in releases.

Do not implement future roadmap functionality unless explicitly requested.

Treat MASTER_CURSOR_DEVELOPMENT_SPECIFICATION.md as the primary authority.

Before major implementation:
1. Inspect repository.
2. Inspect architecture.
3. Inspect existing patterns.
4. Identify affected domains.
5. Identify database impact.
6. Identify API impact.
7. Identify UI impact.
8. Identify security impact.
9. Identify testing impact.
10. Present a concise implementation plan.

Do not rewrite working code unnecessarily.

Do not introduce microservices prematurely.

Do not bypass domain boundaries.

Do not access another tenant's data.

Do not expose EF entities directly.

Do not hard-code business rules.

Do not hard-code user-facing strings.

Do not hard-code colors, spacing or typography.

Reuse the design system.

Use the approved Figma references.

For external systems always use adapters.

For critical commands use idempotency.

For external side effects use transactional outbox where appropriate.

For offline operations use a sync queue.

For payments never store raw card data.

For fiscal operations use a dedicated abstraction.

For production schema changes use migrations.

Use UTC for persisted timestamps.

Use decimal for monetary values.

Write tests with every critical feature.

A feature is not complete until:
- build passes
- tests pass
- security is reviewed
- tenant isolation is reviewed
- relevant documentation is updated
- acceptance criteria pass

Do not silently change scope.

If requirements conflict with the specification:
identify the conflict first,
explain the impact,
propose a safe solution,
do not silently violate the architecture.
```

---

# 91. CURSOR FEATURE WORKFLOW

Her feature:

```text
Analyze
 ↓
Plan
 ↓
Implement
 ↓
Test
 ↓
Review
 ↓
Document
```

---

# 92. CURSOR ANALYZE MODU

Büyük özelliklerde önce kod yazılmayacaktır.

Çıktı:

```text
Affected modules
Affected files
Database changes
API changes
UI changes
Security risks
Test plan
```

---

# 93. CURSOR REVIEW MODU

Her büyük feature sonunda:

```text
Review for:
- Security
- Tenant isolation
- Race conditions
- Duplicate commands
- Performance
- API compatibility
- UI consistency
- Localization
- Accessibility
- Error handling
- Logging
```

---

# 94. CURSOR DATABASE RULES

Schema değişikliğinde:

```text
Migration
Index
Constraint
Existing data impact
Rollback/recovery
Tests
```

değerlendirilecektir.

---

# 95. CURSOR API RULES

Yeni endpoint:

```text
Authentication
Authorization
Tenant scope
Validation
Rate limit
Error handling
Logging
```

kontrollerinden geçmelidir.

---

# 96. CURSOR UI RULES

Her ekran:

```text
Loading
Empty
Error
Success
Offline
```

durumlarını düşünmelidir.

---

# 97. CURSOR MOBILE RULES

Future Staff Mobile:

```text
SQLite
Sync Queue
Idempotency
Retry
Conflict Strategy
Network awareness
```

destekleyecek şekilde hazırlanacaktır.

---

# 98. CURSOR INTEGRATION RULES

External API hiçbir zaman domain modeline sızmayacaktır.

Her adapter:

```text
External DTO
Mapping
Canonical model
Error handling
Retry
Idempotency
Health
Tests
```

içermelidir.

---

# 99. CURSOR PAYMENT RULES

Payment:

```text
Intent
Attempt
Status
Webhook
Reconciliation
Refund
```

olarak modellenmelidir.

---

# 100. CURSOR FISCAL RULES

ÖKC/fiş/yazarkasa kodu:

```text
Fiscal Adapter
```

katmanında kalmalıdır.

Domain'e hardware-specific kod sokulmaz.

---

# 101. CURSOR AI RULES

AI:

- veri uydurmaz
- eksik veriyi gerçek gibi göstermez
- kritik işlemi izinsiz yapmaz
- öneri ile eylemi ayırır
- confidence kullanabilir

---

# 102. CURSOR SCOPE RULE

MVP dışındaki feature listesinde bir problem görülürse:

> “Bunu şimdi geliştireyim.”

denilmeyecek.

Sadece:

```text
Future architecture note
```

olarak dokümante edilecek.

---

# 103. RELEASE 0 — FOUNDATION

## Task 001

Repository structure.

## Task 002

Backend skeleton.

## Task 003

Frontend skeleton.

## Task 004

Design system.

## Task 005

Database infrastructure.

## Task 006

CI/CD.

## Task 007

Logging/health check.

---

# 104. RELEASE 1 — CORE AUTH

## Task 008

Registration.

## Task 009

Login.

## Task 010

Refresh token.

## Task 011

Role/permission foundation.

---

# 105. RELEASE 1 — RESTAURANT

## Task 012

Organization.

## Task 013

Restaurant.

## Task 014

Branch.

## Task 015

Restaurant owner.

---

# 106. RELEASE 1 — TABLE / QR

## Task 016

Table CRUD.

## Task 017

QR creation.

## Task 018

QR activation.

## Task 019

QR revocation.

## Task 020

QR download/print.

---

# 107. RELEASE 1 — SESSION

## Task 021

QR resolver.

## Task 022

TableSession.

## Task 023

GuestSession.

## Task 024

Session security.

---

# 108. RELEASE 1 — MENU

## Task 025

Menu.

## Task 026

Category.

## Task 027

Product.

## Task 028

Product image.

## Task 029

Price.

## Task 030

Availability.

## Task 031

Localization.

---

# 109. RELEASE 1 — CUSTOMER WEB

## Task 032

QR entry.

## Task 033

Menu.

## Task 034

Product detail.

## Task 035

Cart.

## Task 036

Order submit.

## Task 037

Order tracking.

## Task 038

Basic ETA.

---

# 110. RELEASE 1 — RESTAURANT WEB

## Task 039

Dashboard.

## Task 040

Orders.

## Task 041

Order detail.

## Task 042

Status management.

## Task 043

Tables.

## Task 044

Menu management.

---

# 111. RELEASE 1 — SECURITY

## Task 045

Tenant isolation.

## Task 046

Rate limiting.

## Task 047

QR validation.

## Task 048

Audit log.

## Task 049

Security tests.

---

# 112. RELEASE 1 — TESTING

## Task 050

Unit tests.

## Task 051

Integration tests.

## Task 052

API tests.

## Task 053

Customer E2E.

## Task 054

Restaurant E2E.

## Task 055

Security E2E.

---

# 113. RELEASE 1 EXIT CRITERIA

Release cannot be considered complete until:

```text
Restaurant can register
Restaurant can create branch
Restaurant can create table
Restaurant can generate QR
Customer can scan QR
Restaurant/branch/table are automatically resolved
Customer can see menu
Customer can create cart
Customer can create order
Restaurant sees order
Restaurant changes order status
Customer sees updated status
Basic ETA works
Unauthorized menu access is denied
Tenant isolation tests pass
E2E tests pass
```

### Waiter Call vs Waiter Mobile (source: `docs/ilk M__C__S.md`)

Do **not** conflate these:

| Capability | MVP (`ilk M__C__S` §162) | Staff app (`ilk M__C__S` §163 Phase 2) |
|---|---|---|
| **Waiter Call** (`ServiceRequest`: WAITER, BILL, WATER, …) | Yes — customer taps “call staff”; restaurant/staff get an in-app alert | Consumed by mobile later |
| **Waiter Mobile App** (`Design_Waiter_Mobile_App`, Flutter `staff-mobile`) | No — not required to exit Release 1 | Yes — table map, take order, offline sync, notifications tab |

Product may *accelerate* Waiter Mobile into an earlier release **only after** Release 1 exit criteria above are green. Until then, treat `docs/design/Approved/Design_Waiter_Mobile_App` as approved UI for Phase 2 / Release 3, not as a blocker for first commercial QR→order loop.

### Notifications — intended vs current (implementation truth)

Intended channels (`ilk M__C__S` §125): In-App, Web Push, Mobile Push, Email, SMS — behind a notification abstraction.

**Current code (Release 1 thin realtime — in progress):**

```text
Order placed by customer
  → persisted
  → publishes orderStatusChanged to management SignalR group (same event as status changes)
  → restaurant-web mergeOrder adds unknown ids
  → RestaurantOS.Web Orders page connects to /hubs/v1/management-orders and upserts cards

Order status changed by restaurant
  → customer hub notified (customer tracking)
  → management hub notified

Waiter Call / ServiceRequest
  → customer POST /api/v1/customer/service-requests
  → management SignalR serviceRequestCreated + MVC Orders inbox
  → complete via POST /api/v1/management/service-requests/{id}/complete
```

Development auto-applies EF migrations on API startup.

---

# 114. RELEASE 2 — FIRST MONEY

Only after MVP works.

Add:

```text
Trial
Free
Pro
Feature entitlement
Basic usage limit
Basic subscription management
```

Payment automation may initially be manual.

---

# 115. RELEASE 2 — FIRST CUSTOMER PILOTS

Goal:

```text
1–3 real restaurants
```

then:

```text
5–10 real restaurants
```

Do not immediately chase hundreds.

---

# 116. FIRST PILOT METRICS

Measure:

```text
QR scans
Menu opens
Orders
Order conversion
Average order
Order failures
ETA error
Restaurant active days
```

---

# 117. RELEASE 3 — WAITER / KDS

After Release 1 is green (and preferably after first pilots / money packaging):

```text
Staff Mobile (Flutter) — apps/staff-mobile
UI baseline: docs/design/Approved/Design_Waiter_Mobile_App
Waiter role flows (tables map/list, take order, item status)
Fixed Tablet mode
KDS
Waiter Call / ServiceRequest inbox (if not already shipped in Release 1 ops)
Offline: SQLite + sync queue + idempotency (see §97)
```

Waiter Call alone may ship earlier as a thin API + management/in-app alert without the full mobile app.

---

# 118. RELEASE 4 — BILL / PAYMENT

```text
Digital Bill
Mobile Payment
Split Bill
Tips
Digital Receipt
```

---

# 119. RELEASE 5 — POS / EDGE / FISCAL

```text
Edge Agent
Printer
POS
Cash Session
Fiscal Adapter
ÖKC
```

---

# 120. RELEASE 6 — MARKETPLACE

```text
Integration Engine
Canonical Order
Yemeksepeti
Trendyol
Migros
Other adapters
```

---

# 121. RELEASE 7 — INVENTORY

```text
Ingredients
Inventory
Recipe
Cost
Purchasing
Supplier
Waste
```

---

# 122. RELEASE 8 — BUSINESS INTELLIGENCE

```text
Analytics
Restaurant Health
Benchmark
Channel Profitability
Kitchen Analytics
ETA Accuracy
```

---

# 123. RELEASE 9 — AI

```text
AI Translation
AI Recommendation
AI ETA
AI Forecast
AI Stock
AI Restaurant Advisor
```

---

# 124. RELEASE 10 — ENTERPRISE

```text
Advanced Multi Branch
Franchise
White Label
API Platform
Reservation
Waitlist
Hotel
Resort
Integration Marketplace
```

---

# 125. MASTER PRODUCT RULE

No feature is considered valuable merely because a competitor has it.

Every feature must answer at least one:

```text
Does it increase revenue?
Does it reduce cost?
Does it reduce labor?
Does it reduce errors?
Does it improve customer experience?
Does it increase retention?
Does it improve decision making?
```

---

# 126. MASTER QUALITY RULE

The first release must NOT be a low-quality prototype.

It must have:

```text
Professional UX
Professional UI
Correct architecture
Secure authentication
Correct tenant isolation
Reliable order flow
Good mobile experience
Localization support
Responsive design
Error handling
Logging
Tests
```

It is small in scope, not poor in quality.

---

# 127. MASTER PRODUCT DIFFERENTIATION

Long-term unique strengths:

```text
Smart QR Security
Table / Guest Session
One Order Engine
Hybrid Ordering
ETA Intelligence
Cloud + Edge
Offline Staff
Channel-neutral Integrations
Restaurant Health
Anonymous Benchmark
AI Advisor
```

These must remain part of the product vision.

---

# 128. MASTER UX PRINCIPLE

Customer should think:

> QR'ı okuttum, gerisini sistem yaptı.

Waiter:

> Masayı seçtim, siparişi girdim.

Kitchen:

> Sıradaki işi görüyorum.

Cashier:

> Hesap net.

Owner:

> Restoranın durumunu görüyorum.

The complexity belongs inside the system, not inside the user's mental model.

---

# 129. FINAL DEFINITION OF DONE

A release is complete only when:

```text
[ ] Code complete
[ ] Tests complete
[ ] Security reviewed
[ ] Tenant isolation reviewed
[ ] UI reviewed
[ ] Mobile reviewed if applicable
[ ] Localization reviewed
[ ] RTL reviewed
[ ] Accessibility reviewed
[ ] Performance reviewed
[ ] Logs/metrics reviewed
[ ] Documentation updated
[ ] Migration validated
[ ] Build passes
[ ] Test suite passes
[ ] Acceptance criteria pass
```

---

# 130. FINAL PRINCIPLE

Restaurant OS must be:

```text
SMALL TO START
STRONG UNDERNEATH
EASY TO USE
SECURE
SCALABLE
EXTENSIBLE
COMMERCIAL
```

The first product earns the right to become the larger platform.

Do not build the entire restaurant ERP before proving that restaurants want the first product.

But do not build the first product in a way that prevents it from becoming the Restaurant Operating System.