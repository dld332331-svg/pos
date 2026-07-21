# SCREEN-016: Backup Management (النسخ الاحتياطي)

## PURPOSE
Manage database backups: create manual backups, restore from backups, view backup history, and verify backup integrity.

## PERMISSIONS
- `Backup` — Required to create backups
- `Restore` — Required to restore from backups

## EXACT FIELDS

| #  | Field               | Type          | Notes                                        |
|----|---------------------|---------------|----------------------------------------------|
| 1  | Backup History Grid | DataGridView  | List of all backups with details             |
| 2  | Last Backup Label   | Label         | "آخر نسخة احتياطية: [date/time]"             |
| 3  | Total Backups Label | Label         | "إجمالي النسخ: X"                            |
| 4  | Backup Size Label   | Label         | "الحجم الإجمالي: X MB"                       |
| 5  | Status Message      | Label         | Current operation status                     |

## EXACT BUTTONS

| #  | Button                | Permission | Action                                           |
|----|-----------------------|------------|--------------------------------------------------|
| 1  | 💾 إنشاء نسخة احتياطية | Backup     | Creates a new database backup                     |
| 2  | 🔄 استعادة            | Restore    | Restores the selected backup (requires confirmation) |
| 3  | ✅ التحقق من السلامة  | Backup     | Runs RESTORE VERIFYONLY on selected backup       |
| 4  | 🗑️ حذف                | Backup     | Deletes selected backup file and record          |
| 5  | 📥 تصدير              | Backup     | Copy backup file to external location            |
| 6  | 🔄 تحديث              | -          | Refreshes backup history list                    |

## GRID COLUMNS

| #  | Column Name       | Format                    | Notes             |
|----|-------------------|---------------------------|-------------------|
| 1  | #                 | Integer                   | Row number        |
| 2  | التاريخ           | DateTime                  | Creation timestamp|
| 3  | الحجم             | "X.XX MB"                 | File size         |
| 4  | منشئ النسخة       | Text                      | Created by user   |
| 5  | تم التحقق         | Badge (نعم/لا)            | Integrity check   |
| 6  | ملاحظات           | Text                      | Notes field       |

## UI STATES

| State           | Description                                         |
|-----------------|-----------------------------------------------------|
| Loading         | Backup history being loaded                         |
| Loaded          | Backups displayed; actions available                |
| Creating        | Backup in progress (progress bar shown)             |
| Verifying       | RESTORE VERIFYONLY in progress                      |
| Restoring       | Restore in progress (caution: app may freeze)       |
| Error           | Operation failed; error message shown               |
| Empty           | No backups yet                                      |

## ACCEPTANCE CRITERIA

1. **AC-001:** Creating a backup generates a .bak file and records it in the database.
2. **AC-002:** Every backup is automatically verified with RESTORE VERIFYONLY.
3. **AC-003:** Restore requires confirmation: "تحذير: سيتم استبدال البيانات الحالية. هل أنت متأكد؟".
4. **AC-004:** Backup history shows creation date, size, verifier, and notes.
5. **AC-005:** Automatic backups run at configured intervals (BackgroundService).
6. **AC-006:** Retention policy keeps max 30 backups (or 90 days).
7. **AC-007:** Status bar shows progress during backup operations.
8. **AC-008:** Deleting a backup requires confirmation.
