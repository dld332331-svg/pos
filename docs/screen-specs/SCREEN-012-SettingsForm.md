# SCREEN-012: Settings (الإعدادات)

## PURPOSE
Configure system-wide settings including company information, tax rates, receipt headers/footers, printer defaults, and application preferences.

## PERMISSIONS
- `ChangeSettings` — Required to view and modify settings

## EXACT FIELDS

| #  | Field                    | Type          | Notes                                      |
|----|--------------------------|---------------|--------------------------------------------|
| 1  | Company Name (Arabic)    | RtlTextBox    | اسم الشركة بالعربية                         |
| 2  | Company Name (English)   | TextBox       | Company name in English                    |
| 3  | Tax Number               | TextBox       | VAT / Tax registration number              |
| 4  | Tax Rate (%)             | NumericUpDown | Default tax percentage                     |
| 5  | Phone Number             | TextBox       | Company phone                              |
| 6  | Address                  | TextBox       | Company address                            |
| 7  | Receipt Header           | TextBox       | Custom header text for receipts             |
| 8  | Receipt Footer           | TextBox       | Custom footer text for receipts             |
| 9  | Default Printer          | ComboBox      | Default receipt printer                     |
| 10 | Currency Symbol          | Label/Text    | JOD (fixed)                                |
| 11 | Decimal Places           | Label/Text    | 3 (fixed)                                  |
| 12 | Auto-Backup Interval     | ComboBox      | "كل ساعة" / "كل 4 ساعات" / "كل 8 ساعات" / "كل 24 ساعة" |
| 13 | Language                 | ComboBox      | العربية / English                          |
| 14 | Receipt Paper Size       | ComboBox      | "80mm" / "58mm"                            |

## EXACT BUTTONS

| #  | Button              | Action                                                |
|----|---------------------|-------------------------------------------------------|
| 1  | 💾 حفظ الإعدادات     | Validates and saves all settings                      |
| 2  | 🔄 استعادة الإعدادات الافتراضية | Resets to defaults with confirmation |
| 3  | 🖨️ طباعة تجربة      | Prints a test receipt                                 |

## UI STATES

| State          | Description                                         |
|----------------|-----------------------------------------------------|
| Loading        | Settings being loaded                                |
| Loaded         | Settings form populated with current values          |
| Saving         | Settings being saved                                 |
| SaveSuccess    | "تم حفظ الإعدادات بنجاح" message shown              |
| SaveError      | Failed to save; error message shown                 |

## ACCEPTANCE CRITERIA

1. **AC-001:** All settings are loaded from the database on form open.
2. **AC-002:** Company name appears on receipts and report headers.
3. **AC-003:** Tax rate change affects all new invoices (not existing ones).
4. **AC-004:** Receipt header/footer text appears on all printed receipts.
5. **AC-005:** Test print sends a sample receipt to the selected printer.
6. **AC-006:** Changing language requires app restart to take full effect.
7. **AC-007:** Auto-backup interval change takes effect on next schedule.
8. **AC-008:** Currency display (JOD, 3 decimals) is read-only and fixed.
