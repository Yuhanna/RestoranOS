# RESTAURANT OS

## Master Cursor Specification

### Product Vision + Architecture + UX + Security + Engineering Constitution

**Document Status:** Master Reference
**Purpose:** This document is the primary product, architecture, UX and engineering authority for the Restaurant OS project.

---

# 1. EXECUTIVE SUMMARY

Restaurant OS is a cloud/web-first, multi-tenant restaurant operating platform.

The product begins with QR-based dine-in ordering but is intentionally designed to evolve into a complete Restaurant Operating System.

The long-term platform covers:

* Customer QR ordering
* Mobile web menu
* Restaurant management
* Table management
* Guest sessions
* Order management
* Waiter application
* Fixed-table tablets
* Kitchen Display System
* POS
* Fiscal/ÖKC integrations
* Payment
* Digital bill
* Split bill
* Marketplace integrations
* Website ordering
* Pickup
* Delivery
* Inventory
* Recipes
* Cost management
* Purchasing
* Suppliers
* Staff
* Multi-branch management
* CRM
* Loyalty
* Analytics
* Restaurant Health
* Anonymous benchmarking
* ETA intelligence
* AI restaurant advisor
* White-label
* Enterprise
* Future hotel/resort/event use cases

The system must remain manageable by starting with a modular monolith and gradually expanding.

The project must be designed so that adding future modules does not require rewriting the core platform.

---

# 2. CORE PRODUCT PRINCIPLE

The system is NOT:

> "A QR menu application."

The system is:

> "A Restaurant Operating System with QR ordering as its primary customer entry point."

The initial customer journey:

```text
Table
  ↓
QR
  ↓
Restaurant / Branch / Table automatically detected
  ↓
Guest Session
  ↓
Localized Menu
  ↓
Cart
  ↓
Order
  ↓
Kitchen
  ↓
ETA
  ↓
Order Tracking
  ↓
Bill
  ↓
Payment
  ↓
Receipt
  ↓
Feedback / Loyalty
```

The restaurant journey:

```text
Customer
  ↓
Order Engine
  ↓
Kitchen / KDS
  ↓
POS / Fiscal
  ↓
Payment
  ↓
Inventory
  ↓
Analytics
  ↓
Benchmark
  ↓
AI Advisor
```

---

# 3. MASTER ENGINEERING PRINCIPLE

## Feature-ready, not feature-loaded.

The platform must:

* anticipate future capabilities
* avoid premature implementation
* avoid unnecessary complexity
* preserve clean domain boundaries
* preserve backward compatibility
* allow future modules to be added without architectural rewrites

Do not implement every future feature in the MVP.

Do not design the MVP in a way that blocks future features.

---

# 4. DEVELOPMENT PHILOSOPHY

The project must be developed incrementally.

Recommended order:

```text
Phase 0
Architecture + Design System

Phase 1
Backend Foundation

Phase 2
Customer Web

Phase 3
Restaurant Web

Phase 4
Staff Mobile

Phase 5
KDS

Phase 6
Edge Agent

Phase 7
Payments / POS / Fiscal

Phase 8
Marketplace Integrations

Phase 9
Inventory / Purchasing / Cost

Phase 10
CRM / Loyalty / Analytics

Phase 11
Benchmark / Restaurant Health

Phase 12
AI
```

Do not skip architecture because a feature seems simple.

Do not build large unrelated areas simultaneously.

---

# 5. CURSOR ROLE

Cursor is the senior implementation assistant.

Cursor is NOT authorized to redesign the platform arbitrarily.

Cursor must:

1. Understand the existing architecture.
2. Follow this specification.
3. Reuse existing abstractions.
4. Preserve established patterns.
5. Identify conflicts before changing architecture.
6. Implement incrementally.
7. Write tests.
8. Validate security.
9. Validate tenant isolation.
10. Validate backward compatibility.
11. Update documentation where necessary.

---

# 6. PROJECT REPOSITORY

Use a monorepo.

Recommended structure:

```text
restaurant-os/
│
├── apps/
│   ├── api/
│   ├── customer-web/
│   ├── restaurant-web/
│   ├── platform-web/
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
│   ├── MASTER_CURSOR_SPECIFICATION.md
│   ├── PRODUCT_SPECIFICATION.md
│   ├── ARCHITECTURE.md
│   ├── DOMAIN_MODEL.md
│   ├── DATABASE_DESIGN.md
│   ├── API_CONTRACTS.md
│   ├── SECURITY.md
│   ├── UX_PRINCIPLES.md
│   ├── DESIGN_SYSTEM.md
│   ├── CUSTOMER_UI.md
│   ├── RESTAURANT_UI.md
│   ├── MOBILE_UI.md
│   ├── KDS_UI.md
│   ├── POS_UI.md
│   ├── OFFLINE_SYNC.md
│   ├── INTEGRATIONS.md
│   ├── PAYMENTS.md
│   ├── FISCAL.md
│   ├── MVP.md
│   ├── ROADMAP.md
│   └── DECISIONS/
│
├── infra/
│   ├── docker/
│   ├── ci/
│   └── deployment/
│
└── README.md
```

---

# 7. APPLICATIONS

## 7.1 API

ASP.NET Core / C#.

Responsible for:

* authentication
* authorization
* domain logic
* APIs
* persistence
* realtime events
* integrations
* background processing

---

## 7.2 Customer Web

Mobile-first responsive web.

Used by customers through QR.

Must not require app installation.

---

## 7.3 Restaurant Web

Used by:

* owner
* manager
* cashier
* kitchen manager
* inventory manager
* staff administrators

---

## 7.4 Platform Web

Used by platform administrators.

---

## 7.5 Staff Mobile

Flutter.

Used by:

* waiter
* restaurant staff
* manager
* fixed-table tablet
* future kiosk scenarios

---

## 7.6 Edge Agent

.NET Worker / Windows Service.

Responsible for:

