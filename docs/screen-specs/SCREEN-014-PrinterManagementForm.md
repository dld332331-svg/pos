# SCREEN-014: Printer Management (إدارة الطابعات)

## PURPOSE
Configure and manage connected printers. Assign printer roles (receipt, kitchen, label, report), test connections, and monitor printer status.

## PERMISSIONS
- `ManageUsers` — Required to manage printer configuration

## EXACT FIELDS

| #  | Field                 | Type          | Notes                                        |
|----|-----------------------|---------------|----------------------------------------------|
| 1  | Printer Name          | RtlTextBox    | Friendly name for the printer                |
| 2  | Connection Type       | ComboBox      | "USB" / "Network" / "Bluetooth"              |
| 3  | IP Address / Port     | TextBox       | Network printer address (if network)         |
| 4  | Printer Role          | ComboBox      | "فاتورة" / "مطبخ" / "باركود" / "تقارير"     |
| 5  | Printer Type          | ComboBox      | "حرارية (80mm)" / "حرارية (58mm)" / "ليزر" |
| 6  | Kitchen Station       | ComboBox      | Assign to kitchen station (if role = kitchen)|
| 7  | Printer Status        | Label/Badge   | "متصل" (Online) / "غير متصل" (Offline)       |
| 8  | Printers Grid         | DataGridView  | List of configured printers                   |

## EXACT BUTTONS

| #  | Button                | Action                                               |
|----|-----------------------|------------------------------------------------------|
| 1  | ➕ إضافة طابعة         | Add new printer configuration                        |
| 2  | ✏️ تعديل              | Edit selected printer                                |
| 3  | 🗑️ حذف                | Delete printer configuration                         |
| 4  | 🔄 اختبار الاتصال      | Test printer connection and print test page          |
| 5  | 🖨️ طباعة اختبار       | Print a test receipt/kitchen ticket                  |
| 6  | 🔄 تحديث               | Refresh printer list and statuses                    |

## GRID COLUMNS

| #  | Column Name  | Format       | Notes                 |
|----|--------------|--------------|-----------------------|
| 1  | #            | Integer      | Row number            |
| 2  | الاسم        | Text         | Printer name          |
| 3  | النوع        | Text         | Connection type       |
| 4  | الدور        | Badge        | Printer role          |
| 5  | IP / المنفذ  | Text         | Network address       |
| 6  | الحالة       | Badge (Online/Offline) | Connection status |

## KITCHEN STATIONS SECTION

| #  | Field                 | Type          | Notes                                     |
|----|-----------------------|---------------|-------------------------------------------|
| 1  | Station Name          | RtlTextBox    | Name of kitchen station                   |
| 2  | Station Grid          | DataGridView  | List of stations with assigned printers   |
| 3  | ➕ إضافة محطة         | Button        | Add new kitchen station                   |

## UI STATES

| State     | Description                                         |
|-----------|-----------------------------------------------------|
| Loading   | Printer list being loaded                           |
| Loaded    | Printers displayed in grid                          |
| Testing   | Printer connection test in progress                 |
| Empty     | No printers configured                              |
| Error     | Failed to load printer list                         |

## ACCEPTANCE CRITERIA

1. **AC-001:** All configured printers are displayed with their roles and statuses.
2. **AC-002:** Test connection verifies the printer is reachable and shows success/failure.
3. **AC-003:** Test print sends sample content to the selected printer.
4. **AC-004:** Kitchen printers are assigned to specific kitchen stations.
5. **AC-005:** Printer status updates after test connection.
6. **AC-006:** Cannot assign a printer with a role already assigned (one role per printer).
7. **AC-007:** Deleting a printer requires confirmation.
