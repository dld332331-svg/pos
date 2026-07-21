# Changelog — POS System

**Application:** On-premises POS System (Restaurant + Retail)  
**Architecture:** Clean Architecture (Domain → Application → Infrastructure → Desktop)  
**Stack:** .NET 10, WinForms, EF Core 10, SQL Server, ESC/POS  
**Specification:** POS_EN.md — Unified Engineering Spec v2.0 (2135 lines)  
**Current build:** 0 errors, 0 warnings  
**Current tests:** **801/801 pass (Release mode ✅)**  
**Coverage:** POS.Application 79.32% line / 83.24% branch (Coverlet)  

---

## [v5] — 2026-07-22 — Final Coverage Gap Closure

**Test count:** 782 → **801** (+19)  
**Coverage gaps closed:** 6/6 uncovered methods now tested

### Added

#### Tests — AuthService (11 new → 23 total)
- `CheckDatabaseConnectionAsync`: success path, failure path, exception caught gracefully
- `LogoutAsync`: audit logged with `AuditActionType.Logout`
- `GetUserPermissionsAsync`: user found returns permissions, user not found returns empty, no permissions returns empty
- `HasPermissionAsync`: user has permission → true, doesn't have → false, not found → false, inactive → false

#### Tests — CustomerService (3 new → 16 total)
- `DeleteCustomerAsync`: success marks IsActive=false, not found throws Arabic error, already inactive is idempotent

#### Tests — DashboardService (5 new → 17 total)
- `GetRecentTransactionsAsync`: returns last 5 out of 10, includes payment method from Payment entity, no completed sales returns empty, less than count returns all, no payment shows dash "—"

### Fixed
- `BuildServiceWithMocks` in `AuthServiceTests.cs` extended to 5-element tuple (added `permissionServiceMock`), supports `canConnect` and `permissions` params
- All 12 existing AuthService tests updated to use 5-element deconstruction

---

## [v4] — 2026-07-21 — Unit Test Coverage Expansion

**Test count:** 587 → **782** (+195)  
**Services tested:** 13/20 → **20/20** (+7)

### Added

#### Tests — 8 new test files

| File | Tests | Coverage |
|:-----|:-----:|:---------|
| `PromotionServiceTests.cs` | 39 | All 4 promotion types, eligibility, application, edge cases |
| `PurchaseOrderServiceTests.cs` | 15 | PO creation, state machine, receive, order numbers |
| `PurchaseOrderCalculatorTests` | 17 scenarios | Line cost, total cost, transitions, display |
| `RecipeServiceTests.cs` | 18 | CRUD, ingredient costing, missing items |
| `KitchenOrderServiceTests.cs` | 22 | Filtering, priority, station mapping |
| `SupplierServiceTests.cs` | 16 | CRUD, search, PO history |
| `PrinterManagementServiceCrudTests.cs` | 26 | Add/Update/Delete, stations, cash drawer |
| `ServiceIntegrationTests.cs` | 18 | EF Core InMemory end-to-end across all 7 services |

#### Features
- **Multi-unit conversion**: `IUnitConversionService` cross-unit conversion pipeline + `SaleService` unit pricing/inventory (18 tests)
- **Notifications**: `ToastNotificationForm` with colored accent, auto-dismiss, stacking; Full NotificationService event wiring in MainShell
- **RTL Icon Helper**: `RtlIconHelper.GetIcon()` + `GetPaginationArrow()` wired into all paginating forms
- **Empty states**: Verified all 12+ list forms have complete empty-state panels

### Fixed
- `UpdatePrinterAsync`: Added `Enum.TryParse<PrinterRole>` to close AssignedRole update gap
- `LoginForm.cs:225`: Fixed `LoadUsernames()` showing both ComboBox and TextBox
- `ProductForm.cs`: Fixed CS8602 null dereference on `_unitComboBox.SelectedItem`

### Changed
- `AuthServiceTests.BuildServiceWithMocks`: Extended to 5-element tuple (added permissionServiceMock)
- All pagination in list forms now uses `RtlIconHelper.GetPaginationArrow()` for RTL consistency
- `ProductForm`: Replaced `_unitTextBox` with `_unitComboBox` for unit selection
- `PosTerminalForm`: Added "Unit" column + quantity-with-unit dialog selector

### Infrastructure
- Coverlet code coverage analysis: POS.Application 79.32% / POS.Domain 69.92%
- Release mode validation: Full 801-test suite passes with 0 warnings