* local printer communication
* local device communication
* fiscal devices
* POS hardware
* local KDS support
* local queueing
* cloud synchronization
* health monitoring

---

# 8. TECHNOLOGY BASELINE

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

PostgreSQL is NOT required merely because it is popular.

Existing MSSQL expertise is a valid reason to use MSSQL.

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

## Mobile

```text
Flutter
Dart
SQLite
```

---

## Web

```text
React
TypeScript
```

---

## Background Processing

```text
.NET Worker
```

---

## Messaging

Start with:

```text
Transactional Outbox
+
Background Worker
```

Use RabbitMQ when justified by scale or integration requirements.

Do not introduce RabbitMQ merely because it exists.

---

## Infrastructure

```text
Docker
CI/CD
Cloud deployment
```

---

# 9. ARCHITECTURE

Initial architecture:

**Modular Monolith**

Do not start with microservices.

Domain boundaries must nevertheless be strong enough that a module can eventually be extracted if required.

---

# 10. CORE DOMAINS

The backend should contain explicit domains/modules:

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
Audit
Security
```

---

# 11. DOMAIN BOUNDARY RULE

A module must not directly modify another module's internal state.

Bad:

```text
OrderModule
→ InventoryDbContext
→ modify inventory directly
```

Prefer:

```text
OrderModule
→ domain/application contract
→ InventoryModule
```

or:

```text
Order completed
→ domain event
→ inventory consumption
```

---

# 12. MULTI-TENANCY

The system must be multi-tenant from the beginning.

Recommended model:

```text
Organization
    ↓
Restaurant
    ↓
Branch
```

A restaurant may have multiple branches.

A group/company may have multiple restaurants.

---

# 13. TENANT ISOLATION

Tenant isolation is a security requirement.

Every query and mutation must respect:

```text
OrganizationId
RestaurantId
BranchId
```

where applicable.

Never trust a client-provided tenant ID.

Tenant scope must be resolved from authenticated context or controlled server-side context.

---

# 14. ROLES

Minimum roles:

```text
PlatformAdmin
OrganizationOwner
RestaurantOwner
RestaurantManager
Cashier
Waiter
KitchenStaff
InventoryManager
Viewer
Customer
```

The role system must support granular permissions.

---

# 15. PERMISSIONS

Do not rely solely on role names.

Use permission policies such as:

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
Order.Refund

Payment.View
Payment.Create
Payment.Refund

Inventory.View
Inventory.Adjust

Staff.View
Staff.Manage
```

---

# 16. QR AS DIGITAL TABLE GATEWAY

A QR code is not merely a menu URL.

It is:

> Digital Table Gateway.

Flow:

```text
Scan QR
 ↓
QR Resolver
 ↓
QR Validation
 ↓
Restaurant
 ↓
Branch
 ↓
Table
 ↓
Current Table Session
 ↓
Guest Session
 ↓
Customer Web
```

---

# 17. QR CUSTOMER EXPERIENCE

The customer must NEVER be asked to manually choose:

* restaurant
* branch
* table

when the QR is valid.

These values must be automatically resolved.

The customer should see:

```text
[Restaurant Logo]

Restaurant Name
Branch Name
Table 12
```

---

# 18. QR SECURITY

QR is not considered authentication.

Security must use:

```text
QR
+
Table
+
TableSession
+
GuestSession
+
Device/session identity
+
IP reputation
+
Rate limit
+
Behavior
+
Risk score
```

---

# 19. MULTIPLE QR CODES PER TABLE

Support multiple QR records per table.

Example:

```text
QR-001 ACTIVE
QR-002 INACTIVE
QR-003 REVOKED
```

Use cases:

* replacement
* damaged QR
* compromised QR
* reprinting
* migration

Periodic automatic QR rotation is NOT the primary security mechanism.

---

# 20. TABLE SESSION

A table visit is represented by a TableSession.

Example:

```text
Table 12
Session 10052
```

can contain:

```text
Guest A
Guest B
Guest C
```

and:

```text
Order 1
Order 2
Order 3
```

---

# 21. GUEST SESSION

A GuestSession represents a customer's current interaction with a TableSession.

Possible fields:

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

Do not store unnecessary personal data.

---

# 22. GROUP ORDERING

Multiple guests at one table can order independently.

The system must preserve:

* guest identity within the session
* item ownership if available
* shared table
* shared check
* optional split check

---

# 23. CONTINUOUS TAB

The TableSession remains active until explicitly closed.

Additional orders can be added.

Example:

```text
Order 1
Burger

Order 2
Cola

Order 3
Dessert
```

All belong to the same TableSession.

---

# 24. SERVER-STARTED ORDER

A waiter can create a TableSession before the customer scans the QR.

Customer scanning later joins the existing table session.

This enables hybrid service.

---

# 25. ORDER ENGINE

All order sources use ONE order engine:

```text
QR
Waiter
Tablet
Kiosk
POS
Website
Marketplace
API
```

Never create separate order business logic for each channel.

---

# 26. ORDER CHANNEL

Example:

```text
DINE_IN_QR
WAITER
FIXED_TABLET
KIOSK
WEBSITE
POS
YEMEKSEPETI
TRENDYOL
MIGROS
PHONE
```

---

# 27. FULFILLMENT

Support:

```text
DINE_IN
TAKEAWAY
PICKUP
DELIVERY
PREORDER
```

---

# 28. ORDER STATE MACHINE

Base:

```text
DRAFT
SUBMITTED
RECEIVED
CONFIRMED
PREPARING
READY
SERVED
COMPLETED
```

Alternatives:

```text
CANCELLED
REJECTED
```

State transitions must be validated server-side.

---

# 29. ORDER ITEM STATE

Order items may have independent kitchen states.

Example:

```text
Burger → READY
Pizza → PREPARING
Cola → SERVED
```

This is necessary for kitchen workflows.

---

# 30. PAYMENT STATE

Never mix OrderState with PaymentState.

