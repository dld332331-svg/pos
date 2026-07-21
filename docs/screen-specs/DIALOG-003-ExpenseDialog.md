# DIALOG-003: Expense Dialog (المصروفات)

## PURPOSE
Record a cash expense during an active shift. Tracks expense category, amount, and reason for accounting purposes.

## PERMISSIONS
- Requires active shift to record expenses

## EXACT FIELDS

| #  | Field              | Type             | Required | Notes                                     |
|----|--------------------|------------------|----------|-------------------------------------------|
| 1  | Expense Amount     | NumericUpDown    | Yes      | Amount in JOD; 3 decimal places           |
| 2  | Expense Category   | ComboBox         | Yes      | "مشتريات" / "صيانة" / "نقل" / "قرطاسية" / "فواتير" / "أخرى" |
| 3  | Description        | TextBox          | Yes      | Reason for expense                        |
| 4  | Receipt Reference  | TextBox          | No       | External receipt or invoice number        |
| 5  | Current Shift Info | Label (read-only)| -        | Shows active shift details                |

## EXACT BUTTONS

| #  | Button              | Action                                                |
|----|---------------------|-------------------------------------------------------|
| 1  | ✅ تسجيل المصروف     | Validates and saves the expense                      |
| 2  | ❌ إلغاء             | Closes without saving                                 |

## EXPENSE CATEGORIES

| Category | Description                    |
|----------|--------------------------------|
| مشتريات  | Purchases for the business     |
| صيانة    | Maintenance and repairs        |
| نقل      | Transportation and delivery    |
| قرطاسية  | Office supplies                |
| فواتير   | Utility bills                  |
| أخرى     | Miscellaneous                  |

## VALIDATION RULES

| Field       | Rule                                              |
|-------------|---------------------------------------------------|
| Amount      | Must be > 0                                       |
| Category    | Must be selected                                  |
| Description | Required; max 500 characters                      |

## UI STATES

| State     | Description                                          |
|-----------|------------------------------------------------------|
| Entering  | User filling in expense details                      |
| Saving    | Expense being saved                                  |
| Complete  | "تم تسجيل المصروف بنجاح" message                     |
| Error     | Validation or save error                             |

## ACCEPTANCE CRITERIA

1. **AC-001:** Expense is recorded against the currently open shift.
2. **AC-002:** Amount must be > 0; validation prevents zero or negative.
3. **AC-003:** Category and description are required.
4. **AC-004:** Expense reduces the shift's expected closing cash.
5. **AC-005:** Expense appears in the shift report.
6. **AC-006:** Only one expense can be recorded per submission (no bulk entry).
