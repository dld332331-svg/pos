# DIALOG-005: Modifier Selection Dialog (اختيار الإضافات)

## PURPOSE
Display available modifier groups and options for a product that supports modifications (add-ons, removals, sizes). Allows the user to select quantities of each option before adding the product to the invoice.

## PERMISSIONS
- `Sell` — Required to access modifier selection

## EXACT FIELDS

| # | Field | Type | Notes |
|---|-------|------|-------|
| 1 | Modifier Group Title | Label | Group name (e.g., "إضافات", "حجم", "صلصة") |
| 2 | Option Checkboxes/Counters | NumericUpDown / CheckBox | Per-option selection with quantity picker |
| 3 | Option Price | Label | Price adjustment for each selected option |
| 4 | Extra Cost Total | Label | Sum of all selected option price adjustments |
| 5 | Summary Preview | Label | Arabic summary of selections (e.g., "جبنة +2, صوص +1") |

## EXACT BUTTONS

| # | Button | Action |
|---|--------|--------|
| 1 | ✅ تأكيد (Confirm) | Validates required groups, then returns `ModifierSelectionResult` with selections, total extra cost, and summary string |
| 2 | ❌ إلغاء (Cancel) | Closes dialog with `DialogResult.Cancel` without saving |

## UI STATES

| State | Description |
|-------|-------------|
| Ready | Default state — groups and options displayed for selection |
| Empty | No modifier groups available for this product (shows "لا توجد تعديلات متاحة لهذا المنتج") |
| Selected | One or more options selected; extra cost total updates in real-time |
| ValidationError | Required group has no selection — shows warning via `RtlMessageBox` before returning |

## ACCEPTANCE CRITERIA

1. **AC-001:** All modifier groups for the product are displayed with their options, sorted by SortOrder.
2. **AC-002:** Each option shows its name and price adjustment (e.g., "+ 0.500 JOD" or "مجاني").
3. **AC-003:** User toggles an option on/off via CheckBox. When checked, the modifier is selected (qty=1); when unchecked, it is deselected.
4. **AC-004:** Total extra cost updates in real-time as selections change, including size price adjustments.
5. **AC-005:** If a modifier has size variants, a ComboBox appears showing available sizes with their price adjustments; selecting a size updates the total.
6. **AC-006:** Summary string is generated showing selected option names (with size if applicable) in Arabic, comma-separated.
7. **AC-007:** Confirm validates required groups — if a required group has no selection, shows warning and returns focus.
8. **AC-008:** Confirm validates MinSelections/MaxSelections constraints.
9. **AC-009:** Confirm returns the `ModifierSelectionResult` with selections, total extra cost, and summary.
10. **AC-010:** Cancel returns `null` without modifying the cart.
11. **AC-011:** If no modifier groups exist, shows "لا توجد تعديلات متاحة لهذا المنتج" message and only Cancel is meaningful.
12. **AC-012:** RTL layout is correct with right-aligned labels and Arabic text throughout.