Payment:

```text
UNPAID
PARTIALLY_PAID
AUTHORIZED
PAID
FAILED
REFUNDED
PARTIALLY_REFUNDED
```

---

# 31. FISCAL STATE

Separate:

```text
NOT_FISCALIZED
FISCALIZED
VOIDED
```

---

# 32. PRICE SNAPSHOTS

Order history must never change when a product changes.

OrderItem must preserve:

```text
ProductNameSnapshot
UnitPriceSnapshot
TaxSnapshot
DiscountSnapshot
ModifierSnapshot
```

---

# 33. MENU

Core entities:

```text
Menu
MenuVersion
MenuCategory
Product
ProductTranslation
ProductImage
ProductVariant
ModifierGroup
ModifierOption
Ingredient
Allergen
Nutrition
DietaryTag
Availability
```

---

# 34. PRODUCT MASTER

A product is maintained centrally.

It can be exposed through:

```text
QR
Dine-in
Website
POS
Yemeksepeti
Trendyol
Migros
Kiosk
```

---

# 35. CHANNEL PRODUCT

External marketplace products are separate mappings.

Example:

```text
Internal Product:
BURGER-001

Yemeksepeti:
99812

Trendyol:
55371
```

Use:

```text
ChannelProductMapping
```

---

# 36. EXTERNAL API ISOLATION

Never use external platform DTOs as internal domain entities.

Always:

```text
External DTO
 ↓
Adapter
 ↓
Canonical Model
 ↓
Domain Model
```

---

# 37. MARKETPLACE ADAPTERS

Use interfaces such as:

```text
IOrderChannelAdapter
ICatalogChannelAdapter
IAvailabilityChannelAdapter
```

Each provider must be isolated.

---

# 38. MARKETPLACE FEATURES

Potentially support:

```text
Catalog sync
Product sync
Price sync
Availability sync
Order receive
Order accept
Order reject
Order cancel
Status update
Store open/close
Operating hours
Promotion
Webhook
Health check
```

---

# 39. WEBHOOK IDEMPOTENCY

Every webhook event must be uniquely tracked.

Store:

```text
ExternalEventId
Channel
PayloadHash
ReceivedAt
ProcessedAt
Status
```

Same event must never create duplicate orders.

---

# 40. RETRY

External integrations must support:

```text
Retry
Exponential Backoff
Dead Letter
Manual Retry
```

---

# 41. TRANSACTIONAL OUTBOX

Important state changes should use:

```text
DB transaction
 ├── Domain data
 └── Outbox event
```

Then:

```text
Worker
 ↓
Publish / Integrate / Notify
```

---

# 42. KITCHEN

KDS must support stations.

Examples:

```text
Grill
Pizza
Beverage
Dessert
Cold
Hot Kitchen
```

Order items can be routed to stations.

---

# 43. KDS DISPLAY

KDS must prioritize:

* speed
* readability
* touch
* high contrast
* elapsed time
* ETA
* priority
* station awareness

Avoid decorative UI.

---

# 44. ETA ENGINE

ETA is a core feature.

Initial deterministic model:

```text
PreparationTime
+
CurrentQueue
+
KitchenStation
+
Operational rules
```

Output:

```text
Estimated Ready Time
Estimated Duration Range
```

Example:

```text
Ready around 19:42
Approximately 12–15 minutes
```

---

# 45. ETA DATA COLLECTION

Store:

```text
EstimatedAt
EstimatedReadyAt
ActualReadyAt
ErrorMinutes
```

This data is required for future intelligent ETA.

---

# 46. ETA EVOLUTION

Version 1:

```text
Rule based
```

Version 2:

```text
Historical averages
```

Version 3:

```text
ML/AI
```

Never introduce AI before sufficient historical data exists.

---

# 47. CUSTOMER BILL

Customer can view:

```text
Products
Modifiers
Discount
Tax
Service Charge
Tip
Total
```

---

# 48. DIGITAL BILL MODES

Restaurant can configure:

```text
VIEW_ONLY
VIEW_AND_REQUEST_WAITER
VIEW_AND_PAY
HYBRID
```

---

# 49. MOBILE PAYMENT

Payment flow:

```text
Get current bill
 ↓
Create payment snapshot
 ↓
Payment Intent
 ↓
Provider
 ↓
Confirmation
 ↓
Receipt
```

Never charge based on stale client-side totals.

---

# 50. SPLIT BILL

Support future:

```text
Equal split
By item
Pay own items
Custom amount
One person pays all
```

---

# 51. DIGITAL RECEIPT

Support:

```text
View
PDF
Email
SMS
```

where legally and technically appropriate.

---

# 52. TIPS

Restaurant-configurable:

```text
0%
5%
10%
15%
20%
Custom
```

Do not hard-code tipping assumptions.

---

# 53. WAITER CALL

Model:

```text
ServiceRequest
```

Types:

```text
WAITER
BILL
WATER
CUTLERY
NAPKIN
OTHER
```

Track:

```text
CreatedAt
AssignedAt
CompletedAt
Duration
```

---

# 54. CUSTOMER MENU

Customer UX:

```text
Restaurant
Branch
Table
Search
Categories
Product
Modifiers
Notes
Allergens
Nutrition
Recommendations
Cart
Order
Tracking
Bill
```

---

# 55. MENU LOCALIZATION

Initial languages:

```text
TR
EN
DE
RU
FA
AR
BG
```

Architecture must support additional languages.

---

# 56. RTL

Arabic and Persian must be considered from day one.

Do not retrofit RTL later.

---

# 57. LOCALIZATION RULE

No user-facing text may be hard-coded inside source code.

Use localization keys.

Example:

```text
order.received
order.preparing
order.ready
order.cancelled
```

---

# 58. AI TRANSLATION

AI may generate translations.

Process:

```text
Draft
 ↓
AI Translation
 ↓
Human Review
 ↓
Publish
```

