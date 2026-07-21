# SCREEN-010: Shift Management (إدارة الورديات)

## PURPOSE
Open and close cash register shifts. Manage shift handover, record opening/closing cash amounts, and track shift variances.

## PERMISSIONS
- `ViewReports` — Required to view shift information

## EXACT FIELDS

| #  | Field               | Type          | Notes                                        |
|----|---------------------|---------------|----------------------------------------------|
| 1  | Shift Status        | Label         | "مفتوحة" / "مغلقة" with color indicator      |
| 2  | Current Shift Info  | Label Panel   | Supervisor name, start time, duration        |
| 3  | Opening Cash        | NumericUpDown | Cash amount at shift start; JOD 0.000        |
| 4  | Closing Cash        | NumericUpDown | Cash amount at shift end; JOD 0.000          |
| 5  | Expected Cash       | Label         | Auto-calculated: opening + sales - expenses  |
| 6  | Variance            | Label         | Difference between closing and expected      |
| 7  | Shift History Grid  | DataGridView  | Past shifts with summary                     |
| 8  | Sales Summary       | Label Panel   | Sales count, total, returns during shift     |

## EXACT BUTTONS

| #  | Button              | Action                                                 |
|----|---------------------|--------------------------------------------------------|
| 1  | 🔓 فتح وردية جديدة   | Opens new shift with opening cash entry                |
| 2  | 🔒 إنهاء الوردية     | Closes current shift; records closing cash              |
| 3  | 💵 إيداع/سحب         | Opens WithdrawalDepositDialog                          |
| 4  | 📋 مصروفات           | Opens ExpenseDialog                                    |
| 5  | 🖨️ طباعة تقرير الوردية | Prints shift summary report                          |

## UI STATES

| State          | Description                                         |
|----------------|-----------------------------------------------------|
| NoShift        | No active shift; only "Open Shift" button enabled   |
| ShiftOpen      | Shift active; options for closing, expenses, etc.   |
| ShiftClosed    | Shift ended; summary displayed; variances shown     |
| Loading        | Shift data being retrieved                          |
| Error          | Failed to load shift data                           |

## ACCEPTANCE CRITERIA

1. **AC-001:** Opening a new shift records start time, user, and opening cash.
2. **AC-002:** Closing a shift requires entering closing cash amount.
3. **AC-003:** Variance is auto-calculated: Closing - (Opening + Sales - Withdrawals + Deposits - Expenses).
4. **AC-004:** Expenses and withdrawals can be recorded during an open shift.
5. **AC-005:** Shift history shows last 50 closed shifts with summaries.
6. **AC-006:** All monetary values displayed in JOD with 3 decimal places.
7. **AC-007:** Printing the shift report includes all financial activity during the shift.
8. **AC-008:** Cannot open a new shift while another is still open.