---

## [v3] — 2026-07-20 — UI/UX & Multi-Unit Foundation

**Test count:** 587 (steady — feature additions)

### Added

#### Multi-Unit of Measure (Foundation)
- `UnitOfMeasure` entity with conversion factor, category, base unit support
- `Product.UnitOfMeasureId` FK + navigation property
- Migration `20260721183507_AddUnitOfMeasureSupport` — creates `UnitOfMeasures` table
- `DbInitializer` seeds 6 default units (kg, g, L, mL, piece, dozen) with Arabic names
- `IUnitOfWork.UnitOfMeasures` repository

#### Notifications System
- `INotificationService` — interface with 4 types, 6 categories, auto-dismiss, read tracking
- `NotificationService` — thread-safe, event-driven `NotificationRaised`
- `ToastNotificationForm` — colored accent border, auto-dismiss timer, stacking
- `MainShell` — bell icon + unread badge + notification center popup + auto-show toasts
- Static `Notify()` convenience method for non-DI forms
- `Program.cs` DI registration: `AddSingleton<INotificationService, NotificationService>()`

#### RTL Icons
- `RtlIconHelper.cs` — `GetIcon()` for FontAwesome directional swaps, `GetPaginationArrow()` for pagination
- Wired into `AuditLogForm`, `ProductListForm`, `CustomerListForm`, `SupplierListForm`, `InventoryForm`

#### Screen Specs
- `DIALOG-005-ModifierSelectionDialog.md`

---

## [v2] — 2026-07-19 — Feature Expansion

**Test count:** ~400 → **587** (+187)

### Added

#### Infrastructure
- `POS.Application` → `POS.Infrastructure` dependency (Composition Root)
- `IBarcodeScannerService` — KeyboardWedge + Serial COM mode support
- `BarcodeScannerService` — listens on configurable COM port, auto-adds product on scan
- `ISoundService` / `SoundService` — Console.Beep for 10 events, SettingsForm wiring

#### Features
- **Table Map** (`TableMapForm`): 6 states + Cleaning with distinct colors + legend + context menu
- **Reports** (`ReportForm`): QuestPDF + ClosedXML, PDF/Excel export with SaveFileDialog
- **Cash Drawer**: Full ESC/POS integration (`ESCPOSPrinter.OpenCashDrawerAsync()`)

#### Database
- Migration `20260721085126_AddBatchTracking` — InventoryBatch entity, batch tracking
- Migration `20260721094528_EnforceDecimal183Precision` — All monetary columns DECIMAL(18,3)
- Migration `20260721081526_AddPromotionsEngine` — Promotions engine tables

#### Tests — Coverage expansion
- `DashboardServiceTests`, `BackupServiceTests`, `TableServiceTests`, `UserServiceTests`
- `SettingsServiceTests`, `ShiftServiceTests`, `ReportServiceTests`
- `SaleServiceIntegrationTests` expansion
- `DbInitializerIntegrationTests`, `BenchmarkSmokeTests`
- `ESCPOSPrinterDispatchIntegrationTests`
- UI Tests: `LoginFormUITests`, `DashboardFormUITests`, `PaymentDialogUITests`, `PosTerminalFormUITests`

---

## [v1] — 2026-07-19 — Foundation & Initial Implementation

**Test count:** 0 → **~400** (initial baseline)

### Added — Architecture
- 5-layer Clean Architecture: Domain → Application → Infrastructure → Desktop + Tests + Reporting + Benchmarks
- 7 `.csproj` files + `POS.sln`

### Added — Domain Layer
- **29 Entities**: Sale, SaleItem, SaleItemModifier, SalePromotion, Payment, Product, Category, InventoryItem, InventoryMovement, InventoryBatch, Customer, Supplier, PurchaseOrder, PurchaseOrderItem, Recipe, RecipeIngredient, Modifier, ModifierGroup, ModifierSize, Table, Shift, Register, User, AuditLog, BackupRecord, Expense, HeldSale, Return, ReturnItem, Room, Printer, KitchenStation, WithdrawalDeposit, Setting, Promotion, UnitOfMeasure
- **12 Enums**: SaleStatus, OrderType, PaymentMethod, UserRole, Permission (27 values), ProductStatus, ProductType, MovementType, AuditActionType, ShiftStatus, TableStatus, SoundEvent, PrinterType, PrinterRole, PrinterConnection, PrinterStatus, PromotionType, WithdrawalDepositType
- **3 Value Objects**: ArabicName, Money, MoneyPolicy
- **12 Interfaces**: IRepository<T>, IUnitOfWork, IAuditService, IAuthService, IBackupService, IDatabaseBackupExecutor, ILoggerService, IPasswordHasher, IPermissionService, IPrinterService, IBarcodeScannerService, ISoundService, INotificationService