Never auto-publish critical customer-facing translations without review if accuracy is uncertain.

---

# 59. ALLERGENS

Support structured allergens.

Never depend solely on free-text descriptions.

---

# 60. NUTRITION

Support:

```text
Calories
Protein
Carbohydrate
Fat
Sugar
Salt
```

AI estimates must not be presented as verified nutrition data.

---

# 61. DIETARY TAGS

Examples:

```text
Vegetarian
Vegan
GlutenFree
LactoseFree
SugarFree
Spicy
HighProtein
```

Restaurant is responsible for content accuracy.

---

# 62. MENU SCHEDULING

Support:

```text
Breakfast
Lunch
Dinner
Late Night
Happy Hour
Weekend
Holiday
Seasonal
```

The QR code does not change.

The digital experience changes according to schedule.

---

# 63. MENU VERSIONING

Use:

```text
DRAFT
PUBLISHED
ARCHIVED
```

Never directly overwrite a live menu in a way that breaks active sessions.

---

# 64. RESTAURANT BRANDING

Restaurant can configure:

```text
Logo
Primary Color
Secondary Color
Accent
Fonts within approved system
Images
Banner
```

But accessibility rules must prevent unreadable combinations.

---

# 65. CUSTOMER DESIGN PRINCIPLES

Customer Web must be:

* mobile-first
* fast
* simple
* large touch targets
* visually attractive
* minimal scrolling where possible
* easy to understand
* multilingual
* RTL-aware
* accessible

---

# 66. RESTAURANT DESIGN PRINCIPLES

Restaurant Web must prioritize:

* density
* speed
* keyboard use
* tablet use
* desktop workflows
* clear state
* predictable navigation
* quick actions
* live operational information

---

# 67. KDS DESIGN PRINCIPLES

KDS is not a general-purpose dashboard.

It must show:

```text
Order
Table/Channel
Time
Priority
Items
Modifiers
ETA
State
```

---

# 68. POS DESIGN PRINCIPLES

POS must prioritize:

* minimal clicks
* touch-friendly controls
* keyboard shortcuts where useful
* fast item lookup
* clear total
* payment workflow
* cash session
* refund workflow

---

# 69. SHARED DESIGN SYSTEM

Create:

```text
packages/design-system
```

Web components must be reusable.

Use design tokens.

---

# 70. DESIGN TOKENS

Centralize:

```text
Color
Typography
Spacing
Radius
Elevation
Motion
Breakpoints
```

Never hard-code random values.

---

# 71. STORYBOOK

Reusable web components should have Storybook stories.

Examples:

```text
Button
Card
Modal
DataTable
Badge
OrderCard
TableCard
StatusBadge
MoneyDisplay
```

---

# 72. MOBILE DESIGN SYSTEM

Flutter has its own implementation but must follow the shared design tokens.

Do not make mobile look like a completely unrelated product.

---

# 73. RESPONSIVE UX

Customer:

```text
mobile-first
```

Restaurant:

```text
tablet + desktop
```

KDS:

```text
large tablet / large screen
```

POS:

```text
desktop / touch terminal
```

---

# 74. OFFLINE

Staff app should be offline-capable.

Customer payment must remain online-required.

---

# 75. MOBILE LOCAL DATABASE

Use SQLite for:

```text
Cached menu
Tables
Draft orders
Pending commands
Sync queue
Device configuration
```

---

# 76. SYNC QUEUE

Fields:

```text
Id
OperationType
EntityType
EntityId
Payload
CreatedAt
RetryCount
LastAttemptAt
Status
IdempotencyKey
LastError
```

---

# 77. IDEMPOTENCY

Mandatory for:

```text
Order create
Payment
Refund
Fiscal command
Marketplace event
Sync command
```

---

# 78. CONFLICT RESOLUTION

Never silently overwrite financial/order data.

Define per-domain strategy:

```text
ServerWins
ClientWins
Merge
ManualReview
```

---

# 79. EDGE AGENT

The Edge Agent bridges cloud and local restaurant hardware.

Responsibilities:

```text
Printer
POS
Fiscal device
Local KDS
Device discovery
Local queue
Health
Synchronization
```

---

# 80. EDGE SECURITY

Use secure device identity.

No hard-coded shared secret.

Use secure credential storage.

Commands must be authorized.

---

# 81. POS ARCHITECTURE

POS software is separate from payment device/fiscal device.

POS manages:

```text
Orders
Checks
Payments
Cash
Discounts
Refunds
Receipts
Cash sessions
```

Device integrations occur through adapters.

---

# 82. FISCAL ARCHITECTURE

Use:

```text
IFiscalDeviceAdapter
```

Device-specific implementations remain isolated.

Never put hardware-specific code in Order domain.

Fiscal transaction history must be immutable.

---

# 83. PAYMENT ARCHITECTURE

Use:

```text
IPaymentProvider
```

Separate:

```text
PaymentIntent
PaymentAttempt
PaymentStatus
Refund
Chargeback
```

Never store raw card data.

---

# 84. INVENTORY

Future core:

```text
Ingredient
StockItem
Warehouse
StockTransaction
Recipe
```

---

# 85. STOCK TRANSACTION TYPES

```text
PURCHASE
IN
OUT
TRANSFER
CONSUMPTION
WASTE
ADJUSTMENT
RETURN
```

Inventory changes must be traceable.

---

# 86. RECIPE

Product consumption must support recipe-based inventory deduction.

Example:

```text
Burger
180g meat
1 bread
20g cheese
30g tomato
```

---

# 87. COST

Support:

```text
Ingredient Cost
Product Cost
Gross Margin
Channel Cost
Commission
Waste
```

---

# 88. PURCHASING

Future entities:

```text
Supplier
PurchaseOrder
PurchaseOrderItem
GoodsReceipt
SupplierInvoice
```

Do not implement full accounting unless explicitly scheduled.

---

# 89. STAFF

Future:

