# SCREEN-002: Main Navigation Shell

## PURPOSE
Provide the primary application window with a sidebar menu for navigating between all screens. Hosts user session info, lock/logout controls, and the content panel where all forms are displayed.

## PERMISSIONS
- Requires authenticated session
- Menu items are shown/hidden based on user permissions via `ApplyPermissionsAsync()`
- Lock and Logout buttons require `ChangeSettings` permission

## EXACT FIELDS

| #  | Field           | Type    | Notes                                              |
|----|-----------------|---------|----------------------------------------------------|
| 1  | User Name Label | Label   | Top of sidebar; shows logged-in user's full name   |
| 2  | Role Label      | Label   | Below user name; shows role (مدير / مشرف / كاشير)  |

## EXACT BUTTONS (Sidebar Menu Items)

| #  | Button Label (Arabic) | Permission           | Action                         |
|----|-----------------------|----------------------|--------------------------------|
| 1  | 🖥️ شاشة البيع         | Sell                 | Opens PosTerminalForm          |
| 2  | 📊 لوحة التحكم         | ViewDashboard        | Opens DashboardForm            |
| 3  | 📦 المنتجات            | EditProducts         | Opens ProductListForm          |
| 4  | 🏪 المخزون             | AdjustInventory      | Opens InventoryForm            |
| 5  | 📋 التقارير            | ViewReports          | Opens ReportForm               |
| 6  | 👤 المستخدمين          | ManageUsers          | Opens UserManagementForm       |
| 7  | ⚙️ الإعدادات          | ChangeSettings       | Opens SettingsForm             |
| 8  | 🪑 الطاولات            | ManageTables         | Opens TableMapForm             |
| 9  | 🖨️ الطابعات           | ManageUsers          | Opens PrinterManagementForm    |
| 10 | 🍳 المطبخ              | ManageUsers          | Opens KitchenDisplayForm       |
| 11 | 💾 النسخ الاحتياطي     | Backup               | Opens BackupForm               |
| 12 | 📜 سجل العمليات        | ManageUsers          | Opens AuditLogForm             |
| 13 | 👥 العملاء             | ManageUsers          | Opens CustomerListForm         |
| 14 | 🚚 الموردين            | ManageSuppliers      | Opens SupplierListForm         |
| 15 | 📥 أوامر الشراء        | ManagePurchases      | Opens PurchaseOrderForm        |
| 16 | 🔄 الورديات            | ViewReports          | Opens ShiftForm                |

## TOP BAR BUTTONS

| #  | Button    | Permission      | Action                                          |
|----|-----------|-----------------|-------------------------------------------------|
| 1  | 🔒 قفل     | ChangeSettings  | Locks screen; shows login overlay to unlock     |
| 2  | 🚪 خروج   | ChangeSettings  | Logs out; returns to LoginForm                  |

## UI STATES

| State       | Description                                   |
|-------------|-----------------------------------------------|
| Loading     | Initial state while user context is loaded    |
| Active      | Normal operation; sidebar is visible          |
| Locked      | Overlay covers content; requires re-login     |

## ACCEPTANCE CRITERIA

1. **AC-001:** Sidebar shows only menu items the logged-in user has permissions for.
2. **AC-002:** Clicking a menu item opens the corresponding form in the content panel.
3. **AC-003:** Lock button overlays a login prompt; user must re-enter password.
4. **AC-004:** Logout returns to LoginForm.
5. **AC-005:** Active menu item is highlighted with the primary color.
6. **AC-006:** User name and role are displayed at the top of the sidebar.
7. **AC-007:** RTL layout is maintained throughout the shell.
8. **AC-008:** Menu items that require unavailable permissions are hidden (not disabled).
