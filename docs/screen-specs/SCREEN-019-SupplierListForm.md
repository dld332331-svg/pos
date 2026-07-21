# SCREEN-019: Supplier Management (إدارة الموردين)

## PURPOSE
Manage supplier records for purchase order processing and inventory procurement.

## PERMISSIONS
- `ManageSuppliers` — Required to manage suppliers

## EXACT FIELDS

| #  | Field               | Type          | Notes                                      |
|----|---------------------|---------------|--------------------------------------------|
| 1  | Search Box          | RtlTextBox    | Search by name, contact, or phone          |
| 2  | Suppliers Grid      | DataGridView  | List of suppliers with details             |
| 3  | Total Count Label   | Label         | "إجمالي الموردين: X"                       |

## SUPPLIER FORM FIELDS (Add/Edit)

| #  | Field                | Type          | Required | Notes                      |
|----|----------------------|---------------|----------|----------------------------|
| 1  | Supplier Name        | RtlTextBox    | Yes      | Supplier/company name      |
| 2  | Contact Person       | RtlTextBox    | No       | Person responsible         |
| 3  | Phone Number         | TextBox       | Yes      | Primary contact number     |
| 4  | Email                | TextBox       | No       | Email address              |
| 5  | Address              | TextBox       | No       | Full address               |
| 6  | Tax Number           | TextBox       | No       | VAT/Tax registration       |
| 7  | Payment Terms        | ComboBox      | No       | "نقداً" / "30 يوم" / "60 يوم" / "90 يوم" |
| 8  | Status               | ComboBox      | Yes      | "نشط" / "موقف"             |
| 9  | Notes                | TextBox       | No       | Internal notes             |

## EXACT BUTTONS

| #  | Button                | Action                                               |
|----|-----------------------|------------------------------------------------------|
| 1  | ➕ إضافة مورد          | Opens SupplierForm in create mode                    |
| 2  | ✏️ تعديل              | Opens SupplierForm in edit mode                      |
| 3  | 📋 طلبات الشراء       | View purchase orders for this supplier               |
| 4  | 🗑️ حذف                | Delete supplier after confirmation                    |
| 5  | 🔄 تحديث               | Refresh supplier list                                 |

## GRID COLUMNS

| #  | Column Name     | Format              | Notes                 |
|----|-----------------|---------------------|-----------------------|
| 1  | #               | Integer             | Row number            |
| 2  | الاسم           | Text                | Supplier name         |
| 3  | جهة الاتصال     | Text                | Contact person        |
| 4  | الهاتف          | Text                | Phone number          |
| 5  | طلبات الشراء    | Integer             | Count of open POs     |
| 6  | الحالة          | Badge (نشط/موقف)    | Supplier status       |

## UI STATES

| State     | Description                                          |
|-----------|------------------------------------------------------|
| Loading   | Supplier list being fetched                          |
| Loaded    | Suppliers displayed in grid                          |
| Empty     | No suppliers found matching search                   |
| Saving    | Supplier data being saved                            |
| Error     | Failed to load or save supplier                      |

## ACCEPTANCE CRITERIA

1. **AC-001:** Supplier list loads and displays all suppliers.
2. **AC-002:** Search filters by name, contact person, or phone.
3. **AC-003:** Adding a supplier requires at least name and phone.
4. **AC-004:** Editing supplier updates existing record.
5. **AC-005:** View purchase orders links to supplier-specific PO list.
6. **AC-006:** Deleting a supplier requires confirmation.
7. **AC-007:** Inactive suppliers are hidden from new purchase order selection.