```text
Employee
Shift
Role
Attendance
Performance
```

Payroll remains a later module.

---

# 90. CRM

Customer identity must be optional.

Guest ordering must not require registration.

Only store customer information when necessary and legally appropriate.

---

# 91. CUSTOMER DATA OWNERSHIP

The restaurant owns the customer relationship.

Platform data use must be clearly defined.

Customer data must not be exposed to other restaurants.

---

# 92. LOYALTY

Future:

```text
Points
Visits
Rewards
Coupons
Perks
Campaigns
```

---

# 93. ANALYTICS

Core metrics:

```text
Revenue
Orders
Average Order Value
Orders per Table
Table Turnover
Preparation Time
ETA Accuracy
Cancellation
Refund
Waste
Stock
Channel Revenue
Channel Contribution
```

---

# 94. RESTAURANT HEALTH

Health dimensions:

```text
Sales
Kitchen
Service
Inventory
Customer
Profitability
Operations
```

---

# 95. BENCHMARK

Compare restaurants only through privacy-preserving aggregated metrics.

Never expose another restaurant's raw commercial data.

Use minimum group-size thresholds.

---

# 96. AI

AI is an intelligence layer, not the foundation of the system.

AI must use verified business data.

---

# 97. AI FEATURES

Future:

```text
AI Translation
AI Menu Assistant
AI Recommendations
AI ETA
AI Demand Forecast
AI Stock Forecast
AI Waste Analysis
AI Benchmark Analysis
AI Restaurant Advisor
```

---

# 98. ACTIONABLE AI

AI should produce:

```text
Observation
Evidence
Possible Cause
Recommendation
Expected Impact
Confidence
```

not generic motivational text.

---

# 99. AI SAFETY

AI must:

* not invent numbers
* distinguish facts from estimates
* show uncertainty
* not silently change business data
* not automatically execute financial/fiscal actions
* require human approval for critical changes

---

# 100. SUBSCRIPTION

Initial plans:

```text
FREE
PRO
BUSINESS
ENTERPRISE
```

---

# 101. ENTITLEMENT ENGINE

Do not write:

```csharp
if(plan == Premium)
```

Use:

```text
Feature
Entitlement
UsageLimit
```

Examples:

```text
KDS_ENABLED
MOBILE_PAYMENT
MULTI_BRANCH
INVENTORY
AI_ADVISOR
BENCHMARK
```

---

# 102. FEATURE FLAGS

Support feature activation per:

```text
Platform
Organization
Restaurant
Branch
User
```

This is critical for gradual rollout.

---

# 103. OBSERVABILITY

Required:

```text
Structured Logs
Metrics
Health Checks
Correlation IDs
Trace IDs
Error Tracking
```

---

# 104. SECURITY LOGGING

Do not log:

* passwords
* access tokens
* raw card numbers
* secrets
* unnecessary personal data

---

# 105. AUDIT LOG

Audit:

```text
Login
RoleChange
PriceChange
OrderCancel
OrderModify
Refund
Payment
QRRevoke
InventoryAdjustment
FiscalOperation
SubscriptionChange
SecurityBlock
```

Audit records should be append-only where appropriate.

---

# 106. SECURITY BASELINE

Required:

```text
HTTPS
JWT
Refresh Tokens
RBAC
Policy Authorization
Tenant Isolation
Rate Limiting
Validation
Secure Headers
CORS
Secrets Management
Audit Logging
```

---

# 107. RATE LIMITING

Initial configurable values may be:

```text
Guest:
2 orders / minute

Device:
5 orders / 5 minutes

Device:
15 orders / 30 minutes

IP:
20 orders / 5 minutes
```

These are starting parameters, not immutable business truths.

---

# 108. RISK ENGINE

Risk signals can include:

```text
High order frequency
Many tables
Repeated cancellations
Unusual amount
Repeated QR reuse
Blocked-device history
Suspicious IP reputation
```

---

# 109. RISK ACTIONS

Example:

```text
LOW
→ allow

MEDIUM
→ additional validation

HIGH
→ staff/PIN approval

CRITICAL
→ block
```

Do not hard-code risk thresholds.

---

# 110. WEB DEVICE IDENTIFICATION

Do not attempt to collect the user's physical phone serial number through normal web browser APIs.

Use:

```text
Session
Device Identifier
IP Hash
Behavior
Risk
```

and appropriate platform security APIs on native staff applications.

---

# 111. API PRINCIPLES

All APIs must:

* authenticate
* authorize
* validate
* respect tenant boundaries
* respect rate limits
* return consistent errors
* use DTOs
* support pagination for large collections
* avoid unnecessary data
* be documented

---

# 112. API VERSIONING

Use versioning strategy such as:

```text
/v1
```

Breaking changes require deliberate versioning.

---

# 113. PROBLEM DETAILS

API errors should use a consistent problem/error representation.

Never expose internal stack traces in production API responses.

---

# 114. MONEY

Use:

```text
decimal
```

not float/double.

Currency must be explicit.

---

# 115. TIME

Persist timestamps in UTC.

Render according to:

```text
Restaurant Time Zone
```

---

# 116. DATABASE

Use:

* foreign keys
* unique constraints
* indexes
* concurrency controls
* audit requirements
* migrations

Do not create indexes blindly.

Index based on real query patterns.

---

# 117. ENTITY DELETION

Do not hard-delete historical financial/order records.

Use archive/soft-delete where appropriate.

---

# 118. DATABASE MIGRATION

Every schema change requires:

```text
Migration
Data impact review
Index review
Constraint review
Compatibility review
Test
```

---

# 119. EXPAND / MIGRATE / CONTRACT

For production-sensitive schema changes prefer:

```text
Expand
 ↓
Migrate
 ↓
Validate
 ↓
Contract
```

rather than destructive immediate changes.

---

# 120. PERFORMANCE

Avoid:

* N+1 queries
* unnecessary tracking
* unbounded queries
* loading full tables
* duplicate API requests
* repeated network calls
* synchronous blocking operations

