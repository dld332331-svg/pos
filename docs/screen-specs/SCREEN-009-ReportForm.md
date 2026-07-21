# SCREEN-009: Reports (التقارير)

## PURPOSE
View and generate sales, financial, and inventory reports with customizable date ranges and filters.

## PERMISSIONS
- `ViewReports` — Required to access reports

## EXACT FIELDS

| #  | Field              | Type         | Notes                                         |
|----|--------------------|--------------|-----------------------------------------------|
| 1  | Report Type        | ComboBox     | "المبيعات" / "المخزون" / "الأرباح" / "النقدية"  |
| 2  | Date From          | DateTimePicker| Start date for report range                    |
| 3  | Date To            | DateTimePicker| End date for report range                      |
| 4  | Category Filter    | ComboBox     | Optional product category filter               |
| 5  | Payment Method     | ComboBox     | "الكل" / "نقداً" / "بطاقة" / "آجل"             |
| 6  | Report Preview     | RichTextBox/Panel | Rendered report output area              |

## EXACT BUTTONS

| #  | Button             | Action                                              |
|----|--------------------|-----------------------------------------------------|
| 1  | 📊 عرض التقرير      | Generates and displays the report                   |
| 2  | 🖨️ طباعة           | Sends report to printer                             |
| 3  | 📥 تصدير PDF        | Exports report as PDF file                          |
| 4  | 📥 تصدير Excel      | Exports report as Excel file                        |
| 5  | 🔄 تحديث            | Refreshes report data                               |

## REPORT TYPES

| Type       | Description                                | Key Metrics                                     |
|------------|--------------------------------------------|-------------------------------------------------|
| المبيعات   | Sales report by date range                 | Total sales, count, avg per invoice, by payment |
| المخزون    | Inventory valuation and stock levels       | Stock value, low stock items, movement summary  |
| الأرباح    | Profitability analysis                     | Revenue, cost, profit margin, by product        |
| النقدية    | Cash flow and drawer summary               | Opening cash, sales, expenses, withdrawals, closing |

## UI STATES

| State      | Description                                        |
|------------|----------------------------------------------------|
| Idle       | No report shown; defaults set                     |
| Loading    | Report being generated                             |
| Loaded     | Report displayed in preview area                   |
| Empty      | No data for selected filters                       |
| Exporting  | Report being exported to file                      |
| Error      | Report generation failed                           |

## ACCEPTANCE CRITERIA

1. **AC-001:** Selecting report type and date range generates the correct report.
2. **AC-002:** All monetary values displayed in JOD with 3 decimal places.
3. **AC-003:** Printing sends formatted report to configured receipt/printer.
4. **AC-004:** PDF export includes header with company name, date range, and report title.
5. **AC-005:** Excel export includes all data rows and column headers.
6. **AC-006:** Empty date range defaults to current month.
7. **AC-007:** Reports can be previewed before printing/exporting.
