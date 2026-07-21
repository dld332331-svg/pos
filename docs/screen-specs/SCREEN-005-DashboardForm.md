# SCREEN-005: Dashboard (لوحة التحكم)

## PURPOSE
Provide a visual overview of today's sales performance, key metrics, and quick access to common operations.

## PERMISSIONS
- `ViewDashboard` — Required to view the dashboard

## EXACT FIELDS

| #  | Field                  | Type  | Notes                                                |
|----|------------------------|-------|------------------------------------------------------|
| 1  | Today's Sales Total    | Label | إجمالي مبيعات اليوم - large font, JOD 0.000          |
| 2  | Transaction Count      | Label | عدد المعاملات اليوم                                   |
| 3  | Average Sale Value     | Label | متوسط قيمة الفاتورة - JOD 0.000                      |
| 4  | Current Shift Info     | Label | معلومات الوردية الحالية (المشرف، الوقت)              |
| 5  | Open Invoices          | Label | عدد الفواتير المفتوحة                                 |
| 6  | Low Stock Items        | Label | عدد المنتجات منخفضة المخزون                            |
| 7  | Top Selling Products   | Label | أفضل المنتجات مبيعاً                                   |
| 8  | Sales Chart            | PictureBox/Chart | Column/line chart showing hourly/daily sales |

## EXACT BUTTONS

| #  | Button                     | Action                                      |
|----|----------------------------|---------------------------------------------|
| 1  | 🔄 تحديث (Refresh)         | Reloads all dashboard data                 |
| 2  | 📊 تقرير مفصل (Full Report)| Opens ReportForm with today's data         |
| 3  | 👤 فتح وردية (Open Shift)  | Opens ShiftForm to start a new shift       |

## UI STATES

| State    | Description                                          |
|----------|------------------------------------------------------|
| Loading  | Data is being fetched from `IDashboardService`       |
| Loaded   | All metrics displayed with data                      |
| Empty    | No sales data for today; zero values shown           |
| Error    | Failed to load data; retry message displayed         |

## ACCEPTANCE CRITERIA

1. **AC-001:** Dashboard loads and displays today's sales summary within 2 seconds.
2. **AC-002:** All monetary values show in JOD with 3 decimal places.
3. **AC-003:** Low stock items are highlighted in red if stock < MinStock.
4. **AC-004:** Sales chart updates hourly or on manual refresh.
5. **AC-005:** Clicking "Full Report" opens ReportForm pre-filtered for today.
6. **AC-006:** Dashboard auto-refreshes every 60 seconds.
7. **AC-007:** Loading state shows skeleton or spinner; error state shows retry option.