---

# 121. CACHING

Suitable for:

```text
Menu
Restaurant configuration
Feature entitlements
Reference data
Availability
```

Do not treat cache as source of truth for:

```text
Payments
Fiscal data
Financial records
Orders
```

---

# 122. CONCURRENCY

Consider concurrent:

* waiter order
* customer order
* stock update
* payment
* order status update
* marketplace webhook

Use optimistic concurrency/appropriate transactional design.

---

# 123. PAYMENT CONCURRENCY

Avoid:

```text
double payment
double refund
```

Use:

```text
PaymentIntent
Idempotency
State machine
Webhook reconciliation
```

---

# 124. MARKETPLACE CONCURRENCY

External platform can send:

```text
accepted
cancelled
updated
```

out of order.

Adapter must validate state transitions.

---

# 125. NOTIFICATIONS

Use notification abstraction.

Possible channels:

```text
In-App
Web Push
Mobile Push
Email
SMS
```

---

# 126. UI PRINCIPLES

Never create arbitrary visual patterns.

Before implementing a component:

1. Search existing design system.
2. Reuse if possible.
3. Extend if reasonable.
4. Create a new component only when necessary.

---

# 127. DESIGN SYSTEM

Centralize:

```text
Colors
Typography
Spacing
Radii
Icons
Buttons
Inputs
Tables
Cards
Dialogs
Badges
Status
Navigation
```

---

# 128. CUSTOMER UX

Goal:

> Minimum cognitive load.

Customer must not have to understand the underlying system.

---

# 129. CUSTOMER HOME

Recommended:

```text
Restaurant Logo
Restaurant
Branch • Table
Search
Categories
Products
Cart
```

---

# 130. CUSTOMER BILL

Recommended:

```text
Items
Subtotal
Discount
Tax
Service
Tip
Total
```

Actions:

```text
View
Split
Pay
Request waiter
```

according to restaurant configuration.

---

# 131. RESTAURANT COMMAND CENTER

Restaurant homepage should focus on live operations:

```text
Revenue
Orders
Open Tables
Kitchen
ETA
Alerts
```

---

# 132. LIVE OPERATIONS SCREEN

Must be one of the most useful restaurant screens.

Show:

```text
Table
Order
Guest count
Current status
ETA
Waiting time
Payment state
Service request
```

---

# 133. POS UX

Prioritize speed over visual decoration.

---

# 134. KDS UX

Prioritize:

```text
Read
Understand
Act
```

in under a second wherever possible.

---

# 135. MOBILE UX

Staff mobile must:

* work one-handed
* support large touch targets
* work in poor network conditions
* clearly show sync state
* minimize navigation
* support phone and tablet layouts

---

# 136. OFFLINE INDICATOR

Never silently behave offline.

Show state such as:

```text
Online
Offline
Syncing
Pending Sync
Sync Error
```

---

# 137. ACCESSIBILITY

Support:

* sufficient contrast
* keyboard navigation
* text scaling
* screen-reader labels
* focus state
* RTL
* reduced motion

---

# 138. TESTING

Required layers:

```text
Unit
Integration
API
Contract
UI
End-to-End
Load
Security
Offline
```

---

# 139. CRITICAL E2E FLOW

At minimum:

```text
QR
→ Table resolved
→ GuestSession
→ Menu
→ Cart
→ Order
→ KDS
→ ETA
→ Ready
→ Bill
→ Payment
→ Receipt
```

---

# 140. OFFLINE TEST

Test:

```text
Network lost
→ Draft
→ Queue
→ Retry
→ Reconnect
→ Sync
→ No duplicate
```

---

# 141. MARKETPLACE TEST

Each adapter:

```text
Connect
Catalog
Order
Webhook
Status
Cancel
Retry
Duplicate webhook
Failure
Recovery
```

---

# 142. PAYMENT TEST

Test:

```text
Success
Timeout
Failure
Duplicate callback
Partial payment
Refund
Partial refund
Chargeback
```

---

# 143. SECURITY TEST

Test:

```text
Tenant leakage
IDOR
Privilege escalation
Invalid JWT
Expired token
Replay
Rate limit bypass
Fake webhook
Duplicate payment
Duplicate order
```

---

# 144. LOAD TEST

Test gradually.

Potential scenarios:

```text
100 concurrent tables
500 concurrent guests
1000 concurrent order requests
```

Actual production capacity must be established by measurement, not assumptions.

---

# 145. CURSOR IMPLEMENTATION RULE

Cursor must not implement a large feature immediately.

For major features:

```text
Analyze
 ↓
Plan
 ↓
Architecture impact
 ↓
DB impact
 ↓
API impact
 ↓
UI impact
 ↓
Security impact
 ↓
Testing plan
 ↓
Implementation
 ↓
Validation
```

---

# 146. CURSOR MUST ASK:

Before touching multiple modules:

```text
Does this feature affect:
- Domain?
- Database?
- API?
- Customer UI?
- Restaurant UI?
- Mobile?
- Edge?
- Integration?
- Security?
- Billing?
- Analytics?
```

If yes, address each impact.

---

# 147. CURSOR MUST NOT

* rewrite working code unnecessarily
* introduce microservices prematurely
* duplicate business logic
* hard-code business rules
* expose entities directly
* ignore tenant isolation
* bypass authorization
* ignore localization
* ignore RTL
* store secrets
* store card data
* introduce external API coupling
* silently change database behavior

---

# 148. CURSOR SHOULD

* reuse existing abstractions
* write tests
* update documentation
* preserve backwards compatibility
* use domain events where appropriate
* use outbox for side effects
* use idempotency
* use structured logging
* use feature flags
* use existing design components
* maintain localization

---

# 149. BUSINESS RULES MUST NOT BE HARD-CODED

Do not hard-code:

```text
5 orders
10 minutes
20 seconds
Premium
Turkey
7 languages
```

