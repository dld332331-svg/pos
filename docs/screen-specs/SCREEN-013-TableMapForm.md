# SCREEN-013: Table Map (خريطة الطاولات)

## PURPOSE
Visual representation of the restaurant's table layout. Manage table status (available, occupied, reserved), assign orders to tables, and transfer tables.

## PERMISSIONS
- `ManageTables` — Required to manage tables

## EXACT FIELDS

| #  | Field              | Type          | Notes                                        |
|----|--------------------|---------------|----------------------------------------------|
| 1  | Table Canvas       | Panel/FlowLayout | Visual grid of table buttons             |
| 2  | Legend Panel       | Panel         | Color legend for statuses                    |
| 3  | Statistics Bar     | Label         | "X مشغولة / Y شاغرة / Z محجوزة"             |
| 4  | Room Selector      | ComboBox      | Filter by room/section                       |

## TABLE BUTTONS (Dynamically generated)

Each table is represented by a clickable button/panel showing:

| Element      | Description                              |
|--------------|------------------------------------------|
| Table Number | Large text: "طاولة 5"                     |
| Status       | Color indicator: أخضر=شاغرة, أحمر=مشغولة, أصفر=محجوزة, رمادي=غير مفعلة |
| Guest Count  | If occupied: "3 ضيوف"                     |
| Duration     | If occupied: "45 دقيقة"                   |
| Order Total  | If occupied: JOD 0.000                    |

## EXACT BUTTONS

| #  | Button                | Action                                           |
|----|-----------------------|--------------------------------------------------|
| 1  | ➕ إضافة طاولة         | Adds a new table to the map                      |
| 2  | ✏️ تعديل الطاولة      | Edit table name, capacity, location              |
| 3  | 🔄 نقل الطاولة        | Transfer order to another table                  |
| 4  | 📋 دمج الطاولات        | Merge two tables into a combined order           |
| 5  | 🔄 تحديث               | Refreshes table statuses                          |

## CONTEXT MENU (Right-click on table)

| Option             | Action                                      |
|--------------------|---------------------------------------------|
| فتح طلب             | Open POS terminal with this table           |
| عرض الفاتورة        | View current invoice for this table         |
| نقل الطاولة         | Transfer order to another table             |
| تغيير الحالة        | Manually change table status                |
| حذف الطاولة         | Remove table from map                       |

## UI STATES

| State         | Description                                         |
|---------------|-----------------------------------------------------|
| Loading       | Table layout being loaded                           |
| Loaded        | Table map displayed with all tables                 |
| Empty         | No tables configured; prompt to add first table     |
| Error         | Failed to load table data                           |

## ACCEPTANCE CRITERIA

1. **AC-001:** Tables are displayed in a grid layout matching the physical restaurant layout.
2. **AC-002:** Table colors update in real-time when status changes.
3. **AC-003:** Clicking an occupied table shows order details.
4. **AC-004:** Clicking an available table opens POS terminal for new order.
5. **AC-005:** Tables can be assigned to rooms/sections.
6. **AC-006:** Transfer order moves all items to another table with audit trail.
7. **AC-007:** Merging tables requires confirmation.
8. **AC-008:** Statistics bar updates dynamically.
9. **AC-009:** Legend shows color meanings for all statuses.
