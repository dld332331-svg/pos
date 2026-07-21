# SCREEN-001: Login Screen

## PURPOSE
Authenticate the user and establish the session context (user identity, role, permissions, and assigned register).

## PERMISSIONS
- No authentication required (public screen)
- All users regardless of role

## EXACT FIELDS

| #  | Field             | Type       | Required | Notes                                            |
|----|-------------------|------------|----------|--------------------------------------------------|
| 1  | Username / Email  | TextBox    | Yes      | Right-to-left enabled; Arabic and English input  |
| 2  | Password          | TextBox    | Yes      | Masked input (`UseSystemPasswordChar = true`)    |
| 3  | Register / POS    | ComboBox   | Yes      | Populated from `IRegisterService`; shows "كاشير 1", "كاشير 2", etc. |
| 4  | Status Message    | Label      | No       | Red text for errors, green for informational      |
| 5  | Version Info      | Label      | No       | Bottom-right; shows application version           |

## EXACT BUTTONS

| #  | Button      | Shortcut | Action                                               |
|----|-------------|----------|------------------------------------------------------|
| 1  | تسجيل الدخول (Login) | Enter    | Validates fields → calls `IAuthService.AuthenticateAsync()` → on success, opens `MainShell` with user context |
| 2  | إلغاء (Cancel)      | Escape   | Closes application or returns to previous state      |

## UI STATES

| State         | Description                                     | Visual Indicator                     |
|---------------|-------------------------------------------------|--------------------------------------|
| Idle          | Initial state, fields empty                     | All fields enabled, Login button enabled |
| Validating    | Checking credentials                            | Login button disabled, status text: "جاري التحقق..." |
| Success       | Authentication succeeded                        | Opens MainShell; LoginForm closes    |
| Error         | Invalid credentials or server error             | Status message shows error in red; fields remain |
| Locked        | Too many failed attempts (>= 3)                 | All inputs disabled for 30 seconds; countdown shown |
| DatabaseError | Cannot connect to database                      | Status message shows failure; retry option |

## ACCEPTANCE CRITERIA

1. **AC-001:** User enters valid credentials and presses Enter → session is created → MainShell opens.
2. **AC-002:** User enters invalid credentials → red error message "اسم المستخدم أو كلمة المرور غير صحيحة" (Invalid username or password).
3. **AC-003:** User leaves username or password empty → validation error shown.
4. **AC-004:** User fails login 3 times → screen locks for 30 seconds; countdown displayed.
5. **AC-005:** User selects a register from the ComboBox before login.
6. **AC-006:** Database is unreachable → error message "لا يمكن الاتصال بقاعدة البيانات" shown with retry option.
7. **AC-007:** Application version is displayed at bottom-right corner.
8. **AC-008:** All labels and buttons use Arabic text with proper Right-to-Left alignment.
9. **AC-009:** Visual feedback (loading state) is shown during authentication attempt.
