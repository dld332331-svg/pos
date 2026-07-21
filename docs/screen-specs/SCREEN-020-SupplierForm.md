# SCREEN-020: Supplier Form (إضافة/تعديل مورد)

## PURPOSE
Create or edit a supplier record for procurement and purchase order processing.

## PERMISSIONS
- `ManageSuppliers` — Required to create/edit suppliers

## EXACT FIELDS

| #  | Field                | Type          | Required | Notes                      |
|----|----------------------|---------------|----------|----------------------------|
| 1  | Supplier Name        | RtlTextBox    | Yes      | Supplier/company name      |
| 2  | Contact Person       | RtlTextBox    | No       | Person responsible         |
| 3  | Phone Number         | TextBox       | Yes      | Primary contact number     |
| 4  | Secondary Phone      | TextBox       | No       | Alternative contact        |
| 5  | Email                | TextBox       | No       | Email address              |
| 6  | Website              | TextBox       | No       | Company website            |
| 7  | Address              | TextBox       | No       | Full address               |
| 8  | Tax Number           | TextBox       | No       | VAT/Tax registration       |
| 9  | Payment Terms        | ComboBox      | No       | "نقداً" / "30 يوم" / "60 يوم" / "90 يوم" |
| 10 | Status               | ComboBox      | Yes      | "نشط" / "موقف"             |
| 11 | Notes                | TextBox       | No       | Internal notes             |

## EXACT BUTTONS

| #  | Button            | Action                                               |
|----|-------------------|------------------------------------------------------|
| 1  | 💾 حفظ             | Validates and saves supplier; closes form            |
| 2  | ❌ إلغاء            | Closes without saving                                |

## UI STATES

| State       | Description                                           |
|-------------|-------------------------------------------------------|
| Create Mode | All fields empty; title "إضافة مورد جديد"              |
| Edit Mode   | Fields pre-filled; title "تعديل المورد"               |
| Saving      | Save button disabled; data being saved                |
| SaveError   | Validation errors; field highlights                   |

## ACCEPTANCE CRITERIA

1. **AC-001:** Create mode has empty fields; Edit mode pre-fills with existing data.
2. **AC-002:** Name and Phone are required fields.
3. **AC-003:** Phone number format validation (at least 7 digits).
4. **AC-004:** Saving with missing required fields shows validation errors.
5. **AC-005:** Canceling with unsaved changes prompts confirmation.
6. **AC-006:** Status defaults to "نشط" for new suppliers.
