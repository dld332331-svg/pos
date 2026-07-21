# SCREEN-021: Purchase Orders (أوامر الشراء)

## PURPOSE
Create and manage purchase orders for inventory procurement. Track order status from creation through receipt.

## PERMISSIONS
- `ManagePurchases` — Required to manage purchase orders

## EXACT FIELDS

| #  | Field                | Type          | Notes                                        |
|----|----------------------|---------------|----------------------------------------------|
| 1  | PO Number            | Label         | Auto-generated: PO-YYYYMMDD-XXX              |
| 2  | Supplier Selector    | ComboBox      | Populated from active suppliers              |
| 3  | Order Date           | DateTimePicker| Defaults to today                            |
| 4  | Expected Delivery    | DateTimePicker| Optional delivery date                        |
| 5  | Status               | Label/Badge   | "جديد" / "جزئي" / "مستلم" / "ملغي"           |
| 6  | Order Items Grid     | DataGridView  | Columns: #, المنتج, الكمية, السعر, الإجمالي   |
| 7  | Total Amount         | Label         | "الإجمالي: X.XXX JOD"                         |
| 8  | Notes                | TextBox       | Internal notes / supplier instructions        |
| 9  | POs History Grid     | DataGridView  | List of all purchase orders                   |

## EXACT BUTTONS

| #  | Button               | Action                                                |
|----|----------------------|-------------------------------------------------------|
| 1  | ➕ أمر شراء جديد      | Creates new blank purchase order                      |
| 2  | ➕ إضافة صنف          | Adds a product line item to the order                 |
| 3  | 🗑️ حذف صنف           | Removes selected item from order                      |
| 4  | 💾 حفظ الأمر          | Saves purchase order                                  |
| 5  | 📥 استلام الطلب       | Records receipt of items (partial or full)            |
| 6  | ❌ إلغاء الأمر        | Cancels purchase order                                |
| 7  | 🖨️ طباعة             | Prints purchase order document                        |

## GRID COLUMNS (Order Items)

| #  | Column Name  | Format              | Notes                |
|----|--------------|---------------------|----------------------|
| 1  | #            | Integer             | Row number           |
| 2  | المنتج       | Text                | Product name         |
| 3  | الكمية المطلوبة | Integer          | Ordered quantity     |
| 4  | الكمية المستلمة | Integer          | Received quantity    |
| 5  | سعر الوحدة   | JOD 0.000           | Unit cost            |
| 6  | الإجمالي     | JOD 0.000           | Qty × Unit Price    |

## ORDER STATUSES

| Status     | Color  | Description                         |
|------------|--------|-------------------------------------|
| جديد       | 🔵 Blue | Created, not yet received           |
| جزئي       | 🟡 Yellow | Partially received                |
| مستلم      | 🟢 Green | Fully received                      |
| ملغي       | 🔴 Red  | Cancelled                           |

## UI STATES

| State         | Description                                         |
|---------------|-----------------------------------------------------|
| Loading       | Purchase orders being loaded                        |
| Loaded        | Orders displayed; can create/edit                   |
| Creating      | New PO being entered                                |
| Saving        | PO data being saved                                 |
| Receiving     | Recording receipt of items                          |
| Error         | Failed to load or save                              |

## ACCEPTANCE CRITERIA

1. **AC-001:** Creating a new PO generates a unique PO number.
2. **AC-002:** Items are added from the product list with unit cost entry.
3. **AC-003:** Total is auto-calculated from line totals.
4. **AC-004:** Receiving updates inventory quantities and marks items as received.
5. **AC-005:** Partial receipt updates status to "جزئي" until all items received.
6. **AC-006:** Cancelling a PO requires confirmation.
7. **AC-007:** Printing outputs the PO document with supplier info and all items.
8. **AC-008:** All monetary values displayed in JOD with 3 decimal places.
9. **AC-009:** Receiving items creates inventory movement records.