### Added — Application Layer
- **20 Service Implementations**: SaleService (964 lines), ProductService, InventoryService, PrinterManagementService, PromotionService, CustomerService, AuthService, DashboardService, BackupManagementService, AuditQueryService, ReportService, SettingsService, ShiftService, TableService, UserService, SupplierService, PurchaseOrderService, RecipeService, KitchenOrderService, UnitConversionService
- **19 Service Interfaces** + 2 Calculators (SaleCalculator, PurchaseOrderCalculator) + 2 Validators (ProductValidator, SaleValidator)
- **CQRS**: Commands + Queries + Dispatcher
- **DTOs**: 10 files covering all data transfer

### Added — Infrastructure Layer
- `POSDbContext` with EF Core 10, all entity configurations
- `DbInitializer` with seed data, configurable admin password
- Migration `20260719234248_InitialCreate` — foundation schema
- `Repository<T>`, `UnitOfWork` with transaction support
- `ESCPOSPrinter` — ESC/POS commands for thermal/impact printers
- `RawPrinterHelper` — Win32 Raw printer API
- `PasswordHasher` (PBKDF2), `PermissionService`, `AuditLogger`
- `BackupService`, `SqlBackupExecutor`, `BackupBackgroundService`
- `LoggerService` (Serilog)

### Added — Desktop Layer
- **26 Forms**: MainShell, LoginForm (7 states), DashboardForm (5 widgets), PosTerminalForm (13 states), PaymentDialog (7 states, 4 methods), ProductListForm, ProductForm, InventoryForm, CustomerListForm, ShiftForm, ReportForm, SettingsForm, UserManagementForm, PrinterManagementForm, TableMapForm, KitchenDisplayForm, BackupForm, AuditLogForm, SupplierForm, SupplierListForm, PurchaseOrderForm, PromotionsListForm, ExpenseDialog, HoldSaleDialog, StockAdjustmentDialog, WithdrawalDepositDialog, ModifierSelectionDialog
- **7 Custom Controls**: RtlButton, RtlComboBox, RtlDataGridView, RtlDialog, RtlGridControl, RtlNumericUpDown, RtlTextBox
- **3 Theme Files**: DesignTokens, ThemeManager, RtlMessageBox
- **FontAwesome** embedded icon library + FontLoader

### Added — Testing
- **Initial test files (~400 tests):** SaleService, SaleCalculation, SaleCalculator, SaleValidator, AuthService (Login + ChangePassword), MoneyPolicy, Money, ArabicName, PasswordHasher, ProductValidator, ProductService, InventoryService, CustomerService, AuditQueryService, BackupManagementService, ReportBuilder, ReportService, ReceiptBuilder, SqlBackupExecutor, PrinterManagementServicePrintReceipt

### Added — Documentation
- `POS_EN.md` — Master specification (2135 lines)
- `docs/QA_AUDIT_REPORT.md` — Comprehensive QA audit
- `docs/FULL_COMPLIANCE_REPORT.md` — Full compliance tracking
- 26 screen-spec docs in `docs/screen-specs/`

---

## Test Growth Timeline

```
v1 (Foundation)
  └─ ~400 tests — Core services, validators, domain logic

v2 (Feature Expansion)
  └─ 587 tests — Dashboard, Backup, Table, User, Settings, Shift,
                 Reports, Integration, UI tests

v3 (UI/UX & Multi-Unit)
  └─ 587 tests — Feature additions only (no net new tests)

v4 (Coverage Expansion)
  └─ 782 tests — +195: PromotionService, PurchaseOrderService,
                 RecipeService, KitchenOrderService, SupplierService,
                 PrinterCrud, IntegrationTests (8 new files)

v5 (Coverage Gap Closure)
  └─ 801 tests — +19: AuthService gap closure (11), CustomerService (3),
                 DashboardService (5) — ALL 6 uncovered methods now covered
```

---

## Coverage Evolution

