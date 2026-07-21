# SCREEN-004: Payment Dialog

## PURPOSE
Process customer payments for a completed sale. Supports multiple payment methods, split payments, and change calculation.

## PERMISSIONS
- `Sell` — Required to process payments

## EXACT FIELDS

| #  | Field                   | Type          | Notes                                           |
|----|-------------------------|---------------|-------------------------------------------------|
| 1  | Sale Summary            | Label         | Shows invoice number and date                   |
| 2  | Total Amount            | Label         | "الإجمالي المستحق" - formatted as JOD 0.000     |
| 3  | Amount Received         | NumericUpDown | Input for cash amount; 3 decimal places          |
| 4  | Change Due              | Label         | "الباقي" - auto-calculated, formatted as JOD 0.000 |
| 5  | Payment Method Selector | ComboBox      | Options: نقداً / بطاقة / محفظة إلكترونية / آجل   |
| 6  | Customer Selector       | ComboBox      | For credit sales ("آجل") - required if credit    |

## EXACT BUTTONS

| #  | Button              | Shortcut | Action                                                    |
|----|---------------------|----------|-----------------------------------------------------------|
| 1  | ✅ تأكيد الدفع       | Enter    | Processes payment, finalizes sale, prints receipt          |
| 2  | ❌ إلغاء              | Escape   | Closes dialog without processing; returns to cart         |
| 3  | 💵 Cash Quick Amounts | -        | Buttons for 5, 10, 20, 50, 100 JOD quick-entry           |

## QUICK CASH AMOUNTS

| Button | Value  |
|--------|--------|
| ٥ JOD  | 5.000  |
| ١٠ JOD | 10.000 |
| ٢٠ JOD | 20.000 |
| ٥٠ JOD | 50.000 |
| ١٠٠ JOD| 100.000|

## UI STATES

| State           | Description                                       |
|-----------------|---------------------------------------------------|
| EnterAmount     | Waiting for payment amount entry                  |
| ExactChange     | Amount entered exactly equals total               |
| ChangeDue       | Overpayment; change amount displayed              |
| Insufficient    | Underpayment; remaining balance shown in red      |
| Processing      | Payment being processed (button disabled)         |
| Complete        | Payment successful; dialog closes                  |
| Error           | Payment failed; error message shown               |

## ACCEPTANCE CRITERIA

1. **AC-001:** Total amount displayed prominently in JOD with 3 decimals.
2. **AC-002:** Entering exact amount shows "المبلغ تمام" with no change.
3. **AC-003:** Entering more than total auto-calculates and displays change.
4. **AC-004:** Entering less than total shows remaining balance in red.
5. **AC-005:** Quick cash amount buttons set the received amount instantly.
6. **AC-006:** Selecting "آجل" (credit) requires customer selection.
7. **AC-007:** Successful payment prints receipt and updates the POS terminal state.
8. **AC-008:** Canceling returns to cart with all items intact.
9. **AC-009:** Keyboard: Enter confirms, Escape cancels.
