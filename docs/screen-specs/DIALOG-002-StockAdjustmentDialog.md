# DIALOG-002: Stock Adjustment Dialog (تسوية المخزون)

## PURPOSE
Adjust inventory quantities for a specific product. Record the reason for adjustment and track changes via inventory movements.

## PERMISSIONS
- `AdjustInventory` — Required to perform stock adjustments

## EXACT FIELDS

| #  | Field              | Type             | Required | Notes                                          |
|----|--------------------|------------------|----------|------------------------------------------------|
| 1  | Product Selector   | ComboBox/Search  | Yes      | Search and select product                      |
| 2  | Current Stock      | Label (read-only)| -        | Shows current quantity before adjustment       |
| 3  | Adjustment Type    | ComboBox         | Yes      | "إضافة" (Add) / "خصم" (Subtract) / "تسوية" (Set) |
| 4  | Quantity           | NumericUpDown    | Yes      | Quantity to adjust                             |
| 5  | New Stock Preview  | Label (read-only)| -        | Calculated new quantity after adjustment       |
| 6  | Reason             | ComboBox/TextBox | Yes      | "تلف" / "سرقة" / "خطأ في الجرد" / "مرتجع" / "أخرى" |
| 7  | Notes              | TextBox          | No       | Additional details about the adjustment        |

## EXACT BUTTONS

| #  | Button              | Action                                                |
|----|---------------------|-------------------------------------------------------|
| 1  | ✅ تأكيد التسوية     | Validates and saves the adjustment                    |
| 2  | ❌ إلغاء             | Closes without saving                                 |

## ADJUSTMENT TYPES

| Type     | Effect                                      |
|----------|---------------------------------------------|
| إضافة    | Increases stock by quantity                  |
| خصم      | Decreases stock by quantity                  |
| تسوية    | Sets stock to exact quantity entered          |

## UI STATES

| State         | Description                                          |
|---------------|------------------------------------------------------|
| Selecting     | User selecting product and entering details          |
| Previewing    | New stock preview calculated                         |
| Saving        | Adjustment being saved                               |
| Complete      | "تمت تسوية المخزون بنجاح" message                   |
| Error         | Validation or save error                             |

## ACCEPTANCE CRITERIA

1. **AC-001:** Product search works by name or SKU.
2. **AC-002:** Current stock is displayed and updates dynamically after selection.
3. **AC-003:** New stock preview recalculates in real-time as fields change.
4. **AC-004:** Adjustment type "خصم" cannot reduce stock below 0.
5. **AC-005:** Reason is required; predefined options cover common cases.
6. **AC-006:** Adjustment creates an InventoryMovement record with the reason.
7. **AC-007:** Negative adjustments with quantity > current stock show warning.
8. **AC-008:** After saving, inventory grid refreshes automatically.