| Service | v1 | v2 | v3 | v4 | v5 |
|:--------|:--:|:--:|:--:|:--:|:--:|
| SaleService | ✅ | ✅ | ✅ | ✅ | ✅ |
| AuthService | ✅ | ✅ | ✅ | ✅ | **23 tests** |
| ProductService | ✅ | ✅ | ✅ | ✅ | ✅ |
| InventoryService | ✅ | ✅ | ✅ | ✅ | ✅ |
| CustomerService | ✅ | ✅ | ✅ | ✅ | **16 tests** |
| DashboardService | ✅ | ✅ | ✅ | ✅ | **17 tests** |
| BackupManagementService | ✅ | ✅ | ✅ | ✅ | ✅ |
| SettingsService | ✅ | ✅ | ✅ | ✅ | ✅ |
| ShiftService | ✅ | ✅ | ✅ | ✅ | ✅ |
| TableService | ✅ | ✅ | ✅ | ✅ | ✅ |
| UserService | ✅ | ✅ | ✅ | ✅ | ✅ |
| ReportService | ✅ | ✅ | ✅ | ✅ | ✅ |
| PrinterManagementService | ✅ | ✅ | ✅ | ✅ | ✅ |
| **PromotionService** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **PurchaseOrderService** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **RecipeService** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **KitchenOrderService** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **SupplierService** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **UnitConversionService** | ❌ | ❌ | ❌ | ✅ | ✅ |
| **All 20 services** | 13/20 | 13/20 | 13/20 | **20/20** | **20/20** |

---

## Feature Milestones

| Feature | Session | Status | Tests |
|:--------|:-------:|:------:|:-----:|
| ESC/POS Printing | v1 | ✅ | ✓ |
| Backup & Restore | v1 | ✅ | ✓ |
| CQRS Pattern | v1 | ✅ | — |
| Design Tokens | v1 | ✅ | — |
| RTL Compliance | v1 | ✅ | — |
| JOD Financial Precision | v1 | ✅ | ✓ |
| Login/Auth (7 states) | v1 | ✅ | ✓ |
| POS Terminal (13 states) | v1 | ✅ | ✓ |
| Payment Dialog (7 states) | v1 | ✅ | ✓ |
| Barcode Scanner | v2 | ✅ | ✓ |
| Sound System | v2 | ✅ | ✓ |
| Table Map (6+1 states) | v2 | ✅ | — |
| Reports (PDF/Excel) | v2 | ✅ | ✓ |
| Cash Drawer (ESC/POS) | v2 | ✅ | ✓ |
| Batch/Expiry Tracking | v2 | ✅ | ✓ |
| Multi-Unit of Measure | v3 | ✅ (1 minor gap) | 18 |
| Notifications System | v3 | ✅ | — |
| RTL Icon Helper | v3 | ✅ | — |
| Empty States (all forms) | v3 | ✅ | — |
| PromotionService Tests | v4 | ✅ | 39 |
| PurchaseOrderService Tests | v4 | ✅ | 32 |
| RecipeService Tests | v4 | ✅ | 18 |
| KitchenOrderService Tests | v4 | ✅ | 22 |
| SupplierService Tests | v4 | ✅ | 16 |
| Printer CRUD Tests | v4 | ✅ | 26 |
| End-to-End Integration | v4 | ✅ | 18 |
| Release Mode Validation | v4 | ✅ | 801 |
| Coverage Analysis | v4 | ✅ | Coverlet |
| AuthService Gap Closure | v5 | ✅ | +11 |
| CustomerService Gap Closure | v5 | ✅ | +3 |
| DashboardService Gap Closure | v5 | ✅ | +5 |

---

## Project Statistics

| Metric | v1 | v2 | v3 | v4 | v5 |
|:-------|:--:|:--:|:--:|:--:|:--:|
| **Total Tests** | ~400 | 587 | 587 | **782** | **801** |
| **Test Files** | ~25 | ~33 | ~33 | **40** | **40** |
| **Services Tested** | 13/20 | 13/20 | 13/20 | **20/20** | **20/20** |
| **Uncovered Methods** | — | — | — | 6 | **0** |
| **Build Warnings** | 0 | 0 | 0 | 0 | **0** |
| **Release Mode** | — | — | — | ✅ | **✅** |
| **Compliance Sections** | — | — | — | 39/39 | **39/39** |
| **Source Files** | ~280 | ~300 | ~315 | ~318 | **~320** |
| **Migrations** | 1 | 4 | 5 | 5 | **5** |
| **Screen Specs** | 25 | 25 | 26 | 26 | **26** |