unless those values are true architectural constants.

Configurable business parameters should be stored in configuration/feature policy.

---

# 150. CURSOR UI RULES

```text
Do not hard-code colors.
Do not hard-code spacing.
Do not hard-code user-facing text.
Do not invent a new button style if one exists.
Do not create inconsistent cards.
Do not use color as the only status indicator.
Support loading/empty/error states.
Support localization.
Support RTL.
Support responsive layout.
```

---

# 151. CURSOR MOBILE RULES

```text
Use shared mobile theme.
Use local persistence where feature is offline-capable.
Never assume constant internet.
Always show sync state.
Never silently discard unsynced data.
Never create duplicate orders.
Use idempotency.
```

---

# 152. CURSOR BACKEND RULES

```text
No business logic in controllers.
No direct EF entity serialization.
Use application/domain boundaries.
Validate commands.
Authorize server-side.
Respect tenant scope.
Use transactions intentionally.
```

---

# 153. CURSOR DATABASE RULES

```text
No manual production DB changes.
Every schema change requires migration.
Review index impact.
Review foreign keys.
Review concurrency.
Review historical data.
Review rollback/recovery.
```

---

# 154. CURSOR INTEGRATION RULES

External integrations must have:

```text
Adapter
DTO
Mapping
Error translation
Retry
Idempotency
Logging
Health
Tests
```

No exceptions.

---

# 155. CURSOR PAYMENT RULES

Payment code must always be reviewed for:

```text
Duplicate charge
Duplicate webhook
Timeout
Refund
Race condition
Authorization
Amount mismatch
```

---

# 156. CURSOR AI RULES

AI must never:

* invent business metrics
* fabricate missing data
* silently modify price
* refund money
* alter fiscal records
* delete records
* change subscriptions

without explicit human-approved workflows.

---

# 157. DOCUMENTATION RULE

If architecture changes, update:

```text
ARCHITECTURE.md
DOMAIN_MODEL.md
API_CONTRACTS.md
```

as relevant.

---

# 158. ARCHITECTURE DECISION RECORDS

For important decisions create:

```text
docs/DECISIONS/ADR-XXXX-title.md
```

Examples:

```text
ADR-001-Modular-Monolith
ADR-002-MSSQL
ADR-003-Flutter
ADR-004-Edge-Agent
ADR-005-Channel-Adapter
ADR-006-Transactional-Outbox
```

---

# 159. FEATURE DEVELOPMENT TEMPLATE

Every major Cursor task must follow:

```text
CONTEXT

GOAL

FUNCTIONAL REQUIREMENTS

NON-FUNCTIONAL REQUIREMENTS

AFFECTED DOMAINS

DATABASE CHANGES

API CHANGES

WEB CHANGES

MOBILE CHANGES

EDGE CHANGES

INTEGRATIONS

SECURITY

OBSERVABILITY

TEST PLAN

ACCEPTANCE CRITERIA

ROLLBACK / RECOVERY
```

---

# 160. ACCEPTANCE CRITERIA

Use Given/When/Then when appropriate.

Example:

```text
Given Table 12 exists
And QR-001 is active

When the customer scans QR-001

Then Restaurant 12 context is resolved
And Branch is displayed
And Table 12 is displayed
And the customer cannot select a different table
And a GuestSession is created
```

---

# 161. FEATURE COMPLETE DEFINITION

Feature is NOT complete unless:

```text
[ ] Functional behavior complete
[ ] Database complete
[ ] API complete
[ ] UI complete
[ ] Mobile complete if applicable
[ ] Security reviewed
[ ] Tenant isolation reviewed
[ ] Localization reviewed
[ ] Accessibility reviewed
[ ] Logging reviewed
[ ] Tests pass
[ ] Build passes
[ ] Documentation updated
```

---

# 162. MVP

The first commercial MVP contains:

```text
Restaurant
Branch
Table
QR
Menu
Localization
GuestSession
TableSession
Cart
Order
Basic KDS
Basic ETA
Order Tracking
Waiter Call
Digital Bill View
Restaurant Dashboard
Core Security
```

---

# 163. PHASE 2

```text
Waiter Mobile
Fixed Table Tablet
Group Ordering
Continuous Tab
Server Started Order
Reorder
Digital Payment
Split Bill
Receipt
Tips
Review
```

---

# 164. PHASE 3

```text
KDS Advanced
POS
Fiscal/ÖKC
Marketplace
Website ordering
Pickup
Delivery
Inventory
Recipe
Cost
Purchasing
Multi Branch
```

---

# 165. PHASE 4

```text
CRM
Loyalty
Campaigns
Restaurant Health
Benchmark
Advanced Analytics
```

---

# 166. PHASE 5

```text
AI Translation
AI Recommendation
AI ETA
AI Forecast
AI Inventory
AI Menu
AI Restaurant Advisor
```

---

# 167. PHASE 6

```text
Reservation
Waitlist
White Label
Hotel/Resort
API Marketplace
Enterprise
Advanced Delivery
Social Ordering
```

---

# 168. PRODUCT EVALUATION FRAMEWORK

Before adding a feature, ask:

1. Does it solve a real restaurant problem?
2. Does it improve revenue?
3. Does it reduce cost?
4. Does it reduce service time?
5. Does it improve customer experience?
6. Does it provide measurable value?
7. Can it be maintained?
8. Does it complicate the MVP unnecessarily?

Do not implement features only because competitors have them.

---

# 169. PRODUCT DIFFERENTIATION

Primary differentiation should be:

```text
Smart QR Security
+
Table Session
+
One Order Engine
+
Hybrid Ordering
+
ETA Intelligence
+
Offline Staff
+
Cloud + Edge
+
Channel-neutral Integration
+
Restaurant Health
+
Benchmark
+
AI Advisor
```

---

# 170. SUCCESS METRICS

Measure:

