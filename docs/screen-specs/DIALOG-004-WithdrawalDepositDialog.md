# DIALOG-004: Withdrawal / Deposit Dialog (إيداع / سحب نقدي)

## PURPOSE
Record cash withdrawals from or deposits into the register during an active shift. Used for cash management when money is removed from or added to the drawer (e.g., bank deposits, petty cash top-ups).

## PERMISSIONS
- Requires active shift

## EXACT FIELDS

| #  | Field                | Type             | Required | Notes                                      |
|----|----------------------|------------------|----------|--------------------------------------------|
| 1  | Transaction Type     | ComboBox         | Yes      | "إيداع" (Deposit) or "سحب" (Withdrawal)    |
| 2  | Amount               | NumericUpDown    | Yes      | Amount in JOD; 3 decimal places            |
| 3  | Reason               | ComboBox         | Yes      | "إيداع بنكي" / "سحب مصروفات" / "تسوية درج" / "أخرى" |
| 4  | Notes                | TextBox          | No       | Additional details                         |
| 5  | Current Shift Info   | Label (read-only)| -        | Shows active shift details                 |

## EXACT BUTTONS

| #  | Button               | Action                                                |
|----|----------------------|-------------------------------------------------------|
| 1  | ✅ تأكيد              | Validates and saves the transaction                   |
| 2  | ❌ إلغاء              | Closes without saving                                 |

## TRANSACTION TYPES

| Type       | Effect on cash drawer          |
|------------|--------------------------------|
| إيداع      | Increases expected cash total   |
| سحب        | Decreases expected cash total   |

## VALIDATION RULES

| Field       | Rule                                              |
|-------------|---------------------------------------------------|
| Amount      | Must be > 0                                       |
| Type        | Must be selected                                  |
| Reason      | Required                                          |

## UI STATES

| State     | Description                                          |
|-----------|------------------------------------------------------|
| Entering  | User selecting type and entering amount              |
| Saving    | Transaction being saved                              |
| Complete  | "تم تسجيل العملية بنجاح" message                     |
| Error     | Withdrawal amount exceeds available cash? warning     |

## ACCEPTANCE CRITERIA

1. **AC-001:** Transaction is recorded against the currently open shift.
2. **AC-002:** Deposit increases expected shift total; withdrawal decreases it.
3. **AC-003:** Amount must be > 0.
4. **AC-004:** Withdrawal cannot exceed total cash available in register.
5. **AC-005:** Transaction appears in shift report and audit log.
6. **AC-006:** Type and reason are required fields.
7. **AC-007:** Notes are optional but recommended for audit purposes.
