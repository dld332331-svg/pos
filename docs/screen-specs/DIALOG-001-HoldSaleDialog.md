# DIALOG-001: Hold Sale Dialog (تعليق الفاتورة)

## PURPOSE
Preview and retrieve previously held (parked) sales. Allows the cashier to resume a sale that was temporarily suspended.

## PERMISSIONS
- `Sell` — Required to view and retrieve held sales

## EXACT FIELDS

| #  | Field              | Type          | Notes                                       |
|----|--------------------|---------------|---------------------------------------------|
| 1  | Held Sales Grid    | DataGridView  | List of held sales with summary             |
| 2  | Search Box         | TextBox       | Optional search within held sales           |

## EXACT BUTTONS

| #  | Button               | Action                                               |
|----|----------------------|------------------------------------------------------|
| 1  | ✅ استرداد (Retrieve) | Restores held sale items to the POS cart             |
| 2  | 🗑️ حذف (Delete)     | Deletes held sale after confirmation                  |
| 3  | ❌ إلغاء (Cancel)     | Closes dialog without action                         |
| 4  | 🔄 تحديث              | Refreshes list of held sales                         |

## GRID COLUMNS

| #  | Column Name  | Format         | Notes                  |
|----|--------------|----------------|------------------------|
| 1  | #            | Integer        | Row number             |
| 2  | التاريخ      | DateTime       | When sale was held     |
| 3  | الإجمالي     | JOD 0.000      | Total sale amount      |
| 4  | عدد الأصناف  | Integer        | Number of items        |
| 5  | العميل       | Text           | Customer name (if set) |
| 6  | ملاحظات      | Text           | Notes on held sale     |

## UI STATES

| State     | Description                                          |
|-----------|------------------------------------------------------|
| Loading   | Held sales being fetched                             |
| Loaded    | Held sales displayed in grid                         |
| Empty     | No held sales available                              |
| Retrieving| Selected sale being restored to POS                  |

## ACCEPTANCE CRITERIA

1. **AC-001:** Retrieving a held sale restores all items, quantities, modifiers, and discounts.
2. **AC-002:** After retrieval, the held sale record is deleted.
3. **AC-003:** Deleting a held sale requires confirmation.
4. **AC-004:** Grid shows total, item count, and date for each held sale.
5. **AC-005:** Retrieving a sale from yesterday or earlier shows a warning about pricing changes.
6. **AC-006:** Double-clicking a row retrieves the sale.
