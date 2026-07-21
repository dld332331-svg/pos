# SCREEN-007: Product Form (إضافة/تعديل منتج)

## PURPOSE
Create or edit a product with full details including pricing, category, stock levels, and tax configuration.

## PERMISSIONS
- `EditProducts` — Required to create/edit products
- `EditPrices` — Required to change price fields

## EXACT FIELDS

| #  | Field               | Type          | Required | Notes                                      |
|----|---------------------|---------------|----------|--------------------------------------------|
| 1  | Product Name (Arabic)| RtlTextBox    | Yes      | Primary name in Arabic                     |
| 2  | Product Name (English)| TextBox      | No       | Secondary name in English                  |
| 3  | SKU / Barcode       | TextBox       | No       | Unique product identifier                  |
| 4  | Category            | ComboBox      | Yes      | Populated from categories                  |
| 5  | Product Type        | ComboBox      | Yes      | "سلعة" (Good) or "خدمة" (Service)          |
| 6  | Sale Price          | RtlNumericUpDown | Yes   | Selling price; 3 decimal places (JOD)      |
| 7  | Cost Price          | RtlNumericUpDown | No    | Purchase cost; 3 decimal places (JOD)      |
| 8  | Tax Rate            | NumericUpDown | No       | Percentage: 0 = no tax                     |
| 9  | Minimum Stock       | NumericUpDown | No       | Alert threshold                            |
| 10 | Current Stock       | NumericUpDown | No       | Initial stock quantity                     |
| 11 | Status              | ComboBox      | Yes      | "نشط" (Active) / "غير نشط" (Inactive)      |
| 12 | Description         | TextBox       | No       | Multi-line product description             |
| 13 | Image Preview       | PictureBox    | No       | Product image (optional)                   |

## EXACT BUTTONS

| #  | Button          | Action                                                   |
|----|-----------------|----------------------------------------------------------|
| 1  | 💾 حفظ (Save)   | Validates and saves product; closes form                 |
| 2  | ❌ إلغاء (Cancel)| Closes without saving                                    |
| 3  | 🖼️ اختيار صورة  | Opens file dialog to select product image                |

## VALIDATION RULES

| Field           | Rule                                              |
|-----------------|---------------------------------------------------|
| Arabic Name     | Required; max 200 characters                      |
| Sale Price      | Required; must be >= 0                            |
| Cost Price      | Must be >= 0 if provided                          |
| SKU             | Must be unique if provided                        |
| Min Stock       | Must be >= 0                                      |

## UI STATES

| State       | Description                                           |
|-------------|-------------------------------------------------------|
| Create Mode | All fields empty; title shows "إضافة منتج جديد"        |
| Edit Mode   | Fields pre-filled with product data; title shows "تعديل المنتج" |
| Saving      | Save button disabled; processing indicator            |
| ValidationError | Red border on invalid fields; error message shown |
| SaveSuccess | Product saved; brief success message; form closes     |

## ACCEPTANCE CRITERIA

1. **AC-001:** Create mode has empty fields; Edit mode pre-fills with existing data.
2. **AC-002:** Save validates required fields and shows inline errors.
3. **AC-003:** Arabic name is required; English name is optional.
4. **AC-004:** SKU uniqueness is checked before saving.
5. **AC-005:** Sale price and cost price display with 3 decimal places.
6. **AC-006:** Canceling with unsaved changes shows "هل تريد الحفظ قبل الإغلاق؟".
7. **AC-007:** Saving closes the form and refreshes the product list.
8. **AC-008:** Image selection shows preview before saving.
