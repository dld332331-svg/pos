# SCREEN-003: POS Terminal (شاشة البيع)

## PURPOSE
The primary Point-of-Sale screen for processing customer transactions. Handles item lookup, cart management, discount application, payment processing, and receipt printing.

## PERMISSIONS
- `Sell` — Required to use the POS screen
- `ApplyDiscount` — Required to use discount functionality
- `ChangePrice` — Required to change item prices
- `CancelItem` — Required to remove items from cart
- `CancelInvoice` — Required to void entire invoice
- `OpenCashDrawer` — Required to open cash drawer

## EXACT FIELDS

| #  | Field                     | Type          | Notes                                           |
|----|---------------------------|---------------|-------------------------------------------------|
| 1  | Search Box                | RtlTextBox    | Barcode or product name search (Arabic/English) |
| 2  | Items Grid (Cart)         | DataGridView  | Columns: #, Name, Price, Qty, Total, Actions    |
| 3  | Subtotal Label            | Label         | "المجموع الفرعي" - formatted as JOD 0.000       |
| 4  | Tax Label                 | Label         | "الضريبة" - formatted as JOD 0.000              |
| 5  | Discount Label            | Label         | "الخصم" - formatted as JOD 0.000               |
| 6  | Total Label               | Label         | "الإجمالي" - large font, formatted as JOD 0.000 |
| 7  | Customer Label/Selector   | ComboBox/Label| Optional customer selection                     |
| 8  | Sale Type Selector        | ComboBox      | "بيع عادي" / "مطعم" / "توصيل" / "مؤجل"         |
| 9  | Item Count Badge          | Label         | Badge showing number of items in cart            |
| 10 | Status Bar                | Label         | Bottom status messages ("تمت الإضافة", "تم الدفع", etc.) |

## EXACT BUTTONS

| #  | Button               | Permission      | Action                                                |
|----|----------------------|-----------------|-------------------------------------------------------|
| 1  | 🔍 (Search Icon)     | Sell            | Triggers product search                               |
| 2  | ➕ (Quick Add)       | Sell            | Adds selected product to cart                         |
| 3  | 🗑️ (Remove Item)    | CancelItem      | Removes selected item from cart                       |
| 4  | 🏷️ خصم (Discount)   | ApplyDiscount   | Opens discount entry dialog                           |
| 5  | 💰 دفع (Pay)         | Sell            | Opens PaymentDialog with sale totals                  |
| 6  | ⏸️ تعليق (Hold)     | Sell            | Holds current sale; saves to HeldSale                 |
| 7  | 📋 سحوبات (Held)    | Sell            | Opens HoldSaleDialog to retrieve held sales           |
| 8  | ❌ إلغاء الفاتورة   | CancelInvoice   | Voids entire invoice after confirmation               |
| 9  | 🖨️ طباعة (Print)    | Sell            | Reprints last receipt                                 |
| 10 | 📦 درج النقود (Drawer) | OpenCashDrawer | Opens cash drawer                                     |

## QUICK PRODUCT CATEGORY GRID

| #  | Area               | Description                                        |
|----|--------------------|----------------------------------------------------|
| 1  | Category Tabs      | Horizontal tabs showing product categories         |
| 2  | Product Grid       | Grid of product buttons with name and price        |
| 3  | Quantity Selector  | +/- buttons to adjust quantity before adding        |
| 4  | Modifier Button    | Opens ModifierSelectionDialog for applicable items  |

## UI STATES

| State                | Description                                        |
|----------------------|----------------------------------------------------|
| EmptySale            | No items in cart; all action buttons visible       |
| ActiveSale           | Items in cart; totals displayed; Pay button enabled|
| Searching            | Search in progress; results loading                 |
| HoldPending          | Sale being held; confirmation shown                 |
| PaymentInProgress    | PaymentDialog open; POS waits for completion        |
| SaleComplete         | Sale processed; receipt printing; brief success msg |
| Voiding              | Void confirmation shown                             |

## ACCEPTANCE CRITERIA

1. **AC-001:** Barcode scan adds product to cart instantly.
2. **AC-002:** Product search shows results after 3+ characters typed.
3. **AC-003:** Adding item updates cart grid and recalculates totals in real-time.
4. **AC-004:** Discount button disabled unless user has `ApplyDiscount` permission.
5. **AC-005:** Pressing Pay with empty cart shows error "السلة فارغة".
6. **AC-006:** Held sale can be retrieved and resumed with all items intact.
7. **AC-007:** Voiding invoice requires confirmation dialog.
8. **AC-008:** All monetary values display in JOD with 3 decimal places (0.000).
9. **AC-009:** Keyboard shortcuts: F1=Search, F2=Discount, F4=Pay, F7=Hold, F8=Held Sales.
10. **AC-010:** Quantity can be changed by double-clicking the quantity cell.
11. **AC-011:** Quick category grid shows products grouped by category.
12. **AC-012:** Modifiers can be added/changed via context menu on cart items.
