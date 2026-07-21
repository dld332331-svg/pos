# SCREEN-015: Kitchen Display (شاشة المطبخ)

## PURPOSE
Display incoming food orders to the kitchen staff in real-time. Shows order items with modifiers, quantities, and preparation status.

## PERMISSIONS
- `ManageUsers` — Required to access kitchen display

## EXACT FIELDS

| #  | Field                | Type          | Notes                                        |
|----|----------------------|---------------|----------------------------------------------|
| 1  | Orders Grid          | FlowLayout/Panel | Order cards displayed in columns        |
| 2  | Order Card           | Panel         | Individual order with items, table, time     |
| 3  | New Orders Badge     | Label/Badge   | Count of new orders since last view           |
| 4  | Kitchen Station Label| Label         | Current station name (if multiple)            |

## ORDER CARD ELEMENTS

| Element         | Description                                |
|-----------------|--------------------------------------------|
| Order #         | "طلب #1234" - large font                    |
| Table/Type      | "طاولة 5" or "توصيل" or "استلام"            |
| Time Elapsed    | "منذ 15 دقيقة" - color coded (red if >30 min)|
| Items           | List of items with modifiers                |
| Quantity Badge  | x2, x3 for multi-qty items                  |
| Notes           | Special instructions in red                  |
| Status Buttons  | "جارٍ التحضير" / "جاهز" / "تم التوصيل"       |
| Priority        | Highlight for urgent orders                  |

## EXACT BUTTONS

| #  | Button               | Action                                               |
|----|----------------------|------------------------------------------------------|
| 1  | ✅ بدء التحضير        | Mark order as "In Progress"                          |
| 2  | ✅ تجهيز (Complete)   | Mark order as ready for serving                      |
| 3  | ✅ تم التوصيل (Served)| Mark order as delivered to table                     |
| 4  | 🔄 تحديث              | Refresh orders list                                  |
| 5  | 🔊 تنبيه صوتي         | Play notification sound for new orders               |

## COLOR CODING

| Status           | Color       | Description                     |
|------------------|-------------|---------------------------------|
| New              | 🟡 Yellow   | Just received, not started      |
| In Progress      | 🔵 Blue     | Being prepared                  |
| Ready            | 🟢 Green    | Ready to serve                  |
| Served           | ⚪ Gray     | Delivered; fades out            |
| Priority         | 🔴 Red      | Order older than 30 minutes     |

## UI STATES

| State         | Description                                         |
|---------------|-----------------------------------------------------|
| Loading       | Orders being loaded                                  |
| Active        | Orders displayed; real-time updates expected         |
| Empty         | No pending orders                                    |
| Error         | Failed to load orders                                |

## ACCEPTANCE CRITERIA

1. **AC-001:** New orders appear automatically without manual refresh.
2. **AC-002:** Orders are sorted by time received (oldest first).
3. **AC-003:** Items with modifiers show modifier details (e.g., "برجر - إضافي جبن").
4. **AC-004:** Time elapsed is color-coded: green (<10min), yellow (10-30min), red (>30min).
5. **AC-005:** Sound notification plays when new order arrives.
6. **AC-006:** Marking as "Ready" moves order to completed section.
7. **AC-007:** Completed orders auto-clear after 10 minutes.
8. **AC-008:** Failed printer orders show a retry button.
