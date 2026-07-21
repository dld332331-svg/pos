# Final Engineering Specification Document for POS System Implementation
## Strict Engineering Contract • Full RTL UI/UX • On-Premises Windows Environment • Restaurants & Supermarkets

**Document Version:** FINAL 2.0 (Comprehensive and Enhanced Engineering Version)
**Document Type:** Authoritative Engineering Contract covering: Software Architecture, UI/UX, Business Logic, Data Integrity, Hardware Integration, QA, and Implementation Protocols for AI and Developers.
**Target Platform:** Windows Desktop.
**Business Model:** On-Premises operation for Restaurant + Supermarket.
**Primary Language:** Arabic.
**Layout Direction:** Right-to-Left (RTL).
**Currency:** Jordanian Dinar (JOD) — Strict financial precision with 3 decimal places.
**Operating Model:** Fully On-Premises, with no dependency on cloud services for core operations.
**Primary Goal:** Build a fast, stable, modern, professional, maintainable, and scalable commercial system.

---

## 0. DOCUMENT STATUS AND AUTHORITY

This document constitutes the **final and binding engineering contract** for implementing the POS system.
This document is not merely suggestions, an inspiration document, or a general product description.

The implementation team and every AI Coding Agent **must** treat this document as the absolute and exclusive reference for:
- System Architecture and Infrastructure.
- UI/UX Behavior.
- Arabic RTL Layout.
- Screen Structure and Component Behavior.
- Business Rules and Permissions.
- Data Integrity and Financial Handling.
- Hardware Integration and Printing.
- Backup and Testing.
- Quality Acceptance Criteria.

When a requirement is explicitly stated here, the implementation **must** follow it precisely.
When a specific detail is not specified:
1. Do not invent unnecessary complexity.
2. Choose the safest and simplest implementation.
3. Maintain the approved architecture and design system.
4. Document the assumption on which your decision was based.
5. Do not introduce features unrelated to the requirements.

---

## 1. ABSOLUTE PROJECT RULES

The following rules are mandatory and non-negotiable.

### 1.1 The System MUST
- **On-Premises Operation:** Operate fully locally without the Internet for core operations.
- **Design and Interface:** Support Arabic RTL interface as primary, use a modern and consistent design system, rely on centralized Design Tokens, and use a coherent icon and font system.
- **Financial Precision:** Use strict financial precision for the Jordanian Dinar (JOD) with three decimal places in all calculations and databases.
- **Data Integrity:** Protect financial records and inventory history from arbitrary modification, and support comprehensive auditability.
- **Continuity:** Support local backup and reliable recovery.
- **Integration:** Support receipt printing and kitchen/section printing.
- **Performance:** Remain responsive during normal operations and peak times.
- **Usability:** Support keyboard, mouse, and touch (where appropriate), and provide clear states for loading, empty, error, disabled, and permissions.
- **Engineering:** Strictly separate the UI from Business Logic and Data Access.
- **Compatibility:** Support older and modern Windows devices as much as possible.

### 1.2 The System MUST NOT
- **Dependencies:** Rely on cloud services for core sales, or require the Internet for login or normal operation.
- **Calculations:** Use floating-point arithmetic for money (`decimal` must be used).
- **Data:** Physically delete financial records as a shortcut, or silently overwrite inventory records.
- **Interface:** Place Arabic content in an LTR layout by mistake, create random visual styles for each screen, mix incompatible component styles, allow text overflow over controls, or allow element overlap.
- **User Experience:** Create visual clutter, unusable dense forms, huge purposeless empty spaces, unclear destructive actions, or dialogs without complete states.
- **Engineering:** Place business logic directly in UI event handlers, access the database directly from view elements, add packages without a documented reason, or ignore build errors.
- **Quality:** Consider a feature complete without passing acceptance tests.

---

## 2. SYSTEM MISSION

The system is a professional commercial POS platform for Windows environment serving two operational modes:

### 2.1 Restaurant Mode
- Dine-in, Takeaway, and Delivery orders.
- Table, room/section management.
- Modifiers, Add-ons, and Sizes.
- Recipes and ingredient deduction.
- Kitchen stations and section printing.
- Order notes, split bills, merge bills, and order transfers.

### 2.2 Supermarket / Retail Mode
- Fast barcode sales and product search.
- Weighted products.
- Units of measure and multiple selling units.
- Promotions.
- Inventory tracking, expiry tracking, and batch/lot tracking.

### 2.3 Shared Modules
Both modes share: Products, Users, Permissions, Sales, Payments, Inventory, Reports, Printers, Settings, Backup, and Audit.

---

## 3. OPERATING MODEL

### 3.1 Local-First
The system must operate entirely within the business premises. Core operations that **must** work without the Internet include: Login, product search, barcode sales, restaurant orders, payments, receipt and kitchen printing, inventory, returns, shifts, reports, and backup.

### 3.2 Local Network
The system must support installation on a single computer, or across a Local Area Network (LAN) with multiple terminals.
**Recommended Network Architecture:**
- **Host Server:** A Windows machine hosting the local database.
- **Terminals:** Cashier POS stations, kitchen display stations, and management POS stations.
- **Connected Devices:** Receipt printers, section printers, and cash drawers.

---

## 4. TECHNOLOGY AND ENGINEERING ARCHITECTURE

### 4.1 Approved Baseline
- **Language:** C#.
- **Platform:** Modern .NET Windows Desktop.
- **UI Framework:** WinForms (with an approved commercial UI component library to ensure professionalism).
- **Database:** SQL Server / SQL Server Express (or an approved local relational database).
- **Data Access:** Entity Framework Core (or an approved data access strategy).
- **Integration:** ESC/POS-compatible printer integration.
- **Infrastructure:** Structured logging and automated backup.

### 4.2 Dependency Governance
Every external library or package must have: an exact version, a documented purpose, a clear license status, and compatibility verification. Adding libraries solely for their popularity is prohibited.

### 4.3 Solution Structure
A Clean Architecture must be followed to ensure maintainability:

```text
POS.sln
│
├── POS.Domain (Depends on nothing)
│   ├── Entities
│   ├── ValueObjects
│   ├── Enums
│   ├── BusinessRules
│   └── Interfaces
│
├── POS.Application (Depends on Domain)
│   ├── Commands & Queries (CQRS)
│   ├── Services
│   ├── DTOs
│   ├── Validators
│   └── Interfaces
│
├── POS.Infrastructure (Depends on Application and Domain)
│   ├── Database (DbContext)
│   ├── Repositories
│   ├── Printing
│   ├── Hardware
│   ├── Backup
│   ├── Logging
│   └── Security
│
├── POS.Desktop (Depends on Application)
│   ├── Forms
│   ├── Views
│   ├── Custom Controls
│   ├── Navigation
│   ├── Themes
│   └── Resources
│
├── POS.Reporting
└── POS.Tests
```

### 4.4 Strict Dependency Direction
- `Desktop` depends on `Application`.
- `Application` depends on `Domain`.
- `Infrastructure` depends on `Application` and `Domain`.
**Architectural Prohibitions:** UI directly accessing the database (`SQL`), writing data directly, or performing core financial calculations within the UI are forbidden.

---

## 5. FINANCIAL PRECISION