```text
QR Scans
Menu Opens
Cart Conversion
Order Conversion
Average Order Value
Orders per Table
Table Turnover
ETA Accuracy
Waiter Response Time
Payment Conversion
Refund Rate
Marketplace Failure Rate
Active Restaurants
Paid Conversion
Churn
MRR
```

---

# 171. FINAL CURSOR CONSTITUTION

The following principles override convenience:

```text
SECURITY > SPEED OF IMPLEMENTATION

CORRECTNESS > FEATURE COUNT

MAINTAINABILITY > CLEVERNESS

CONSISTENCY > INDIVIDUAL DEVELOPER PREFERENCE

DOMAIN BOUNDARIES > SHORTCUTS

DATA INTEGRITY > UI CONVENIENCE

BACKWARD COMPATIBILITY > QUICK BREAKING CHANGES

REAL MEASURED DATA > ASSUMPTIONS

CUSTOMER EXPERIENCE > TECHNICAL ELEGANCE

RESTAURANT OPERATIONAL RELIABILITY > DECORATIVE UI
```

---

# 172. FINAL CURSOR MASTER PROMPT

Use the following as the principal project-level Cursor instruction:

```text
You are the principal architect and senior engineer responsible for the Restaurant OS platform.

The repository contains multiple applications but one coherent product.

You must treat MASTER_CURSOR_SPECIFICATION.md as the primary product and architecture authority.

Your responsibilities are:

1. Understand before implementing.
2. Preserve existing architecture.
3. Preserve domain boundaries.
4. Preserve tenant isolation.
5. Protect data integrity.
6. Implement securely.
7. Reuse existing abstractions.
8. Reuse existing UI components.
9. Use design tokens.
10. Maintain localization and RTL support.
11. Maintain responsive design.
12. Maintain backward compatibility.
13. Add tests.
14. Update documentation.
15. Consider future modules without prematurely implementing them.

Never assume that a requested feature is isolated.

Analyze its effect on:
- domain
- database
- API
- security
- UI
- mobile
- Edge Agent
- integrations
- analytics
- billing
- observability

For large features:

First provide:
- architecture analysis
- affected files
- affected domains
- database changes
- API changes
- UX changes
- security risks
- test plan
- rollback/recovery considerations

Only then implement.

Never:
- rewrite the architecture unnecessarily
- create microservices prematurely
- duplicate Order logic for every channel
- couple domain logic to marketplace APIs
- expose EF entities directly
- hard-code business rules
- hard-code user-facing text
- store card data
- bypass authorization
- bypass tenant isolation
- silently overwrite financial data
- silently discard offline operations
- ignore duplicate webhook events
- ignore idempotency
- ignore race conditions
- ignore localization
- ignore RTL
- invent data

For external integrations:
External DTO → Adapter → Canonical Model → Domain.

For asynchronous side effects:
DB Transaction → Outbox → Worker → External Effect.

For offline:
Local DB → Sync Queue → Idempotency → Retry → Conflict Resolution.

For payment:
PaymentIntent → Provider → Webhook → Reconciliation.

For fiscal:
Fiscal abstraction → Edge Agent → Device Adapter.

For UI:
Existing component → extend → new component only if necessary.

Always implement:
loading state
empty state
error state
retry state
offline state where relevant
success state

When code changes:
- build
- test
- review
- inspect warnings
- inspect security
- inspect migration
- inspect backwards compatibility

A feature is complete only when the acceptance criteria pass.

When unsure:
do not guess about architecture.
Inspect the repository and existing documentation first.

When requirements conflict:
identify the conflict,
explain the consequences,
propose the safest solution,
do not silently violate the master specification.

The project must evolve toward:

QR Ordering
→ Restaurant Operations
→ Multi-channel Order Management
→ POS/Fiscal
→ Inventory/Cost
→ Analytics
→ Benchmark
→ AI Restaurant Advisor

The goal is not maximum feature count.

The goal is:
a reliable,
secure,
fast,
maintainable,
beautiful,
scalable,
commercially valuable
Restaurant Operating System.
```

---

# 173. IMPORTANT: CURSOR DEVELOPMENT ORDER

Do NOT ask Cursor to build the entire project in one task.

The first tasks must be:

```text
TASK 001
Repository + architecture skeleton

TASK 002
Design System

TASK 003
Authentication foundation

TASK 004
Tenant / Restaurant / Branch

TASK 005
Table + QR

TASK 006
Menu + Localization

TASK 007
Guest Session + Table Session

TASK 008
Cart + Order

TASK 009
Customer Web

TASK 010
Restaurant Command Center

TASK 011
KDS

TASK 012
ETA Engine

TASK 013
Waiter Mobile

TASK 014
Offline Sync

TASK 015
Edge Agent foundation

TASK 016
Payment abstraction

TASK 017
Fiscal abstraction

TASK 018
First marketplace adapter

TASK 019
Inventory foundation

TASK 020
Analytics

TASK 021
Restaurant Health

TASK 022
Benchmark

TASK 023
AI foundation
```

Her görev tamamlandıktan sonra build/test/review yapılmalıdır.

---

# 174. FINAL PRODUCT VISION

The final platform should feel like:

```text
Modern SaaS
+
Restaurant POS
+
QR Ordering
+
Kitchen Management
+
Inventory
+
Marketplace Hub
+
Customer Platform
+
Business Intelligence
+
AI Advisor
```

but without forcing the customer or restaurant to understand this complexity.

The complexity belongs inside the platform.

The user experience must remain simple.

---

# 175. FINAL PRINCIPLE

The customer should think:

> “QR'ı okuttum ve sipariş verdim.”

The waiter should think:

> “Masayı seçtim ve siparişi girdim.”

The kitchen should think:

> “Sıradaki işi görüyorum.”

The cashier should think:

> “Hesap ve ödeme net.”

The owner should think:

> “Restoranımın tamamını görüyorum.”

The platform should think:

> “All data is connected.”

That is the final goal of Restaurant OS.
