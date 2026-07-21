# SCREEN-008: Inventory Management (إدارة المخزون)

## PURPOSE
View and manage inventory levels across all products. Perform stock adjustments, track inventory movements, and monitor low-stock items.

## PERMISSIONS
- `AdjustInventory` — Required to view and manage inventory

## EXACT FIELDS

| #  | Field               | Type         | Notes                                       |
|----|---------------------|--------------|---------------------------------------------|
| 1  | Search Box          | RtlTextBox   | Search by product name or SKU               |
| 2  | Low Stock Filter    | CheckBox     | "إظهار المنتجات منخفضة المخزون فقط"          |
| 3  | Warehouse Selector  | ComboBox     | Location/warehouse filter (if applicable)   |
| 4  | Inventory Grid      | DataGridView | Columns: #, المنتج, الرصيد الحالي, الحد الأدنى, الحالة |
| 5  | Movement Log Grid   | DataGridView | Recent inventory movements (date, type, qty, user) |

## EXACT BUTTONS

| #  | Button                 | Action                                                |
|----|------------------------|-------------------------------------------------------|
| 1  | ➕ تسوية مخزون          | Opens StockAdjustmentDialog                           |
| 2  | 📊 تقرير المخزون        | Generates inventory report                            |
| 3  | 🔄 تحديث                | Refreshes inventory data                              |

## GRID COLUMNS (Inventory)

| #  | Column Name   | Format              | Notes                 |
|----|---------------|---------------------|-----------------------|
| 1  | #             | Integer             | Row number            |
| 2  | المنتج        | Text                | Product name          |
| 3  | الرصيد الحالي | Integer             | Current stock qty     |
| 4  | الحد الأدنى   | Integer             | Min stock threshold   |
| 5  | الحالة        | Badge (OK/Warning/Danger) | Green/Yellow/Red |
| 6  | آخر تحديث     | DateTime            | Last stock update     |

## UI STATES

| State     | Description                                        |
|-----------|----------------------------------------------------|
| Loading   | Inventory data being fetched                       |
| Loaded    | Grids populated with data                          |
| Empty     | No inventory items                                 |
| Error     | Failed to load; error message shown                |

## ACCEPTANCE CRITERIA

1. **AC-001:** Inventory grid shows current stock levels for all products.
2. **AC-002:** Low-stock items (Stock < MinStock) highlighted in red.
3. **AC-003:** Movement log shows last 50 inventory transactions.
4. **AC-004:** Stock adjustment opens dialog and records movement on save.
5. **AC-005:** Inventory report can be exported or printed.
6. **AC-006:** Low-stock filter toggle shows only items below minimum.
7. **AC-007:** All quantities displayed as whole numbers or 3-decimal as appropriate.