All financial values **must** use decimal arithmetic (`decimal` in C# and `DECIMAL(18,3)` in SQL).
- **Currency:** JOD.
- **Display:** `0.000` (e.g., `1.000`, `0.250`, `12.375`).
- **Prohibitions:** The use of floating-point types (`float`, `double`) is strictly forbidden for: price, cost, tax, discount, subtotal, total, payment, change, or profit.
- **Policy:** All calculations must pass through a centralized Money Policy in the `Domain` layer.

---

## 6. DESIGN PHILOSOPHY

The application must look and behave like a modern professional commercial product.

### 6.1 Visual Characteristics
The UI must be:
- **Modern:** Reflects current desktop application design trends.
- **Clean:** Free of visual clutter and unnecessary elements.
- **Restrained:** Use of colors and visual elements wisely and balanced.
- **Calm:** A design that does not cause visual fatigue during long work shifts.
- **High-Contrast:** To ensure readability and distinguishability of elements.
- **Readable:** Use clear fonts and text sizes.
- **Consistent:** Apply the same design principles across all screens and components.
- **Fast:** Instant response to inputs and avoidance of visual lag.
- **Suitable for long shifts:** A design that reduces visual and mental fatigue for users.

### 6.2 Avoid
- Random gradients.
- Excessive shadows.
- Decorative cards everywhere.
- Excessive rounded corners.
- Random and uncoordinated colors.
- Random and inconsistent font sizes.
- Unjustified visual noise.
- Tiny controls that are difficult to interact with.
- Unexplained empty spaces.
- Old-fashioned dense forms.

### 6.3 Visual Hierarchy
Every screen must clearly answer the following questions for the user:
1. **Where am I?** (Identifying the current location in the application).
2. **What is the primary task?** (The main goal of the screen).
3. **What is the primary action?** (The most important button or action).
4. **What requires attention?** (Alerts, errors, notifications).
5. **How can I cancel or leave?** (Safe exit methods from the screen).

---

## 7. DESIGN TOKEN SYSTEM

No screen may invent its own visual values. All visual values must be derived from a centralized Design Token System.

### 7.1 Spacing
A standard spacing system must be used to ensure visual consistency and readability:

| Value (px) | Token | Description |
|------------|-------|-------------|
| `4px`      | `Micro` | For very fine spacing between adjacent elements. |
| `8px`      | `Small` | For small spacing between elements within a single component. |
| `12px`     | `Compact` | For compact spacing, such as between input fields in a form. |
| `16px`     | `Standard` | The standard spacing between components or sub-sections. |
| `20px`     | `Medium` | For medium spacing, such as between groups of controls. |
| `24px`     | `Section` | For spacing between major sections within a screen. |
| `32px`     | `Major` | For large spacing, such as between large blocks of content. |
| `40px`     | `Large` | For larger spacing, used in low-density layouts. |
| `48px`     | `Page` | For spacing surrounding the main page content. |

### 7.2 Control Heights
Standard control heights must be defined to ensure consistency and ease of interaction:
- **Compact:** `32px` (for low-priority elements or in tight spaces).
- **Standard:** `36–40px` (default height for most buttons and input fields).
- **Large:** `44–48px` (for high-priority elements or in touch-based interfaces).
- **Touch:** `48px` or larger (to ensure easy touch tapping according to design guidelines).

### 7.3 Typography
The application must define an integrated typography system:
- **Arabic UI Font:** A clear and readable font that supports all Arabic characters.
- **Arabic Heading Font:** A distinctive font for primary and secondary headings.
- **Latin/English Font:** A font harmonious with the Arabic font for use in mixed content.
- **Numeric Rendering Policy:** Specifies how numbers are displayed (e.g., using Arabic or Hindi numerals).

**Font Hierarchy:**
- `Application Title`
- `Page Title`
- `Section Title`
- `Card Title`
- `Body`
- `Secondary`
- `Caption`
- `Button`
- `Table`

**Typography Rules:**
- **Alignment:** Arabic texts are right-aligned by default.
- **Numeric Values:** Use consistent numeric alignment (usually right or center depending on context).
- **Long Texts:** Must wrap or truncate safely according to component rules, avoiding clipping or text overflow over controls.
- **Buttons:** Button text must never exceed its container.

### 7.4 Colors
Semantic color tokens must be used to ensure consistency and ease of modification:

| Token | Description |
|-------|-------------|
| `Primary` | The primary brand color and main interactions. |
| `Primary Hover` | The `Primary` color on hover. |
| `Primary Pressed` | The `Primary` color when pressed. |
| `Surface` | The color of surfaces containing content (e.g., cards). |
| `Background` | The application background color. |
| `Border` | The color of borders and dividers. |
| `Text Primary` | The primary text color (for reading). |
| `Text Secondary` | The secondary text color (for less important details). |
| `Success` | A color to indicate successful operations. |
| `Warning` | A color to indicate warnings. |
| `Error` | A color to indicate errors. |
| `Info` | A color to indicate general information. |
| `Disabled` | A color for disabled elements. |

**Important Rule:** Color must not be the sole indicator of state. States must be supported with additional icons or text to ensure accessibility.

---

## 8. RTL CONTRACT

RTL layout is structural, not cosmetic. It must be reflected in the application's structure and behavior.

### 8.1 Global Rules
Every screen must:
- Use RTL layout by default.
- Have navigation order natural for Arabic (right to left).
- Place Arabic labels correctly (usually to the right of the input field).
- Right-align Arabic text by default.
- Use correct field placement in forms.
- Use correct dialog action order (e.g., Cancel on the left, Confirm on the right).
- Use correct table structure (column headers right-to-left, cell content based on data type).
- Mirror directional icons where the meaning requires it.

### 8.2 Directional Icons
**Must mirror where appropriate:**
- `Back`
- `Forward`
- `Next`
- `Previous`
- `Expand`
- `Collapse`
- Directional arrows

**Do not mirror:**
- `Search`
- `Print`
- `Settings`
- `Save`
- `Delete`
- `Information`

### 8.3 Mixed Text
Mixed text must be tested and displayed correctly:
- Arabic + English.
- Arabic + Numbers.
- Barcodes.
- Invoice numbers.
- IP addresses.
- URLs.
- Product codes.

### 8.4 RTL Acceptance Checklist
Every component and screen must pass the following checklist to ensure full RTL compliance:
- [ ] Arabic labels are correct.
- [ ] Input fields are correct (alignment, direction).
- [ ] Navigation is correct (menu direction, buttons).
- [ ] Dialog actions are correct (button order).
- [ ] Tables are correct (column order, content alignment).
- [ ] Directional icons are correctly mirrored, and non-directional ones are not.
- [ ] Mixed texts (Arabic/English/Numbers) are displayed correctly.
- [ ] No accidental or unintended LTR layout.

---

## 9. GLOBAL COMPONENT CONTRACT

All shared controls must be centralized and unified in their behavior and design.

### 9.1 Button
Every button must define the following properties:

| Property | Description |
|----------|-------------|
| `ID` | A unique identifier for the button (for tracking and programming). |
| `Arabic Text` | The primary button text in Arabic. |
| `Optional English Text` | An optional English text (for internationalization or mixed contexts). |
| `Icon` | The icon associated with the button (from the approved icon library). |
| `Purpose` | The functional purpose of the button (e.g., `Submit`, `Cancel`, `Add Item`). |
| `Type` | The button type (e.g., `Primary`, `Secondary`, `Destructive`, `Ghost`). |
| `Permission` | The permission required to enable the button. |
| `Size` | The button size (e.g., `Compact`, `Standard`, `Large`). |
| `States` | The visual and behavioral states of the button. |
| `Keyboard Shortcut` | The keyboard shortcut to activate the button (if any). |
| `Success Behavior` | The behavior after a successful operation (e.g., close window, update list). |
| `Failure Behavior` | The behavior after a failed operation (e.g., display error message, enable retry). |

**Button States:**
- `Normal`: The default state.
- `Hover`: When the mouse pointer hovers over it.
- `Pressed`: When clicked.
- `Focused`: When focused using the keyboard.
- `Disabled`: When not available for interaction.
- `Loading`: When executing a time-consuming operation.
- `Success`: To indicate a successful completion (temporary).
- `Error`: To indicate a failed operation (temporary).

### 9.2 Input
Every input field must define the following properties:

| Property | Description |
|----------|-------------|
| `ID` | A unique identifier for the input field. |
| `Label` | The visible label for the field in Arabic. |
| `Placeholder` | A hint text that appears inside the field when it is empty. |
| `Required` | Is the field mandatory? (True/False). |
| `Format` | The expected data format (e.g., `Numeric`, `Text`, `Email`, `Date`, `Barcode`). |
| `Maximum Length` | The maximum number of characters allowed. |
| `Allowed Characters` | The allowed characters (e.g., `[0-9]`, `[A-Za-z]`). |
| `Validation` | Data validation rules (e.g., `MinLength`, `Regex`, `Range`). |
| `Error Message` | The error message displayed when validation fails. |
| `Alignment` | Text alignment within the field (e.g., `Right` for Arabic, `Left` for English, `Center` for numbers). |
| `Keyboard Behavior` | Keyboard behavior (e.g., `Numeric Only`, `AlphaNumeric`). |

### 9.3 Dialog
Every dialog must define the following properties:

| Property | Description |
|----------|-------------|
| `ID` | A unique identifier for the dialog. |
| `Title` | The title of the dialog in Arabic. |
| `Purpose` | The purpose of the dialog (e.g., `Confirmation`, `Data Entry`, `Information`). |
| `Message` | The main text or question displayed in the dialog. |
| `Primary Action` | The primary action (usually the confirm button, placed on the right in RTL). |
| `Secondary Action` | The secondary action (usually the cancel button, placed on the left in RTL). |
| `Default Action` | The action activated when `Enter` is pressed. |
| `Escape Behavior` | The behavior when `Escape` is pressed (e.g., close dialog, cancel operation). |
| `Outside Click Behavior` | The behavior when clicking outside the dialog (e.g., close dialog, do nothing). |
| `Destructive Behavior` | Does the dialog contain a destructive action? (requires additional confirmation). |
| `Loading` | The loading state within the dialog. |
| `Success` | The success state within the dialog. |
| `Error` | The error state within the dialog. |

### 9.4 Table
Every table must define the following properties:

| Property | Description |
|----------|-------------|
| `Columns` | Column definitions (name, data type, width, alignment). |
| `Order` | The default column order. |
| `Width` | Column width policy (fixed, flexible, percentage). |
| `Alignment` | Cell content alignment (right for Arabic, left for English, center for numbers). |
| `Sorting` | Does the table support sorting? (by which column, ascending/descending). |
| `Filtering` | Does the table support filtering? (by which column, filter type). |
| `Search` | Does the table support search? (general text search or in specific columns). |
| `Selection` | Selection type (single row, multiple rows, none). |
| `Actions` | Actions available on rows or the table as a whole (e.g., `Edit`, `Delete`, `View`). |
| `Loading State` | The loading state (when fetching data). |
| `Empty State` | The empty state (when there is no data to display). |
| `Error State` | The error state (when fetching data fails). |
| `Pagination` | Does the table support pagination? (page size, navigation buttons). |
| `Keyboard Navigation` | Does the table support keyboard navigation? (arrows, Tab). |

---

## 10. GLOBAL UI STATES

Every data-driven screen must support the following states clearly and documented:

### 10.1 Normal State
- **Description:** Data is available and displayed correctly.
- **Behavior:** All controls are interactive according to permissions.

### 10.2 Loading State
- **Description:** Data is being fetched or a time-consuming operation is being executed.
- **Behavior:** Display a clear progress indicator (Spinner, Progress Bar) with disabling controls that might cause operation conflicts. The indicator must be visible and understandable to the user.

### 10.3 Empty State
- **Description:** There is no data to display on the screen or component.
- **Behavior:** Display a clear message in Arabic explaining why it is empty and what the user can do next. Example:
  ```text
  There are no products to display currently.
  You can add a new product by clicking the "Add Product" button.
  ```
  The screen should include a suggested action (e.g., an "Add New" button) if it makes sense.

### 10.4 Error State
- **Description:** An error occurred while fetching data or executing an operation.
- **Behavior:** Display a clear and understandable error message to the user in Arabic, avoiding technical details. The message should include guidance on how to proceed (e.g., "Failed to load data. Please try again.").
  ```text
  Failed to load the requested data.
  Please check the network connection and try again.
  If the problem persists, please contact technical support.
  ```
  **Engineering Note:** Full technical error details (such as Stack Trace) should only be written to log files, not shown to the user.

### 10.5 Disabled State
- **Description:** An action or control is not currently available for interaction.
- **Behavior:** The element must be visually disabled (gray color, reduced opacity) and non-clickable. On hover, a tooltip may appear explaining the reason for disabling if necessary.

### 10.6 Permission Denied State
- **Description:** The current user does not have the necessary permissions to access a feature or perform an action.
- **Behavior:** Display a clear message to the user stating the lack of permission. Example:
  ```text
  You do not have permission to perform this operation.
  Please contact the system administrator for assistance.
  ```
  Unauthorized features should be either completely hidden or disabled with an explanatory message.

---

## 11. RESOURCE SYSTEM

### 11.1 Icon Library
A single, coherent icon family must be used across the entire application.

**Required Categories:**
- `Add`, `Edit`, `Delete`, `Save`, `Cancel`
- `Search`, `Filter`, `Print`, `Refresh`
- `Settings`, `Users`, `Products`, `Inventory`
- `Reports`, `Sales`, `Returns`, `Payments`
- `Tables`, `Kitchen`
- `Backup`, `Restore`, `Security`
- `Warning`, `Error`, `Success`, `Info`
- `Lock`, `Unlock`, `Logout`, `Menu`
- `Expand`, `Collapse`, `Chevron (directional arrows)`

### 11.2 Sounds
Sounds are optional and configurable by the user.

**Events that may require sounds (Optional, configurable events):**
- `Login Success`
- `Login Failure`
- `Validation Error`
- `Warning`
- `Product Added`
- `Payment Success`
- `Receipt Printed`
- `Kitchen Order`
- `Inventory Alert`
- `System Error`

**Sound Rules:**
- **Short:** Sounds must be short and non-intrusive.
- **Non-intrusive:** They should not cause annoyance to users during long work shifts.
- **Disableable:** There must be an option to disable all sounds or specific sounds.
- **Not the sole feedback:** Sounds must not be the only indicator of an event; they must be accompanied by visual or textual feedback.

---

## 12. APPLICATION SHELL

The application shell is the main frame that contains all screens and components, providing navigation and access to essential information.

### 12.1 Required Elements
- **RTL Navigation:** A main navigation menu following the RTL direction.
- **Current User:** Display the current user's name and picture (if any).
- **Current Shift:** Information about the active shift (shift number, start time).
- **Current Register:** Information about the active POS/cashier station.
- **Date and Time:** Display the current date and time.
- **Notifications:** An area to display important notifications (e.g., printer failure, inventory alerts).
- **Quick Actions:** Buttons or shortcuts for frequent actions (e.g., `Lock Screen`, `Logout`).
- **Lock:** A button to lock the screen requiring password/PIN re-entry.
- **Logout:** A button to log out of the system.

### 12.2 Shell Rules
- **Visual Cleanliness:** The application shell must remain visually clean and uncluttered.
- **Permission-Aware Navigation:** Navigation elements should only be visible or interactive if the user has the necessary permissions.
- **Clarity:** The user must immediately understand the current module or section they are in.

---

## 13. AUTHENTICATION

### 13.1 Login Screen (AUTH-001 — LOGIN)
- **Purpose:** Authenticate local system users.
- **Layout:** The login screen must be balanced and uncluttered, with a centralized authentication panel.

**Required Elements:**
1. Application logo.
2. Application name.
3. Business name.
4. Username Selector/Input field.
5. Password/PIN Field.
6. Show/Hide password button.
7. Login Button.
8. Clear/Reset Action if appropriate.
9. Database Status (to indicate whether database connection is available).
10. Application Version.

**Layout Rules:**
- The main authentication panel is centered.
- Arabic labels are right-aligned.
- The primary login action (button) is visually dominant.
- Error messages appear near the relevant input field or in a dedicated status area.
- The screen must not be visually overloaded.

**Login Screen States:**
- `Initial`: Before any input.
- `User Selected`: If a user selector is present.
- `Password Entry`: After entering the username.
- `Loading`: During the authentication attempt.
- `Invalid Credentials`: When authentication fails.
- `Locked User`: If the user account is locked after failed attempts.
- `Disabled User`: If the user account is disabled.
- `Database Unavailable`: When unable to connect to the database.
- `Success`: When authentication succeeds.

**Security:**
- No plaintext passwords.
- Failed login attempts are logged.
- Permissions loaded on successful login.
- No Internet required.
- No sensitive information displayed in error messages.

**Acceptance Checklist:**
- [ ] Correct RTL layout.
- [ ] No text clipping.
- [ ] Keyboard login works.
- [ ] `Enter` key performs login.
- [ ] `Escape` behavior defined (e.g., close window).
- [ ] Error state works correctly.
- [ ] Loading state works correctly.
- [ ] Database failure state works correctly.
- [ ] Permissions load correctly.

---

## 14. DASHBOARD

### 14.1 Operational Dashboard (DASH-001 — OPERATIONAL DASHBOARD)
The dashboard must display only useful operational information, not decorative content.

**Possible Widgets:**
- Current Sales.
- Active Shift.
- Low Stock Alerts.
- Pending Kitchen Orders.
- Printer Alerts.
- Recent Activity.

**Widget Rules:**
- Each widget must have a clear purpose.
- Each widget must support loading, empty, and error states.
- Widgets must respect user permissions.
- **Prohibited:** Filling the dashboard with decorative charts that are not operationally useful.

---

## 15. MAIN POS

### 15.1 Sales Terminal Screen (POS-001 — SALES TERMINAL)
This is the highest priority screen in the system.

### 15.2 Primary Layout

```text
┌─────────────────────────────────────────────────────┐
│ Header: User | Shift | Register | Status            │
├───────────────────────────┬─────────────────────────┤
│                           │                         │
│ Search / Barcode          │ Current Transaction     │
│ Categories                │ Items                   │
│ Product Grid              │ Quantity / Price        │
│                           │ Discounts / Taxes       │
│                           │ Totals                  │
│                           │ Actions                 │
├───────────────────────────┴─────────────────────────┤
│ Quick Actions / Payment / Status                    │
└─────────────────────────────────────────────────────┘
```

**Layout Description:**
- **Header:** Contains essential information such as current user, shift, register, and system status.
- **Right Section (RTL):** Displays current invoice details (sales items, quantity, price, discounts, taxes, totals, and invoice-related actions).
- **Left Section (RTL):** Contains product search functions (by name, barcode), categories, and product grid display.
- **Bottom Section:** Contains quick actions, payment buttons, and general system status.

### 15.3 Product Search
- **Support:** Search by name, SKU code, barcode, or alternate code.
- **Performance:** Search must be very fast and instantaneous.
- **Barcode Input:** Must support fast barcode scanning without delay.

### 15.4 Categories
- **Requirements:** Provide an active state for the selected category, clear category hierarchy, fast switching between categories, and correct RTL order.

### 15.5 Product Card
May contain: product name, image, price, unit, availability, and shortcut.

**Rules:**
- The product name must fit within the allocated space.
- Long names must be safely truncated without distortion.
- The price must remain clearly visible.
- The unavailability state must be clear.
- Card sizes must remain consistent.

### 15.6 Transaction Lines
Each invoice line displays:
- Product.
- Quantity.
- Unit price.
- Discount.
- Tax.
- Line total.
- Modification action.
- Removal action.

### 15.7 Actions
**Required Actions:**
- Add item, change quantity, remove item, modify item.
- Apply discount, add customer, assign table, add notes.
- Hold invoice, Retrieve invoice.
- Return, Payment, Print, Cancel.

### 15.8 Primary Workflow
```text
Scan
↓
Add
↓
Pay
↓
Print
```
This workflow requires minimal interaction to ensure speed and efficiency.

### 15.9 POS Screen States
- `Empty Sale`
- `Active Sale`
- `Loading Product`
- `Product Not Found`
- `Out of Stock`
- `Discount Dialog`
- `Hold Sale`
- `Retrieve Sale`
- `Payment`
- `Payment Success`
- `Payment Failure`
- `Printer Failure`
- `Permission Denied`

---

## 16. PAYMENT

### 16.1 Payment Dialog (PAY-001 — PAYMENT DIALOG)
The payment dialog must display:
- Total Due.
- Selected Method.
- Amount Received.
- Change.
- Remaining Balance.
- Confirmation.

**Payment Methods:**
- Cash.
- Card.
- Multiple Methods.
- Partial Payment (where enabled).

**Payment States:**
- `Ready`
- `Invalid Amount`
- `Processing`
- `Success`
- `Failure`

**Important Rule:** A payment failure must not silently close the sale. The user must be clearly informed and provided with options to proceed.

---

## 17. RESTAURANT OPERATIONS

### 17.1 Order Type (REST-001 — ORDER TYPE)
**Support:**
- `Dine-in`
- `Takeaway`
- `Delivery` (where enabled)

The order type must be clear and easily changeable before finalizing the invoice, where business rules permit.

### 17.2 Modifiers (REST-002 — MODIFIERS)
**Support:**
- `Add-ons`
- `Removals`
- `Sizes`
- `Cooking Instructions`
- `Notes`

Modifiers must be visually distinct from the base product for easy differentiation.

---

## 18. TABLE MANAGEMENT

### 18.1 Table Map (TABLE-001 — TABLE MAP)
**Table States:**
- `Available`
- `Occupied`
- `Preparing`
- `Ready`
- `Waiting for Payment`
- `Reserved`

**Actions:**
- Open table.
- Add order.
- Transfer order.
- Merge tables.
- Split bill.
- Close table.

**Important Rule:** Table state must not rely on color alone. Each table must provide a clear visual state with textual/iconic support.

---

## 19. KITCHEN

### 19.1 Kitchen Orders (KITCHEN-001 — KITCHEN ORDERS)
Each kitchen order ticket includes:
- Order number.
- Time.
- Table/order type.
- Items.
- Modifiers.
- Notes.
- Priority.

**Routing:**
Orders must be routed to the correct kitchen stations based on product type (e.g., Burger → Grill, Pizza → Pizza Section, Drink → Beverage Section).

**Printing:** Kitchen printing is independent of receipt printing.

**Printer Failure:** Kitchen printer failure must be:
- Clearly visible.
- Logged in the system logs.
- Support retry.

---

## 20. SUPERMARKET MODE

**Support:**
- Fast barcode scanning.
- Weighted products (scale integration).
- Multiple units of measure.
- Multiple selling units.
- Promotions (where enabled).
- Retail Prices.
- Expiry Dates (where required).
- Batches/Lots (where required).

**Performance:** The checkout process must remain fast and efficient.

---

## 21. PRODUCT MANAGEMENT

### 21.1 Product List (PROD-001 — PRODUCT LIST)
**Requirements:**
- Search.
- Filters (by category, status, stock status, barcode, product type).
- Add, Edit, View, Archive products.

**Suggested Table Columns:**

| Column | Description |
|--------|-------------|
| `Name` | The product name in Arabic. |
| `Barcode` | The product barcode. |
| `Category` | The category the product belongs to. |
| `Type` | The product type (e.g., `Restaurant Item`, `Supermarket Item`). |
| `Price` | The current selling price. |
| `Stock` | The available quantity in stock. |
| `Status` | The product status (active, inactive, archived). |
| `Actions` | Action buttons (edit, delete, view). |

**Column Rules:** All columns must have clear and consistent alignment.

### 21.2 Product Card (PROD-002 — PRODUCT CARD)
**Fields:**

| Field | Description |
|-------|-------------|
| `Arabic Name` | The product name in Arabic (Mandatory). |
| `English Name` | The product name in English (Optional). |
| `SKU` | The Stock Keeping Unit identifier. |
| `Barcode` | The barcode. |
| `Category` | The product category (dropdown). |
| `Product Type` | The product type (e.g., `Standard`, `Weighted`, `Modifier`). |
| `Unit` | The base unit of measure (e.g., `Kg`, `Piece`, `Liter`). |
| `Cost` | The product cost (for profitability calculations). |
| `Selling Price` | The selling price. |
| `Tax` | The applicable tax percentage. |
| `Minimum Stock` | The minimum stock level (for low stock alerts). |
| `Supplier` | The primary product supplier. |
| `Image` | The product image. |
| `Active Status` | The activation status (active/inactive). |
| `Kitchen Station` | The assigned kitchen station (for restaurants). |
| `Recipe` | The recipe associated with the product (for restaurants). |
| `Restaurant Configuration` | Restaurant-specific settings (e.g., `Allow Modifiers`). |

**Field Rules:**
- **Validation:** Each field must have data validation rules.
- **Error Message:** A clear error message when validation fails.
- **Permission:** The permission required to modify the field.
- **Save Behavior:** How changes are saved and their effect on the system.

---

## 22. INVENTORY

Inventory is based on a movement system.

**Movement Types:**
- `Purchase`: Inventory increase due to purchase.
- `Sale`: Inventory decrease due to sale.
- `Return`: Inventory increase due to customer return.
- `Waste`: Inventory decrease due to wastage.
- `Adjustment`: Manual inventory adjustment.
- `Transfer`: Inventory transfer between locations (if supported).
- `Stock Count`: Inventory update based on physical count.

**Each movement records:**
- `Product` (the affected product).
- `Quantity`
- `Before Quantity`
- `After Quantity`
- `Reason` (the reason behind the movement).
- `User` (the user who performed the movement).
- `Timestamp`
- `Reference` (reference for the movement, e.g., purchase or sale invoice number).

**Important Rule:** Inventory history must not be silently overwritten. All movements must be permanently recorded and auditable.

---

## 23. RECIPES

Recipes are used to define the ingredients required to produce a specific product (especially in restaurants).

**Example Recipe:**
```text
Burger
├── Bun: 1 piece
├── Meat: 150 grams
├── Cheese: 1 slice
└── Sauce: 20 grams
```

**Recipe Rules:**
- **Audit:** All changes to recipes must be audited (who made the change, when, and what was changed).
- **Ingredient Consumption:** Ingredient consumption must be deterministic and accurate to ensure inventory accuracy and cost calculation.

---

## 24. PURCHASES AND SUPPLIERS

**Support:**
- Supplier Card: Supplier information, contact details.
- Purchase Order: Create and track purchase orders.
- Purchase Invoice: Record purchase invoices.
- Received Quantities: Match received quantities with purchase orders.
- Costs: Record product costs.
- Inventory Increase: Automatically update inventory upon receiving purchases.
- Supplier Returns: Manage product returns to suppliers.
- Balances: Track supplier balances (where enabled).

---

## 25. CUSTOMERS

Where enabled, the following must be supported:
- Customer Profile: Name, contact information.
- Order History: Display the customer's purchase history.
- Account Balance: Track customer balance (where enabled).
- Notes: Add customer-specific notes.
- Loyalty Configuration: Configure loyalty programs (if adopted).

**Security:** Customer information must be strictly access-controlled by permissions.

---

## 26. RETURNS AND CANCELLATIONS

Returns must reference the original sales.

**Requirements:**
- Original Invoice: Link the return to the original invoice.
- Item: The product being returned.
- Quantity: The returned quantity.
- Amount: The refund amount.
- Reason: The reason for the return.
- User: The user who performed the return.
- Timestamp: The date and time of the return.

**Cancellation:** Requires special permission.

**Important Rule:** Financial records must never be physically deleted. Returns and cancellations must be recorded as reversible movements or auditable adjustments.

---

## 27. SHIFTS AND CASH REGISTER

### 27.1 Open Shift (SHIFT-001 — OPEN)
- Opening Cash: The cash amount at the start of the shift.
- User: The user who opened the shift.
- Timestamp: The date and time the shift was opened.

### 27.2 Close Shift (SHIFT-002 — CLOSE)
**Must calculate:**
- Expected Cash.
- Actual Cash.
- Variance.
- Total Sales.
- Total Returns.
- Expenses.
- Withdrawals.
- Deposits.
- Payment Totals by method.

**Audit:** The shift closing process must be fully auditable.

---

## 28. USERS AND PERMISSIONS

Permissions must be granular.

**Examples of Permissions:**
- `Sell`
- `Apply Discount`
- `Change Price`
- `Cancel Item`
- `Cancel Invoice`
- `Return Item`
- `Return Invoice`
- `Open Cash Drawer`
- `View Reports`
- `Edit Products`
- `Edit Prices`
- `Adjust Inventory`
- `Archive`
- `Backup`
- `Restore`
- `Manage Users`
- `Change Settings`

**Important Rule:** Sensitive actions may require Manager Approval or additional permission.

---

## 29. REPORTING

### 29.1 Sales Reports
- Daily, Weekly, Monthly.
- By user, by product, by category, by payment method.

### 29.2 Inventory Reports
- Current Stock.
- Low Stock.
- Inventory Movements.
- Inventory Adjustments.
- Wastage.

### 29.3 Profitability Reports
- Sales, Cost, Gross Profit.

### 29.4 Cash Reports
- Shifts, Expected Cash, Actual Cash, Variance.

**Every report supports:**
- Filters.
- Date Range.
- Loading, empty, and error states.
- Print (where appropriate).
- Export (if adopted).

---

## 30. PRINTERS

### 30.1 Printer Management (DEV-001 — PRINTER MANAGEMENT)
**Fields:**

| Field | Description |
|-------|-------------|
| `Name` | Printer name (for identification). |
| `Type` | Printer type (e.g., `Thermal`, `Dot Matrix`). |
| `Connection` | Connection method (e.g., `USB`, `Network`, `Serial`). |
| `IP / Port` | IP address or port (for network/serial printers). |
| `Assigned Station` | The assigned station (e.g., `Kitchen`, `Beverage`, `Receipt`). |
| `Paper Width` | Paper width (e.g., `80mm`, `58mm`). |
| `Encoding` | Character encoding (e.g., `Arabic`, `UTF-8`). |
| `Active` | Activation status (active/inactive). |

**Printer Roles:**
- `Receipt`
- `Kitchen`
- `Beverage`
- `Department`

**Actions:**
- Test.
- Retry.
- Status.
- Edit.
- Disable.

**Important Rule:** Printer failure must not crash the POS system. Errors must be handled gracefully.

---

## 31. HARDWARE

**Potential Integrations:**
- Barcode Scanner.
- Touchscreen.
- Cash Drawer.
- Receipt Printer.
- Kitchen Printer.
- Customer Display.
- Scale.

**Engineering Rule:** Each hardware integration must be isolated behind an interface to ensure scalability and ease of replacement.

**Failure Recovery:** Hardware failures must be recoverable without data loss or system crash.

---

## 32. SETTINGS

**Settings Categories:**

| Category | Description |
|----------|-------------|
| `General` | General system settings. |
| `Appearance` | Appearance settings (themes, colors). |
| `Language & Region` | Language and region (including date/time format). |
| `Currency` | Currency settings (symbol, decimal precision). |
| `Users & Permissions` | User and permission management. |
| `Printers` | Printer management. |
| `Kitchen Stations` | Kitchen station management. |
| `Cash Register` | Cash register settings. |
| `Taxes` | Tax settings. |
| `Discounts` | Discount settings. |
| `Inventory` | Inventory settings. |
| `Products` | Product settings. |
| `Restaurant` | Restaurant-specific settings. |
| `Supermarket` | Supermarket-specific settings. |
| `Sounds` | Sound settings. |
| `Notifications` | Notification settings. |
| `Backup` | Backup settings. |
| `Database` | Database settings. |
| `Security` | Security settings. |
| `Maintenance` | Maintenance tasks. |

**Each setting defines:**
- `Type` (setting type: text, number, boolean, dropdown).
- `Default` (default value).
- `Allowed Range` (allowed range for numeric values).
- `Permission` (permission required to modify the setting).
- `Validation` (value validation rules).
- `Impact` (the impact of changing the setting on the system).
- `Restart Requirement` (does changing the setting require restarting the application?).

### 32.1 Appearance
- Appearance options must include only those approved by the design system.
- **Prohibited:** Allowing uncontrolled customization that breaks visual consistency.

### 32.2 Sounds
- Master enabled/disabled.
- Volume.
- Event-specific settings.

### 32.3 Printers
- Assignments.
- Test.
- Status.
- Paper Width.
- Connection.

### 32.4 Security
- Password Policy.
- Failed Login Policy.
- Session Lock.
- Permissions.

---

## 33. BACKUP AND RECOVERY

**Requirements:**
- Manual Backup.
- Automatic Backup.
- Retention Policy.
- Backup Verification.
- Restore.
- Backup History.

**The system must clearly display:**
- `Backup Success`
- `Backup Failure`
- `Restore Warning`
- `Restore Success`
- `Restore Failure`

**Important Rule:** Restoring requires strong confirmation and special permission, given its critical impact on data.

---

## 34. DATABASE INTEGRITY

Critical operations use Transactions to ensure data integrity.

**Example Sale Transaction:**
```sql
BEGIN TRANSACTION;

-- 1. Create the sales invoice
INSERT INTO Sales (SaleID, ShiftID, UserID, SubTotal, TaxAmount, DiscountAmount, TotalAmount, Status, CreatedAt)
VALUES (@SaleID, @ShiftID, @UserID, @SubTotal, @TaxAmount, @DiscountAmount, @TotalAmount, 'Completed', GETDATE());

-- 2. Create the sales items
INSERT INTO SaleItems (SaleItemID, SaleID, ProductID, Quantity, UnitPrice, Discount, Tax, LineTotal)
VALUES (@SaleItemID1, @SaleID, @ProductID1, @Quantity1, @UnitPrice1, @Discount1, @Tax1, @LineTotal1);
INSERT INTO SaleItems (SaleItemID, SaleID, ProductID, Quantity, UnitPrice, Discount, Tax, LineTotal)
VALUES (@SaleItemID2, @SaleID, @ProductID2, @Quantity2, @UnitPrice2, @Discount2, @Tax2, @LineTotal2);

-- 3. Create the payments
INSERT INTO Payments (PaymentID, SaleID, Method, Amount, Timestamp)
VALUES (@PaymentID, @SaleID, @Method, @Amount, GETDATE());

-- 4. Update inventory
UPDATE Inventory
SET Quantity = Quantity - @Quantity1
WHERE ProductID = @ProductID1;

UPDATE Inventory
SET Quantity = Quantity - @Quantity2
WHERE ProductID = @ProductID2;

-- 5. Create audit log
INSERT INTO AuditLog (UserID, Timestamp, Action, Entity, EntityID, BeforeValue, AfterValue)
VALUES (@UserID, GETDATE(), 'Sale Completed', 'Sale', @SaleID, NULL, @TotalAmount);

COMMIT TRANSACTION;
```

**In case of failure:**
```sql
ROLLBACK TRANSACTION;
```

**Important Rule:** No financial operation may remain partially or incompletely committed silently in the database. It must either be fully committed or fully rolled back.

---

## 35. ERROR HANDLING

User-facing messages must be in Arabic, clear, and understandable.

**Example of a Bad Error Message:**
```text
SQL Exception 547: Foreign key constraint violation.
```

**Example of a Good Error Message:**
```text
The operation could not be saved due to invalid data.
Please check your inputs and try again.
```

**Error Handling Rules:**
- **Technical Details:** Technical error details belong only in log files.
- **Unexpected Exceptions:** Must be logged, system stability maintained as much as possible, safe feedback shown to the user, and no sensitive information disclosed.

---

## 36. AUDIT LOG

All important actions in the system must be audited.

**Actions to be Audited:**
- Login and Logout.
- Price Change.
- Discount Application.
- Returns and Cancellations.
- Inventory Adjustments.
- Product Archiving.
- Permission Changes.
- Settings Changes.
- Backup and Restore.

**Each Audit Record Includes:**

| Field | Description |
|-------|-------------|
| `UserID` | The ID of the user who performed the action. |
| `Timestamp` | The date and time of the action. |
| `Action` | The type of action performed (e.g., `PriceChange`, `InventoryAdjustment`). |
| `Entity` | The affected entity (e.g., `Product`, `Sale`, `User`). |
| `EntityID` | The ID of the affected entity. |
| `BeforeValue` | The value before the change (for important fields). |
| `AfterValue` | The value after the change (for important fields). |
| `Reason` | The reason for the action (if available). |

---

## 37. PERFORMANCE

The system must be high-performance and responsive.

**Requirements:**
- **Startup Speed:** The application must start quickly.
- **Responsiveness:** It must remain responsive to inputs at all times.
- **Avoid UI Blocking:** Avoid unnecessarily blocking the UI.
- **Indexed Searches:** Use indexes to improve database search performance.
- **Data Loading:** Avoid loading huge datasets at once.
- **Pagination:** Use pagination where appropriate to handle large amounts of data.
- **Queries:** Avoid repeated and unnecessary database queries.
- **Memory Leaks:** Avoid memory leaks.
- **Animations:** Avoid unnecessary animations or those that slow down operations.

**Important Rule:** Animations must never delay operations.

---

## 38. ACCESSIBILITY AND OPERATIONAL USABILITY

**Support:**
- **Keyboard Navigation:** Full support for keyboard navigation and interaction.
- **Visible Focus:** A clear visual indicator for the focused element.
- **Readable Text:** Use text sizes and fonts that ensure readability.
- **Adequate Contrast:** To ensure clarity of elements and text.
- **Touch-Friendly Targets:** Controls large enough for easy touch tapping.
- **Clear Status Messages:** Clear notifications and feedback for the user.

**Important Rule:** The system must be comfortably and effectively usable during long work shifts.

---

## 39. EXACT SCREEN SPECIFICATION CONTRACT

Every screen must have a dedicated specification containing the following elements:

| Element | Detailed Description |
|---------|---------------------|
| `SCREEN ID` | A unique identifier for the screen (e.g., `POS-001`, `AUTH-001`). |
| `SCREEN NAME` | The clear screen name in Arabic. |
| `PURPOSE` | The primary functional purpose of the screen. |
| `USER ROLES` | User roles allowed to access this screen. |
| `PERMISSIONS` | Specific permissions required to access the screen and perform actions within it. |
| `ENTRY POINT` | How to access this screen (e.g., from navigation menu, after login, from another screen). |
| `EXIT ACTIONS` | Actions the user can take to leave the screen (e.g., `Close`, `Cancel`, `Save & Exit`). |
| `PRIMARY USER TASK` | The main task the user performs on this screen. |
| `SECONDARY TASKS` | Secondary or sub-tasks that can be performed. |
| `RTL REQUIREMENTS` | Specific RTL requirements for this screen (alignment, element order, mirrored icons). |
| `PAGE STRUCTURE` | Description of the overall page structure (sections, main areas). |
| `EXACT SECTIONS` | Identification of the exact sub-sections within the screen. |
| `EXACT COMPONENTS` | A list of all components used in the screen (buttons, input fields, tables). |
| `EXACT FIELDS` | A list of all input fields with their specifications (type, validation, label). |
| `EXACT BUTTONS` | A list of all buttons with their specifications (text, icon, behavior, permission). |
| `EXACT TABLES` | A list of all tables with their specifications (columns, sorting, filtering, states). |
| `EXACT DIALOGS` | A list of all dialogs that can appear from this screen. |
| `VALIDATION` | Data validation rules for all inputs. |
| `BUSINESS RULES` | Specific business rules that apply to this screen. |
| `LOADING STATE` | How to display the loading state. |
| `EMPTY STATE` | How to display the empty state. |
| `ERROR STATE` | How to display the error state. |
| `DISABLED STATE` | How to display disabled elements. |
| `PERMISSION STATE` | How to handle missing permissions. |
| `KEYBOARD SHORTCUTS` | Supported keyboard shortcuts. |
| `ICON REQUIREMENTS` | Specific icons required for this screen. |
| `SOUND REQUIREMENTS` | Specific sounds required for this screen. |
| `DATA INPUTS` | Data the screen receives. |
| `DATA OUTPUTS` | Data the screen produces. |
| `AUDIT REQUIREMENTS` | Audit requirements for actions performed on this screen. |
| `LOGGING REQUIREMENTS` | Logging requirements for events and errors. |
| `ACCEPTANCE CRITERIA` | Specific acceptance criteria for testing this screen. |

**Important Rule:** No screen can be considered fully specified without these elements. These specifications must be an integral part of the development process.

---

## 40. AI CODING AGENT PROTOCOL

Before coding any feature, the AI Agent (or human developer) must follow these steps precisely:

1. **Read the Document:** Read this entire document thoroughly and understand it deeply.
2. **Identify the Module:** Identify the relevant software module (e.g., `POS.Application`, `POS.Desktop`).
3. **Identify the Screen ID:** Identify the specific screen ID (e.g., `POS-001`).
4. **Identify Business Rules:** Extract all relevant business rules from the `Domain` section.
5. **Identify Permissions:** Identify the permissions required to access or perform actions on the feature.
6. **Identify Data Requirements:** Understand the data model and affected entities.
7. **Identify UI States:** Identify all UI states (loading, empty, error, disabled, permission).
8. **Identify RTL Requirements:** Review the specific RTL requirements for the screen and components.
9. **Identify Resources:** Identify the icons, sounds, and design tokens required.
10. **Inspect Existing Architecture:** Review existing architecture to avoid duplication and ensure consistency.
11. **Avoid Duplication:** Do not duplicate existing implementations.
12. **Implement Domain Logic:** Write or modify code in the `POS.Domain` layer first.
13. **Implement Application Service:** Write or modify code in the `POS.Application` layer.
14. **Implement Data Access:** Write or modify code in the `POS.Infrastructure` layer (repositories, database context).
15. **Implement UI:** Write or modify code in the `POS.Desktop` layer.
16. **Implement Validation:** Add all input validation rules.
17. **Implement Permissions:** Apply permission checking mechanisms.
18. **Implement Logging:** Add logging points for important events and errors.
19. **Implement Audit:** Add audit logs for sensitive actions.
20. **Implement Tests:** Write unit, integration, and UI tests.
21. **Build the Solution:** Ensure a successful build.
22. **Visual Screen Review:** Visually review the screen to ensure UI/UX and RTL compliance.
23. **Fix Defects:** Address any discovered errors or defects.
24. **Report Completion:** Provide a detailed implementation status report.

**The AI Agent/Developer must report:**

| Status | Description |
|--------|-------------|
| `Implemented` | The feature is fully implemented. |
| `Not Implemented` | The feature was not implemented (with reason). |
| `Blocked` | The feature is blocked (with reason and obstacles). |
| `Assumptions` | Any assumptions made during implementation. |
| `Tests` | Summary of tests performed and their results. |
| `Build Result` | The result of the build process (success/failure). |
| `Known Issues` | Any known issues or remaining defects. |

---

## 41. UI QUALITY GATE

A screen is considered **FAILED** if any of the following conditions are met:
- Arabic text is incorrectly left-aligned.
- Incorrect RTL order (e.g., action button order in dialogs).
- Text is clipped or overflows controls.
- Controls overlap.
- Spacing is inconsistent.
- Colors are random or do not follow design tokens.
- Icons are missing or inconsistent.
- Loading state is missing.
- Empty state is missing.
- Error state is missing.
- Permission state is missing.
- Buttons are ambiguous or unclear in purpose.
- Tables are unreadable or unnavigable.
- Dialogs have incomplete states or behaviors.
- Keyboard navigation fails.
- Destructive actions are unclear or do not require confirmation.
- Visual hierarchy is confusing.

---

## 42. SCREEN COMPLETION CHECKLIST

Every screen must pass the following checklist before being considered complete:
- [ ] `Screen ID` is specified.
- [ ] `Purpose` is specified.
- [ ] `User Roles` are specified.
- [ ] `Permissions` are specified.
- [ ] `Entry Point` is specified.
- [ ] `Exit Behavior` is specified.
- [ ] `RTL` is verified.
- [ ] `Layout` is verified.
- [ ] `Typography` is verified.
- [ ] `Spacing` is verified.
- [ ] `Colors` use design tokens.
- [ ] `Icons` use the approved system.
- [ ] `Buttons` are documented.
- [ ] `Fields` are documented.
- [ ] `Validation` is implemented.
- [ ] `Loading state` is implemented.
- [ ] `Empty state` is implemented.
- [ ] `Error state` is implemented.
- [ ] `Disabled state` is implemented.
- [ ] `Permission state` is implemented.
- [ ] `Keyboard` is tested.
- [ ] `Database operations` are tested.
- [ ] `Audit` is implemented.
- [ ] `Logging` is implemented.
- [ ] `Sounds` are implemented where required.
- [ ] `Text overflow` is verified.
- [ ] `Build` succeeds.
- [ ] `Tests` pass.
- [ ] `Acceptance criteria` pass.

---

## 43. TESTING STRATEGY

The testing strategy must cover all aspects of the system to ensure quality and reliability.

### 43.1 Sales Tests
- **Unit Tests:**
  - Tax and discount calculations with 3 decimal place precision.
  - Adding/removing products from the invoice.
  - Updating quantities and prices.
- **Integration Tests:**
  - Complete sale process (add products, pay, print) with inventory update and audit log.
  - Hold and retrieve invoices.
  - Return and cancellation operations.
- **UI Tests:**
  - Adding single item, multiple items.
  - Barcode search and text search.
  - Applying discounts and taxes.
  - Payment process and change calculation.
  - Return and cancellation process.

### 43.2 Inventory Tests
- **Unit Tests:**
  - Inventory update on purchase, sale, wastage, adjustment.
  - Ingredient consumption from recipes.
- **Integration Tests:**
  - Complete product lifecycle (purchase, sale, return, wastage) with inventory tracking.
  - Stock counting and inventory adjustment operations.

### 43.3 Printing Tests
- **Unit Tests:**
  - Receipt/kitchen ticket content generation.
- **Integration Tests:**
  - Connected and working printer.
  - Disconnected (Offline) printer and system behavior.
  - Network interruption during printing.
  - Automatic and manual print retry.
  - System behavior with incorrect printer configuration.

### 43.4 Database Tests
- **Unit Tests:**
  - Correctness of queries and stored procedures.
- **Integration Tests:**
  - Sudden system shutdown during a critical transaction and verification of rollback.
  - Transaction failure and data integrity verification.
  - Database restoration from a backup.
  - Performance of large queries.

### 43.5 Security Tests
- **Unit Tests:**
  - Password policies.
  - Encryption mechanisms.
- **Integration Tests:**
  - Invalid Login.
  - Disabled User.
  - Locked User.
  - Insufficient Permission to access a feature or action.
  - Action requiring Manager Approval.
  - Attempted unauthorized sensitive action.

---

## 44. RELEASE QUALITY GATE

The system is **not production-ready** until all the following conditions are met:

- [ ] Core sales operations work fully without Internet connectivity.
- [ ] Financial and inventory data integrity is fully verified.
- [ ] Financial calculations (taxes, discounts, totals) are verified with 3 decimal place precision.
- [ ] Backup works reliably.
- [ ] Restore works reliably.
- [ ] Receipt printing works reliably.
- [ ] Kitchen printing works reliably.
- [ ] RTL layout is fully visually verified.
- [ ] Arabic text is correctly aligned across all screens.
- [ ] No significant text overflow.
- [ ] Permissions work correctly and prevent unauthorized access.
- [ ] Returns are fully auditable.
- [ ] Inventory is fully auditable.
- [ ] UI Quality Gate has passed all tests.
- [ ] All critical tests have passed successfully.
- [ ] Deployment process is repeatable and documented.
- [ ] No known critical data-loss path.

---

## 45. IMPLEMENTATION ORDER

The following order must be followed to implement system components, ensuring a stable and organized build:

1. **Architecture:** Define and document the overall system architecture.
2. **Database Model:** Design the database schema, tables, and relationships.
3. **Domain Rules:** Implement core business logic in the `Domain` layer.
4. **Application Services:** Build services that coordinate between `Domain` and `Infrastructure`.
5. **Design System:** Create design tokens, icon library, and typography/color policies.
6. **Shared UI Components:** Develop buttons, input fields, tables, and dialogs.
7. **Authentication:** Implement the login screen and authentication mechanisms.
8. **POS Transaction Engine:** Build the core logic for processing sales.
9. **Payments:** Implement various payment mechanisms.
10. **Inventory:** Implement the inventory movement tracking system.
11. **Restaurant Workflows:** Implement table management, modifiers, and kitchen orders.
12. **Supermarket Workflows:** Implement barcode sales, weighted products, etc.
13. **Printing:** Integrate receipt and kitchen printers.
14. **Shifts:** Implement shift open/close management.
15. **Reports:** Build the various reporting system.
16. **Settings:** Develop settings management screens.
17. **Backup and Recovery:** Implement backup and restore mechanisms.
18. **Audit:** Implement the comprehensive audit log.
19. **Testing:** Write and execute all types of tests.
20. **Production Hardening:** Final performance, security, and error handling improvements.

---

## 46. FINAL IMPLEMENTATION PRINCIPLE

The system must be built as an integrated commercial POS product, not as a collection of disconnected screens.

**The correct engineering thought order is:**

```text
Business Rule
↓
Data Model
↓
Application Service
↓
UI State
↓
RTL Layout
↓
Visual Design
↓
Validation
↓
Permission
↓
Audit
↓
Testing
```

**A screen is not considered complete simply because it appears on screen.**

**A screen is considered complete only when all the following elements are complete:**

```text
Structure
+ Behavior
+ RTL (Right-to-Left Layout)
+ Visual Design
+ Typography
+ Spacing
+ Validation
+ Permissions
+ Loading
+ Empty
+ Error
+ Disabled
+ Resources
+ Data Flow
+ Audit
+ Logging
+ Testing
```

**This document is the final engineering contract. Any deviation from it requires review and explicit approval.**

# End of Final Engineering Specification Document

---

## 33. BACKUP AND RECOVERY (EXPANDED)

The backup and recovery strategy is critical for ensuring business continuity and data protection.

**Functional Requirements:**
- **Manual Backup:** The system must provide an easy-to-use interface for administrators to perform an immediate backup of the database and configuration files.
- **Automatic Backup:** The system must be configured to perform automatic backups at specific time intervals (daily, weekly) to a secure local storage location (e.g., network drive, dedicated local folder).
- **Retention Policy:** The system must support a configurable policy for retaining a certain number of backups (e.g., keep the last 7 daily backups and the last 4 weekly backups).
- **Backup Verification:** The system must include a mechanism for verifying backup integrity (e.g., CRC check, partial restore attempt) to ensure usability.
- **Restore:** A clear interface must be provided for restoring the database from a specific backup.
- **Backup History:** The system must maintain a log of all backup and restore operations, including date, time, user, and result.

**Visual Status Messages:**
The system must clearly display the status of backup and restore operations:
- `Backup Success`: A green message or success icon.
- `Backup Failure`: A red message or error icon with a brief reason.
- `Restore Warning`: A yellow message or warning icon (e.g., when restoring over existing data).
- `Restore Success`: A green message or success icon.
- `Restore Failure`: A red message or error icon with a brief reason.

**Important Rule:** The restore process requires strong confirmation and special permission (e.g., admin password, manager approval), given its critical impact on current system data. This process must not be easily reversible.

---

## 34. DATABASE INTEGRITY (EXPANDED)

Database integrity is paramount to ensuring the accuracy and reliability of financial and inventory data.

**Transactions:**
- **Critical Operations:** All operations involving multiple data modifications (especially financial and inventory) must use Transactions to ensure ACID principles (Atomicity, Consistency, Isolation, Durability).
- **Example Sale Transaction Flow:**
  ```sql
  BEGIN TRANSACTION; -- Start transaction

  -- 1. Create the main sales invoice record
  INSERT INTO Sales (SaleID, ShiftID, UserID, SubTotal, TaxAmount, DiscountAmount, TotalAmount, Status, CreatedAt)
  VALUES (@SaleID, @ShiftID, @UserID, @SubTotal, @TaxAmount, @DiscountAmount, @TotalAmount, 'Completed', GETDATE());

  -- 2. Create sales item records for each product in the invoice
  INSERT INTO SaleItems (SaleItemID, SaleID, ProductID, Quantity, UnitPrice, Discount, Tax, LineTotal)
  VALUES (@SaleItemID1, @SaleID, @ProductID1, @Quantity1, @UnitPrice1, @Discount1, @Tax1, @LineTotal1);
  INSERT INTO SaleItems (SaleItemID, SaleID, ProductID, Quantity, UnitPrice, Discount, Tax, LineTotal)
  VALUES (@SaleItemID2, @SaleID, @ProductID2, @Quantity2, @UnitPrice2, @Discount2, @Tax2, @LineTotal2);

  -- 3. Create the payment records associated with the invoice
  INSERT INTO Payments (PaymentID, SaleID, Method, Amount, Timestamp)
  VALUES (@PaymentID, @SaleID, @Method, @Amount, GETDATE());

  -- 4. Update inventory quantities for sold products
  UPDATE Inventory
  SET Quantity = Quantity - @Quantity1
  WHERE ProductID = @ProductID1;

  UPDATE Inventory
  SET Quantity = Quantity - @Quantity2
  WHERE ProductID = @ProductID2;

  -- 5. Create an audit log for the complete operation
  INSERT INTO AuditLog (UserID, Timestamp, Action, Entity, EntityID, BeforeValue, AfterValue)
  VALUES (@UserID, GETDATE(), 'Sale Completed', 'Sale', @SaleID, NULL, @TotalAmount);

  COMMIT TRANSACTION; -- Commit the transaction and persist all changes
  ```
- **Failure Scenario:** If an error occurs at any step of the transaction (e.g., inventory update fails), all changes made within this transaction must be rolled back:
  ```sql
  ROLLBACK TRANSACTION; -- Roll back all changes made within the transaction
  ```

**Important Rule:** No financial or inventory operation may remain partially or incompletely committed silently in the database. It must either be fully committed or fully rolled back to ensure consistency and integrity.

---

## 35. ERROR HANDLING (EXPANDED)

Effective error handling is essential for system stability and a positive user experience.

**User-Facing Error Messages:**
- Messages must be in Arabic, clear, understandable, and provide actionable guidance to the user.
- **Bad Example:**
  ```text
  SQL Exception 547: Foreign key constraint violation.
  ```
  This message is technical and incomprehensible to the average user.
- **Good Example:**
  ```text
  The operation could not be saved due to invalid data.
  Please check your inputs and try again.
  ```
  This message explains the problem and offers a solution to the user.

**Error Handling Rules:**
- **Technical Details:** Full technical error details (such as Stack Trace, internal error codes) must only be recorded in log files, not displayed to the user.
- **Unhandled Exceptions:**
  - Must be logged immediately and comprehensively in the Logging System.
  - The system must maintain its stability as much as possible, avoiding complete crashes.
  - Safe and appropriate feedback must be shown to the user (e.g., a generic error message like "An unexpected error occurred, please try again later").
  - No sensitive information (such as credentials, internal file paths) must be disclosed in user-facing error messages.

---

## 36. AUDIT LOG (EXPANDED)

The audit log is a vital component for ensuring accountability, tracking changes, and complying with regulatory requirements. The system must record all important actions performed by users or the system itself.

**Actions to be Audited:**
- **Authentication:** Login, Logout, failed login attempts.
- **Financial Operations:** Price Change, Discount Application, Returns, Cancellations, invoice modifications.
- **Inventory Management:** Inventory Adjustments, Waste, Stock Count, Recipe Changes.
- **Product Management:** Product Add/Edit/Delete/Archive.
- **User and Permission Management:** User Add/Edit/Delete, Permission Changes.
- **System Settings:** Important Settings Changes.
- **Backup and Restore:** Backup and Restore operations.
- **Printer Management:** Printer Add/Edit/Delete, printer assignment changes.

**Each Audit Record Includes:**

| Field | Description |
|-------|-------------|
| `AuditID` | A unique identifier for the audit record (Primary Key). |
| `UserID` | The ID of the user who performed the action. If automatic, it may be `System`. |
| `Timestamp` | The precise date and time of the action (with timezone). |
| `ActionType` | The type of action performed (e.g., `PriceChange`, `LoginSuccess`, `InventoryAdjustment`). |
| `EntityName` | The name of the affected entity (e.g., `Product`, `Sale`, `User`, `Setting`). |
| `EntityID` | The ID of the affected entity (e.g., `ProductID`, `SaleID`). |
| `BeforeValue` | The value before the change (for important modified fields). Must be in JSON or readable text format. |
| `AfterValue` | The value after the change (for important modified fields). Must be in JSON or readable text format. |
| `Reason` | The reason for the action (if the user provided a reason, such as return reason or inventory adjustment). |
| `IPAddress` | The IP address of the device from which the action was performed (for security tracking). |

**Audit Log Rules:**
- **Immutability:** Audit records must be non-modifiable and non-deletable after creation.
- **Performance:** The audit system must be designed to be efficient and not negatively impact core system performance.
- **Storage:** Audit records must be stored securely for a specified period according to data retention policies.

---

## 37. PERFORMANCE (EXPANDED)

Performance is a critical factor for the success of a POS system, especially in busy work environments. The system must be responsive and efficient.

**Core Performance Requirements:**
- **Startup Speed:** The application must start within a few seconds of launch.
- **Continuous Responsiveness:** The system must remain responsive to user input at all times, even during background operations or data fetching.
- **Avoid UI Blocking:** Time-consuming operations (such as complex database queries, printing operations) must be executed asynchronously to avoid freezing the UI.
- **Indexed Searches:** Appropriate indexes must be used on database columns frequently used in search and filter operations to ensure query speed.
- **Efficient Data Loading:** Avoid loading huge datasets at once into memory or the UI. Use:
  - **Pagination:** For handling large lists (e.g., product list, sales history).
  - **Lazy Loading:** To load data only when needed.
  - **Server-Side Filtering:** To apply filters on data before sending it to the client.
- **Avoid Repeated Queries:** Database queries must be optimized to avoid repeatedly fetching the same data or executing unnecessary queries.
- **Memory Management:** The application must be designed to avoid memory leaks and ensure efficient resource usage.
- **Animations:** Animations must be lightweight and smooth, and must never delay core operations. They can be disabled if necessary to improve performance on less powerful devices.

**Performance Metrics (KPIs):**
- **UI Responsiveness Time:** Less than 100 ms for most interactions.
- **Screen Load Time:** Less than 2 seconds for complex screens, and less than 500 ms for simple screens.
- **Barcode Processing Time:** Less than 50 ms.
- **Memory Consumption:** Must be within reasonable limits (e.g., less than 200 MB in idle mode).

---

## 38. ACCESSIBILITY AND OPERATIONAL USABILITY (EXPANDED)

The system must be designed to be usable by a wide range of users and effective in the daily work environment.

**Accessibility Requirements:**
- **Keyboard Navigation:** Full support for navigating between all interface elements and interacting with them using only the keyboard. Navigation order must be logical.
- **Visible Focus:** There must be a clear visual indicator for the currently focused element (e.g., a frame around the button or input field).
- **Readable Text:** Use text sizes and fonts that ensure readability for all users, with support for text zoom options where possible.
- **Adequate Contrast:** There must be sufficient contrast between text and background, and between interface elements, to ensure clarity for users with visual impairments.
- **Touch-Friendly Targets:** Interactive controls (buttons, icons) must be large enough for easy touch tapping, with adequate spacing between them to avoid errors.
- **Clear Status Messages:** Notifications, error messages, and confirmation messages must be clear, concise, and understandable.

**Operational Usability Requirements:**
- **Long Working Shifts:** The design must be easy on the eyes and not cause visual or mental fatigue during long work periods.
- **Minimum Clicks:** Workflows must be designed to minimize the number of clicks or interactions required to complete common tasks (e.g., quick sale process).
- **Instant Feedback:** The system must provide instant feedback to the user after every action (e.g., sound on barcode scan, confirmation message on save).
- **Error-Tolerant Design:** The system must prevent errors as much as possible and provide easy mechanisms for undoing actions or correcting errors.
- **Customization:** Allow some customizations (such as keyboard shortcuts, button ordering) to increase efficiency for advanced users, while maintaining overall consistency.

---

## 39. EXACT SCREEN SPECIFICATION CONTRACT (EXPANDED)

To ensure the highest levels of professionalism and consistency, every screen in the system must have a precise and detailed specification document. This document serves as a contract between designers and developers, and no screen can be considered complete without it.

**Detailed Screen Specification Structure:**

| Element | Detailed Description and Engineering Requirements |
|---------|---------------------------------------------------|
| `SCREEN ID` | **Unique Screen Identifier:** (e.g., `POS-001`, `AUTH-001`, `PROD-002`). Must be unique system-wide. |
| `SCREEN NAME` | **Clear Screen Name:** In Arabic, reflecting its primary function (e.g., `Main Sales Screen`, `Login Screen`). |
| `PURPOSE` | **Primary Functional Purpose:** A brief description of the screen's function and its importance in the system workflow. |
| `USER ROLES` | **Allowed User Roles:** A specific list of user roles (e.g., `Cashier`, `Manager`, `Admin`) that can access this screen. |
| `PERMISSIONS` | **Specific Required Permissions:** A list of granular permissions (e.g., `CanViewSales`, `CanApplyDiscount`) that the user must have to access the screen and perform actions within it. |
| `ENTRY POINT` | **Screen Entry Points:** How to access this screen (e.g., from main navigation menu, after login, from another screen via an `Edit` button). |
| `EXIT ACTIONS` | **Screen Exit Actions:** Actions the user can take to leave the screen (e.g., `Close`, `Cancel`, `Save & Exit`, `Back`). The behavior of each action must be specified. |
| `PRIMARY USER TASK` | **Primary User Task:** A clear description of the main task the user performs on this screen (e.g., `Complete a Sale`, `Manage Products`). |
| `SECONDARY TASKS` | **Secondary or Sub-tasks:** Additional tasks that can be performed on the screen (e.g., `Apply Discount`, `Search for a Product`). |
| `RTL REQUIREMENTS` | **Specific RTL Requirements:** Details of the right-to-left layout requirements for this screen, including text alignment, element order, and icon direction. |
| `PAGE STRUCTURE` | **Overall Page Structure Description:** Division of the screen into main areas (e.g., `Header`, `Sidebar`, `Main Content Area`, `Footer`). |
| `EXACT SECTIONS` | **Exact Sub-section Identification:** Details of sections within the screen (e.g., `Product Search Panel`, `Transaction Items List`, `Payment Summary`). |
| `EXACT COMPONENTS` | **List of All Components Used:** Identification of every UI component (e.g., `Button`, `TextBox`, `DataGrid`, `Dialog`) with reference to the `GLOBAL COMPONENT CONTRACT`. |
| `EXACT FIELDS` | **List of All Input Fields:** With their full specifications (e.g., `ID`, `Label`, `Placeholder`, `Required`, `Format`, `Validation`, `Error Message`). |
| `EXACT BUTTONS` | **List of All Buttons:** With their full specifications (e.g., `ID`, `Arabic Text`, `Icon`, `Purpose`, `Type`, `Permission`, `States`, `Keyboard Shortcut`). |
| `EXACT TABLES` | **List of All Tables:** With their full specifications (e.g., `Columns`, `Sorting`, `Filtering`, `Pagination`, `Actions`, `States`). |
| `EXACT DIALOGS` | **List of All Dialogs:** That can appear from this screen, with their full specifications (e.g., `ID`, `Title`, `Purpose`, `Primary Action`, `States`). |
| `VALIDATION` | **Data Validation Rules:** Details of all validation rules applied to inputs, including specific error messages. |
| `BUSINESS RULES` | **Specific Business Rules:** Details of business rules that apply to this screen and affect its behavior (e.g., `Discount cannot exceed 50%`, `Stock must be available for sale`). |
| `LOADING STATE` | **How to Display the Loading State:** A visual and behavioral description of how the screen handles data fetching or time-consuming operations. |
| `EMPTY STATE` | **How to Display the Empty State:** A visual and behavioral description of how the screen handles when there is no data to display. |
| `ERROR STATE` | **How to Display the Error State:** A visual and behavioral description of how the screen handles errors, including user-facing error messages. |
| `DISABLED STATE` | **How to Display Disabled Elements:** A visual and behavioral description of how disabled controls or sections are displayed. |
| `PERMISSION STATE` | **How to Handle Missing Permissions:** A description of how to hide or disable features the user does not have permission to access. |
| `KEYBOARD SHORTCUTS` | **Supported Keyboard Shortcuts:** A list of specific shortcuts for this screen and their functions. |
| `ICON REQUIREMENTS` | **Specific Required Icons:** A list of icons to be used in this screen. |
| `SOUND REQUIREMENTS` | **Specific Required Sounds:** A list of sounds to be played for specific events in this screen. |
| `DATA INPUTS` | **Data the Screen Receives:** A description of the data passed to the screen when opened or interacted with. |
| `DATA OUTPUTS` | **Data the Screen Produces:** A description of the data produced by the screen (e.g., `SaleID` after completing a sale). |
| `AUDIT REQUIREMENTS` | **Audit Requirements:** Identification of actions that must be recorded in the audit log from this screen. |
| `LOGGING REQUIREMENTS` | **Logging Requirements:** Identification of events and errors that must be recorded in system logs. |
| `ACCEPTANCE CRITERIA` | **Acceptance Criteria:** A list of points to verify for testing the screen and considering it complete (e.g., `[ ] RTL Layout is correct`, `[ ] All validations work`). |

**Important Rule:** No screen can be considered fully specified without these elements. These specifications must be an integral part of the development and delivery process.

---

## 40. AI CODING AGENT PROTOCOL (EXPANDED)

This protocol is a strict guideline for AI Coding Agents and human developers when implementing any feature or component in the system. The goal is to ensure consistency, quality, and adherence to the engineering standards defined in this document.

**Mandatory Implementation Steps:**

1. **Read and Understand the Document:** The agent/developer must read this entire final engineering document and understand all its requirements, rules, and prohibitions before starting any work. Assumption or guessing is not permitted.
2. **Module Identification:** Identify the relevant software module for the feature to be implemented (e.g., `POS.Domain`, `POS.Application`, `POS.Infrastructure`, `POS.Desktop`).
3. **Screen ID Identification:** If the feature relates to the UI, identify the specific screen ID (e.g., `POS-001`) and review its exact screen specification contract.
4. **Business Rules Extraction:** Extract all relevant business rules from the `POS.Domain` layer or the business rules section in this document.
5. **Permissions Identification:** Identify the granular permissions required to access the feature or perform associated actions.
6. **Data Requirements Identification:** Understand the data model and affected entities, including fields, relationships, and data integrity rules.
7. **UI States Identification:** Identify all possible UI states for the feature (loading, empty, error, disabled, permission) and how to handle each.
8. **RTL Requirements Identification:** Review the right-to-left layout requirements specified for the screen and affected components.
9. **Resource Identification:** Identify the icons, sounds, and design tokens required from the central resource system.
10. **Inspect Existing Architecture:** Review existing code and architecture to avoid duplication and ensure consistency with adopted architectural patterns.
11. **Avoid Duplicate Implementations:** Reuse existing components and services as much as possible instead of creating duplicate solutions.
12. **Implement Domain Logic:** Start by implementing or modifying code in the `POS.Domain` layer first, to ensure core business rules are independent of other technologies.
13. **Implement Application Service:** Write or modify code in the `POS.Application` layer, which coordinates between `Domain` and `Infrastructure`.
14. **Implement Data Access:** Write or modify code in the `POS.Infrastructure` layer (repositories, database context) to handle persistent data storage.
15. **Implement UI:** Write or modify code in the `POS.Desktop` layer, adhering to the design system and global component contract.
16. **Implement Validation:** Add all input validation rules at both the UI and application layers.
17. **Implement Permissions:** Apply permission checking mechanisms at both the UI and application layers.
18. **Implement Logging:** Add logging points for important events and errors across all layers.
19. **Implement Audit:** Add audit records for sensitive actions according to audit log requirements.
20. **Implement Tests:** Write unit, integration, and UI tests for the implemented feature.
21. **Build the Solution:** Ensure the build succeeds without errors or warnings.
22. **Visual Screen Review:** Visually review the screen to ensure full compliance with UI/UX, RTL, and the design system.
23. **Fix Defects:** Address any errors or defects discovered during testing or review.
24. **Report Completion:** Provide a detailed implementation status report.

**Implementation Status Report:**
The AI Agent/Developer must provide a detailed report after each implementation task, including:

| Field | Description |
|-------|-------------|
| `Implemented Features` | A list of features successfully implemented. |
| `Not Implemented Features` | A list of features not implemented, with the reason (e.g., out of scope, unexpected complexity). |
| `Blocked Features` | A list of currently blocked features, with the reason and obstacles preventing implementation (e.g., waiting for a core component, infrastructure issue). |
| `Assumptions Made` | Any assumptions made during implementation that were not clearly specified in the document. |
| `Test Results Summary` | A summary of test results (number of tests, passed, failed). |
| `Build Result` | The build result (success/failure, with error details if any). |
| `Known Issues` | Any known issues or remaining defects in the implemented feature, with priority and impact. |

---

## 41. UI QUALITY GATE (EXPANDED)

The UI Quality Gate is a critical checkpoint to ensure that all screens and components meet strict visual and functional standards. A screen is considered **FAILED** if any of the following conditions are met:

- **Incorrect RTL:** Arabic text is incorrectly left-aligned, or RTL element order (such as action buttons in dialogs, or column order in tables) is incorrect.
- **Text Overflow/Clipping:** Text is clipped or overflows control boundaries in any language (Arabic or English).
- **Element Overlap:** Controls or components visually overlap.
- **Inconsistent Spacing:** Spacing between elements or sections does not follow the specified design token system.
- **Random Colors:** Colors used are random or do not follow the specified semantic design tokens.
- **Missing/Inconsistent Icons:** Icons are missing, inconsistent with the approved icon library, or do not reflect the correct meaning in RTL context.
- **Missing Loading State:** The screen or component does not display a clear loading indicator during data fetching or time-consuming operations.
- **Missing Empty State:** The screen or component does not display a clear and directed message when there is no data to display.
- **Missing Error State:** The screen or component does not display a clear and understandable error message to the user when an error occurs.
- **Missing Permission State:** The screen or component does not correctly handle missing permissions (e.g., unauthorized features are not hidden or disabled).
- **Ambiguous Buttons:** Buttons are unclear in purpose, or do not correctly reflect their state (e.g., a `Save` button does not become disabled when there are no changes).
- **Unreadable Tables:** Tables are disorganized, difficult to read, or do not support basic sorting/filtering.
- **Incomplete Dialogs:** Dialogs do not contain all states (loading, error) or exit behaviors (cancel, confirm).
- **Keyboard Navigation Failure:** Navigation between UI elements using the keyboard is not possible or not logical.
- **Unclear Destructive Actions:** Actions that lead to data loss (e.g., `Delete`) do not require clear confirmation or have an ambiguous message.
- **Confusing Visual Hierarchy:** The screen does not clearly answer the visual hierarchy questions (Where am I? What is the primary task?).

---

## 42. SCREEN COMPLETION CHECKLIST (EXPANDED)

This list is a mandatory tool for developers and AI agents to ensure that every implemented screen meets all specified requirements before delivery. This checklist must be completed for each screen individually.

- [ ] `Screen ID` is clearly specified in the screen specification.
- [ ] `Purpose` is clearly specified.
- [ ] `User Roles` are specified.
- [ ] `Permissions` are specified and correctly implemented.
- [ ] `Entry Point` is specified.
- [ ] `Exit Behavior` is specified and implemented.
- [ ] `RTL` is visually and functionally verified.
- [ ] `Layout` is verified according to the design.
- [ ] `Typography` is verified (fonts, sizes, alignment).
- [ ] `Spacing` is verified and follows the design token system.
- [ ] `Colors` use approved design tokens.
- [ ] `Icons` use the approved system and are correctly applied.
- [ ] `Buttons` are documented and work in all states.
- [ ] `Fields` are documented and work in all states.
- [ ] `Validation` is fully implemented and displays clear error messages.
- [ ] `Loading state` is correctly implemented.
- [ ] `Empty state` is correctly implemented.
- [ ] `Error state` is correctly implemented.
- [ ] `Disabled state` is correctly implemented.
- [ ] `Permission state` is correctly implemented.
- [ ] `Keyboard` navigation and interaction are tested.
- [ ] `Database operations` are tested for data integrity.
- [ ] `Audit` is implemented for all sensitive actions.
- [ ] `Logging` is implemented for events and errors.
- [ ] `Sounds` are implemented where required and are configurable.
- [ ] `Text overflow` is verified and no text clipping exists.
- [ ] `Build` succeeds without errors or warnings.
- [ ] `Tests` (unit, integration, UI) pass successfully.
- [ ] `Acceptance criteria` specified for the screen have been met.

---

## 43. COMPREHENSIVE TESTING STRATEGY

To ensure the highest levels of quality, reliability, and performance, the development process must follow a comprehensive testing strategy covering all aspects of the system.

### 43.1 Sales Tests
- **Unit Tests:**
  - Verify accuracy of tax, discount, and total calculations with 3 decimal place precision.
  - Test logic for adding/removing products from the invoice.
  - Verify correct quantity and price updates.
- **Integration Tests:**
  - Test a complete sale lifecycle (add products, apply discount, pay, print receipt) verifying correct inventory update and audit log.
  - Test Hold/Retrieve Sales operations.
  - Test returns and cancellations and their impact on inventory and financial records.
- **UI Tests (Automated & Manual):**
  - Add single item, multiple items, and verify correct display on the invoice.
  - Test barcode search and text search functions for products.
  - Apply discounts and taxes and verify their reflection in totals.
  - Payment process with various methods (cash, card) and verify correct change calculation.
  - Test return and cancellation from the UI.
  - Verify screen behavior in loading, empty, error, and permission denied states.

### 43.2 Inventory Tests
- **Unit Tests:**
  - Verify correct inventory updates on purchase, sale, wastage, and adjustment operations.
  - Test recipe ingredient consumption logic.
- **Integration Tests:**
  - Test complete product lifecycle (purchase, sale, return, wastage) with accurate inventory movement tracking.
  - Test stock counting and inventory adjustment operations and their impact on reports.
  - Verify all inventory movements are audited in the audit log.

### 43.3 Printing Tests
- **Unit Tests:**
  - Verify receipt/kitchen ticket content generation in correct format (ESC/POS).
- **Integration Tests (with actual or simulated printing devices):**
  - Test printing when the printer is connected and working normally.
  - Test system behavior when the printer is disconnected (Offline) or unavailable.
  - Simulate network interruption during printing and verify retry mechanisms.
  - Test automatic and manual print retry.
  - Verify system behavior with incorrect or incompatible printer configuration.
  - Test printing on multiple printers (receipt, kitchen, beverages).

### 43.4 Database Tests
- **Unit Tests:**
  - Verify correctness of queries and stored procedures.
  - Test integrity constraints such as foreign keys and unique constraints.
- **Integration Tests:**
  - Simulate sudden system shutdown during a critical transaction and verify complete data rollback.
  - Test system behavior on transaction failure (e.g., constraint violation) and verify data integrity.
  - Test database backup and restore operations and verify accuracy of restored data.
  - Test performance of large queries and verify effective index usage.

### 43.5 Security Tests
- **Unit Tests:**
  - Verify correct application of password policies.
  - Test encryption and decryption mechanisms for sensitive data.
- **Integration Tests:**
  - Test login with invalid credentials and verify system behavior (error messages, failed attempt logging).
  - Test system behavior with a disabled user or locked user.
  - Test accessing features with insufficient permission and verify correct access denial.
  - Test actions requiring manager approval and verify correct workflow.
  - Attempt an unauthorized sensitive action (e.g., changing a product price without permission) and verify system denial and audit log recording.

### 43.6 Performance Tests
- **Load Testing:** Simulate a large number of concurrent users or simultaneous operations (e.g., rapid repeated barcode scanning) to measure system responsiveness.
- **Stress Testing:** Push the system to its limits to measure the breaking point and system behavior under pressure.
- **Endurance Testing:** Run the system for extended periods (e.g., 24 hours) to check for memory leaks or performance degradation over time.
- **Responsiveness Tests:** Measure UI response times for common inputs (e.g., button click, text entry).

---

## 44. RELEASE QUALITY GATE (EXPANDED)

The system is **NOT PRODUCTION-READY** until all the following conditions are fully and non-negotiably met. This gate is the final assurance of product quality before deployment.

- [ ] **Offline Operation:** Core sales operations (add products, pay, print receipt) work fully and reliably without Internet connectivity.
- [ ] **Data Integrity:** Financial and inventory data integrity is fully verified through integration tests and audits.
- [ ] **Financial Precision:** Financial calculations (taxes, discounts, totals) are verified with 3 decimal place precision in all scenarios.
- [ ] **Backup:** The backup system works reliably (manual and automatic) and backup integrity is verified.
- [ ] **Restore:** Database restoration from a backup works reliably and restores data accurately.
- [ ] **Receipt Printing:** Receipt printing works reliably in all scenarios (success, failure, retry).
- [ ] **Kitchen Printing:** Kitchen order printing works reliably and routes to the correct stations.
- [ ] **Visual RTL:** RTL layout is fully visually verified across all screens and components.
- [ ] **Arabic Text Alignment:** Arabic text is correctly aligned (right by default) in all screens and controls.
- [ ] **No Text Overflow:** No significant text overflow or clipping exists anywhere in the interface.
- [ ] **Permissions:** The permissions system works correctly and prevents unauthorized access to features and sensitive actions.
- [ ] **Returns Auditable:** All return operations are fully recorded in the audit log and reviewable.
- [ ] **Inventory Auditable:** All inventory movements are fully recorded in the audit log and reviewable.
- [ ] **UI Quality Gate:** The UI Quality Gate has passed all specified tests.
- [ ] **Critical Tests:** All critical tests specified in the testing strategy have passed successfully.
- [ ] **Deployment Repeatable:** The deployment process is documented and repeatable reliably.
- [ ] **No Critical Data-Loss Path:** No known path in the system can lead to critical data loss under any normal or abnormal operating conditions.

---

## 45. ENGINEERING IMPLEMENTATION ORDER

The following order must be followed to implement system components, ensuring a stable, organized, and scalable build. This order reflects engineering dependencies and minimizes rework.

1. **Architecture Definition:** Define and document the overall system architecture, including layers, modules, and key dependencies.
2. **Database Model Design:** Design the database schema, tables, relationships, indexes, and integrity constraints. This should be done in parallel with domain rules.
3. **Domain Rules Implementation:** Implement core business logic in the `POS.Domain` layer, focusing on entities, value objects, and technology-independent business rules.
4. **Application Services Implementation:** Build services that coordinate between `Domain` and `Infrastructure`, applying patterns such as CQRS (Commands and Queries).
5. **Design System Development:** Create design tokens (colors, spacing, typography), icon library, and overall appearance policies.
6. **Shared UI Components Development:** Develop buttons, input fields, tables, and dialogs that will be used across the application.
7. **Authentication Module:** Implement the login screen, authentication mechanisms, and session management.
8. **POS Transaction Engine:** Build the core logic for processing sales, including adding products, calculating totals, and applying discounts.
9. **Payments Module:** Implement various payment mechanisms (cash, card, partial payment) and integrate them with the transaction engine.
10. **Inventory Management:** Implement the inventory movement tracking system, including purchase, sale, returns, wastage, and adjustments.
11. **Restaurant Workflows:** Implement table management, modifiers, recipes, and kitchen orders.
12. **Supermarket Workflows:** Implement barcode sales, weighted products, and multi-unit management.
13. **Printing Subsystem:** Integrate receipt and kitchen printers, and manage print settings.
14. **Shifts Management:** Implement shift open/close management and cash calculations.
15. **Reporting Module:** Build various reporting systems (sales, inventory, profitability, cash).
16. **Settings Management:** Develop various system settings management screens.
17. **Backup and Recovery System:** Implement manual and automatic backup and restore mechanisms.
18. **Audit Logging:** Implement the comprehensive audit log for all sensitive actions.
19. **Comprehensive Testing:** Write and execute all types of tests (unit, integration, UI, performance, security).
20. **Production Hardening:** Improve performance, security, final error handling, and operational documentation.

---

## 46. FINAL ENGINEERING IMPLEMENTATION PRINCIPLE

The system must be built as an integrated, high-quality commercial POS product, not as a collection of disconnected screens or features. Every part of the system must serve the overall product vision and adhere to strict engineering standards.

**Correct Engineering Thought Process:**

```text
Business Rule - What should the system do?
↓
Data Model - How is data stored to support the business rule?
↓
Application Service - How are operations coordinated to execute the business rule?
↓
UI State - How does the interface reflect the system state to the user?
↓
RTL Layout - How are elements visually arranged for the Arabic language?
↓
Visual Design - How does the interface look according to the design system?
↓
Validation - How do we ensure input correctness?
↓
Permission - Who can perform this action?
↓
Audit - How do we record this action for future review?
↓
Testing - How do we verify everything works correctly?
```

**A screen is not considered complete simply because it appears on the screen or its basic function works.**

**A screen is considered engineeringly complete only when all the following elements are complete, documented, and tested:**

```text
Structure - Screen design and organization.
+ Behavior - How the screen interacts with user input and system events.
+ RTL (Right-to-Left Layout) - Full compliance with RTL standards.
+ Visual Design - Adherence to the design system and its tokens.
+ Typography - Correct fonts, sizes, and alignment.
+ Spacing - Application of the approved spacing system.
+ Validation - All validation rules implemented.
+ Permissions - All permission restrictions implemented.
+ Loading - Correct handling of loading states.
+ Empty - Correct handling of no-data states.
+ Error - Correct handling of errors.
+ Disabled - Correct handling of disabled elements.
+ Resources - Use of approved icons and sounds.
+ Data Flow - Understanding and applying data flow between layers.
+ Audit - Recording sensitive actions.
+ Logging - Recording events and errors.
+ Testing - Passing all relevant tests.
```

**This document is the final and binding engineering contract. Any deviation from it requires review and explicit approval from the lead system engineer.**

# End of Final Engineering Specification Document
