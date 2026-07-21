# SCREEN-017: Audit Log (سجل العمليات)

## PURPOSE
View and search the system audit log for security and compliance. Track all user actions including sales, modifications, deletions, and configuration changes.

## PERMISSIONS
- `ManageUsers` — Required to view audit logs

## EXACT FIELDS

| #  | Field              | Type          | Notes                                        |
|----|--------------------|---------------|----------------------------------------------|
| 1  | Date From          | DateTimePicker| Start of date range filter                   |
| 2  | Date To            | DateTimePicker| End of date range filter                     |
| 3  | User Filter        | ComboBox      | Filter by user ("الكل" / specific user)      |
| 4  | Action Type Filter | ComboBox      | Filter by action type                        |
| 5  | Search Text        | TextBox       | Free-text search in details                  |
| 6  | Results Grid       | DataGridView  | Audit log entries                             |
| 7  | Total Count Label  | Label         | "إجمالي النتائج: X"                          |

## EXACT BUTTONS

| #  | Button            | Action                                               |
|----|-------------------|------------------------------------------------------|
| 1  | 🔍 بحث            | Applies filters and searches                         |
| 2  | 🔄 تحديث          | Reloads audit log                                    |
| 3  | 📥 تصدير           | Exports filtered results to Excel/CSV                |

## GRID COLUMNS

| #  | Column Name | Format         | Notes                 |
|----|-------------|----------------|-----------------------|
| 1  | #           | Integer        | Row number            |
| 2  | التاريخ     | DateTime       | Action timestamp      |
| 3  | المستخدم    | Text           | User who performed    |
| 4  | نوع العملية | Text/Badge     | Action type           |
| 5  | التفاصيل    | Text           | Action description    |
| 6  | عنوان IP    | Text           | Client IP address     |

## ACTION TYPES

| Type                   | Badge Color | Description                    |
|------------------------|-------------|--------------------------------|
| Login                  | 🟢          | User login                     |
| Logout                 | 🔵          | User logout                    |
| Create                 | 🟢          | Entity created                 |
| Update                 | 🟡          | Entity modified                |
| Delete                 | 🔴          | Entity deleted                 |
| Sale                   | 🟢          | Sale completed                 |
| Payment                | 🔵          | Payment processed              |
| Refund                 | 🟡          | Refund issued                  |
| Void                   | 🔴          | Invoice voided                 |
| Backup                 | 🟢          | Backup created                 |
| Restore                | 🟡          | Backup restored                |
| Configuration Change   | 🟡          | Settings changed               |
| Permission Change      | 🔴          | User permissions modified      |

## UI STATES

| State     | Description                                          |
|-----------|------------------------------------------------------|
| Loading   | Audit log being fetched                              |
| Loaded    | Results displayed in grid                            |
| Empty     | No entries match the current filters                 |
| Exporting | Data being exported                                  |
| Error     | Failed to load audit log                             |

## ACCEPTANCE CRITERIA

1. **AC-001:** Audit log shows all actions with timestamps and user details.
2. **AC-002:** Date range filter defaults to last 7 days.
3. **AC-003:** Action type filter uses a categorized dropdown with all action types.
4. **AC-004:** Free-text search searches across the Details column.
5. **AC-005:** Export downloads data matching current filters (not entire log).
6. **AC-006:** Grid is sorted by date descending (newest first).
7. **AC-007:** Results are paginated (100 entries per page).
8. **AC-008:** IP address is captured automatically during API/service calls.
