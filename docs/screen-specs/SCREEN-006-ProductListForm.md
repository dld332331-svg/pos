# SCREEN-006: Product List (قائمة المنتجات)

## PURPOSE
Browse, search, and manage all products. Provides CRUD operations for products with inventory tracking.

## PERMISSIONS
- `EditProducts` — Required to view and manage products
- `EditPrices` — Required to change product prices

## EXACT FIELDS

| #  | Field                | Type         | Notes                                         |
|----|----------------------|--------------|-----------------------------------------------|
| 1  | Search Box           | RtlTextBox   | Search by name (Arabic/English) or SKU        |
| 2  | Category Filter      | ComboBox     | Filter by category                            |
| 3  | Status Filter        | ComboBox     | "الكل" / "نشط" / "غير نشط"                     |
| 4  | Products Grid        | DataGridView | Columns: #, الاسم, السعر, المخزون, الحالة, SKU |
| 5  | Total Count Label    | Label        | "إجمالي المنتجات: X"                          |

## EXACT BUTTONS

| #  | Button               | Permission      | Action                                    |
|----|----------------------|-----------------|-------------------------------------------|
| 1  | ➕ إضافة منتج جديد    | EditProducts    | Opens ProductForm in Create mode          |
| 2  | ✏️ تعديل             | EditProducts    | Opens ProductForm in Edit mode            |
| 3  | 🗑️ حذف               | EditProducts    | Deletes product after confirmation         |
| 4  | 📦 تعديل السعر        | EditPrices      | Opens price edit dialog                    |
| 5  | 🔄 تحديث              | -               | Reloads product list                      |

## GRID COLUMNS

| #  | Column Name | Format              | Notes               |
|----|-------------|---------------------|---------------------|
| 1  | #           | Integer             | Row number           |
| 2  | الاسم       | Text                | Product name (AR/EN) |
| 3  | السعر       | JOD 0.000           | Current sale price   |
| 4  | المخزون     | Integer/Text        | "3" or "غير متوفر"   |
| 5  | الحالة      | Badge (Active/Inactive) | Colored indicator  |
| 6  | SKU         | Text                | Stock keeping unit   |
| 7  | الفئة       | Text                | Category name        |

## UI STATES

| State     | Description                                          |
|-----------|------------------------------------------------------|
| Loading   | Products being fetched from `IProductService`        |
| Loaded    | Products displayed in grid                           |
| Empty     | No products match filter / search                    |
| Error     | Failed to load; error message shown                  |

## ACCEPTANCE CRITERIA

1. **AC-001:** Products grid loads and displays with all columns visible.
2. **AC-002:** Search filters grid in real-time after 2 characters typed.
3. **AC-003:** Category and status filters can be combined.
4. **AC-004:** Adding a new product opens ProductForm and refreshes grid on save.
5. **AC-005:** Deleting a product shows confirmation "هل أنت متأكد من الحذف؟".
6. **AC-006:** Price editing requires `EditPrices` permission.
7. **AC-007:** All prices displayed in JOD with 3 decimal places.
8. **AC-008:** Double-click on a row opens ProductForm in Edit mode.
