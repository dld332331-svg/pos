# SCREEN-011: User Management (إدارة المستخدمين)

## PURPOSE
Create, edit, and manage system users. Assign roles and configure granular permissions for each user.

## PERMISSIONS
- `ManageUsers` — Required to manage users

## EXACT FIELDS

| #  | Field              | Type          | Required | Notes                                     |
|----|--------------------|---------------|----------|-------------------------------------------|
| 1  | Search Box         | RtlTextBox    | No       | Search by name or username                |
| 2  | Role Filter        | ComboBox      | No       | "الكل" / "مدير" / "مشرف" / "كاشير"    |
| 3  | Users Grid         | DataGridView  | -        | Columns: #, الاسم, اسم المستخدم, الدور, الحالة, آخر دخول |

## EXACT BUTTONS

| #  | Button                | Action                                               |
|----|-----------------------|------------------------------------------------------|
| 1  | ➕ إضافة مستخدم        | Opens create user panel/dialog                       |
| 2  | ✏️ تعديل              | Opens edit user panel                                |
| 3  | 🔑 إعادة تعيين كلمة المرور | Resets user password                            |
| 4  | 🔒 تفعيل / تعطيل       | Toggles user active/inactive status                  |
| 5  | 🗑️ حذف                | Deletes user after confirmation                       |
| 6  | 🔄 تحديث               | Refreshes user list                                  |

## USER FORM FIELDS (Add/Edit)

| #  | Field             | Type       | Required | Notes                        |
|----|-------------------|------------|----------|------------------------------|
| 1  | Full Name         | RtlTextBox | Yes      | Full name in Arabic          |
| 2  | Username          | TextBox    | Yes      | Login username (English)     |
| 3  | Password          | TextBox    | Yes*     | Required for new users       |
| 4  | Role              | ComboBox   | Yes      | مدير / مشرف / كاشير          |
| 5  | Permissions List  | CheckedListBox | Yes  | Granular permission checkboxes |
| 6  | Active            | CheckBox   | Yes      | User enabled/disabled       |
| 7  | Notes             | TextBox    | No       | Internal notes               |

## GRID COLUMNS

| #  | Column Name | Format                  | Notes            |
|----|-------------|-------------------------|------------------|
| 1  | #           | Integer                 | Row number       |
| 2  | الاسم       | Text                    | Full name        |
| 3  | اسم المستخدم| Text                    | Username         |
| 4  | الدور       | Badge                   | Role with color  |
| 5  | الحالة      | Badge (نشط/موقف)        | Active/inactive  |
| 6  | آخر دخول    | DateTime                | Last login time  |

## UI STATES

| State     | Description                                         |
|-----------|-----------------------------------------------------|
| Loading   | Users being fetched                                 |
| Loaded    | Users displayed in grid                             |
| Editing   | User form panel open (add/edit mode)                |
| Saving    | User data being saved                               |
| Error     | Failed to load or save                              |

## ACCEPTANCE CRITERIA

1. **AC-001:** Users grid shows all system users with their roles.
2. **AC-002:** Adding a user requires username, password, name, and role.
3. **AC-003:** Password must be at least 6 characters.
4. **AC-004:** Username must be unique.
5. **AC-005:** Permissions are displayed as a checked list; defaults based on role.
6. **AC-006:** Deactivating a user prevents login but retains data.
7. **AC-007:** Cannot delete the currently logged-in user.
8. **AC-008:** Password reset generates a temporary password.
