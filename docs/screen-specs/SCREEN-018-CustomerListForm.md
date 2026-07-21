# SCREEN-018: Customer Management (إدارة العملاء)

## PURPOSE
Manage customer records for credit sales, loyalty tracking, and customer relationship management.

## PERMISSIONS
- `ManageUsers` — Required to manage customers

## EXACT FIELDS

| #  | Field               | Type          | Notes                                      |
|----|---------------------|---------------|--------------------------------------------|
| 1  | Search Box          | RtlTextBox    | Search by name, phone, or loyalty card     |
| 2  | Customers Grid      | DataGridView  | List of customers with details             |
| 3  | Customer Total Label| Label         | "إجمالي العملاء: X"                        |
| 4  | Credit Total Label  | Label         | "إجمالي الديون: X.XXX JOD" (if any)       |

## CUSTOMER FORM FIELDS (Add/Edit)

| #  | Field                | Type          | Required | Notes                      |
|----|----------------------|---------------|----------|----------------------------|
| 1  | Customer Name        | RtlTextBox    | Yes      | Full name                  |
| 2  | Phone Number         | TextBox       | Yes      | Primary contact number     |
| 3  | Email                | TextBox       | No       | Email address              |
| 4  | Address              | TextBox       | No       | Full address               |
| 5  | Credit Limit         | NumericUpDown | No       | Max credit; JOD 0.000      |
| 6  | Current Balance      | Label (read-only) | No | Current outstanding balance |
| 7  | Notes                | TextBox       | No       | Internal notes             |
| 8  | Status               | ComboBox      | Yes      | "نشط" / "موقف"             |

## EXACT BUTTONS

| #  | Button                | Action                                               |
|----|-----------------------|------------------------------------------------------|
| 1  | ➕ إضافة عميل          | Opens customer form in create mode                   |
| 2  | ✏️ تعديل              | Opens customer form in edit mode                     |
| 3  | 💰 سداد دين            | Record payment against customer balance              |
| 4  | 📋 سجل المشتريات      | View customer purchase history                       |
| 5  | 🗑️ حذف                | Delete customer after confirmation                    |
| 6  | 🔄 تحديث               | Refresh customer list                                 |

## GRID COLUMNS

| #  | Column Name     | Format              | Notes                |
|----|-----------------|---------------------|----------------------|
| 1  | #               | Integer             | Row number           |
| 2  | الاسم           | Text                | Customer name        |
| 3  | الهاتف          | Text                | Phone number         |
| 4  | الرصيد          | JOD 0.000           | Current balance      |
| 5  | الحد الائتماني  | JOD 0.000           | Credit limit         |
| 6  | الحالة          | Badge (نشط/موقف)     | Customer status      |
| 7  | آخر شراء        | DateTime            | Last purchase date   |

## UI STATES

| State     | Description                                          |
|-----------|------------------------------------------------------|
| Loading   | Customer list being fetched                          |
| Loaded    | Customers displayed in grid                          |
| Empty     | No customers found matching search                   |
| Saving    | Customer data being saved                            |
| Error     | Failed to load or save customer                      |

## ACCEPTANCE CRITERIA

1. **AC-001:** Customer list loads and displays all customers.
2. **AC-002:** Search filters by name or phone number.
3. **AC-003:** Adding a customer requires at least name and phone.
4. **AC-004:** Credit balance updates after payments or credit sales.
5. **AC-005:** Payment against debt records a transaction and updates balance.
6. **AC-006:** Purchase history shows all invoices for the selected customer.
7. **AC-007:** Deleting a customer requires confirmation; blocked if outstanding balance.
8. **AC-008:** Credit-limited customers show warning when exceeded during sale.
