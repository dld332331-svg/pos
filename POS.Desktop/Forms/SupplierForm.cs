using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

/// <summary>
/// SUPP-002: Add/Edit supplier dialog helper.
/// Creates an RtlDialog with fields: الاسم (required), جهة الاتصال, الهاتف,
/// البريد الإلكتروني, العنوان (multiline), مفعّل (checkbox).
/// Validation with Arabic error messages. Save/Cancel buttons.
/// </summary>
public static class SupplierForm
{
    /// <summary>
    /// Shows the add/edit supplier dialog.
    /// Returns DialogResult.OK if saved, DialogResult.Cancel otherwise.
    /// When saving a new supplier, it's added to the list automatically.
    /// When editing, the existing entry is updated in place.
    /// </summary>
    public static DialogResult ShowDialog(
        object? existingSupplier,
        List<SupplierData> supplierList,
        IWin32Window? owner)
    {
        var isEdit = existingSupplier != null;
        var entry = existingSupplier as SupplierData;

        var dialog = new RtlDialog(
            isEdit ? "تعديل بيانات المورد" : "إضافة مورد جديد",
            520,
            460);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 8,
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int i = 0; i < 7; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // الاسم (required)
        layout.Controls.Add(CreateLabel("الاسم *:"), 0, 0);
        var txtName = new RtlTextBox
        {
            Text = entry?.Name ?? "",
            Dock = DockStyle.Fill,
            IsRequired = true
        };
        layout.Controls.Add(txtName, 1, 0);

        // جهة الاتصال
        layout.Controls.Add(CreateLabel("جهة الاتصال:"), 0, 1);
        var txtContact = new RtlTextBox
        {
            Text = entry?.Contact ?? "",
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(txtContact, 1, 1);

        // الهاتف
        layout.Controls.Add(CreateLabel("الهاتف:"), 0, 2);
        var txtPhone = new RtlTextBox
        {
            Text = entry?.Phone ?? "",
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(txtPhone, 1, 2);

        // البريد الإلكتروني
        layout.Controls.Add(CreateLabel("البريد الإلكتروني:"), 0, 3);
        var txtEmail = new RtlTextBox
        {
            Text = entry?.Email ?? "",
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(txtEmail, 1, 3);

        // العنوان (multiline)
        layout.Controls.Add(CreateLabel("العنوان:"), 0, 4);
        var txtAddress = new TextBox
        {
            Text = entry?.Address ?? "",
            Multiline = true,
            Height = 80,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.Input,
            BackColor = DesignTokens.Colors.Surface,
            ForeColor = DesignTokens.Colors.TextPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill
        };
        layout.RowStyles[4] = new RowStyle(SizeType.Percent, 100);
        layout.Controls.Add(txtAddress, 1, 4);

        // الرصيد (read-only display)
        layout.Controls.Add(CreateLabel("الرصيد الحالي:"), 0, 5);
        var lblBalance = new Label
        {
            Text = entry != null
                ? $"{DesignTokens.FormatJOD(entry.Balance)} JOD"
                : "0.000 JOD",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = entry != null && entry.Balance < 0
                ? DesignTokens.Colors.Error
                : DesignTokens.Colors.Success,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(lblBalance, 1, 5);

        // مفعّل (checkbox)
        layout.Controls.Add(CreateLabel("الحالة:"), 0, 6);
        var chkActive = new CheckBox
        {
            Text = "مفعّل",
            Checked = entry?.IsActive ?? true,
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextPrimary,
            RightToLeft = RightToLeft.Yes,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };
        layout.Controls.Add(chkActive, 1, 6);

        dialog.ContentArea.Controls.Add(layout);

        // Validation label
        var lblValidation = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Bottom,
            Height = 20,
            TextAlign = ContentAlignment.TopRight,
            Visible = false,
            Padding = new Padding(0, DesignTokens.Spacing.Micro, 0, 0)
        };
        dialog.ContentArea.Controls.Add(lblValidation);

        // Phone validation label
        var lblPhoneValidation = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.Warning,
            Dock = DockStyle.Bottom,
            Height = 16,
            TextAlign = ContentAlignment.TopRight,
            Visible = false,
            Padding = new Padding(0, DesignTokens.Spacing.Micro, 0, 0)
        };
        dialog.ContentArea.Controls.Add(lblPhoneValidation);

        // Phone format hint
        var lblPhoneHint = new Label
        {
            Text = "تنسيق: 07XXXXXXXX (10 أرقام)",
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Bottom,
            Height = 16,
            TextAlign = ContentAlignment.TopRight,
            Visible = true,
            Padding = new Padding(0, DesignTokens.Spacing.Micro, 0, 0)
        };
        dialog.ContentArea.Controls.Add(lblPhoneHint);

        // Phone live validation
        txtPhone.TextChanged += (s, e) =>
        {
            var phone = txtPhone.Text.Trim();
            if (string.IsNullOrWhiteSpace(phone))
            {
                lblPhoneValidation.Visible = false;
                lblPhoneHint.Visible = true;
                return;
            }
            if (!IsValidPhoneFormat(phone))
            {
                lblPhoneValidation.Text = "تنسيق رقم الهاتف غير صحيح";
                lblPhoneValidation.Visible = true;
                lblPhoneHint.Visible = false;
            }
            else
            {
                lblPhoneValidation.Visible = false;
                lblPhoneHint.Visible = true;
            }
        };

        // Email validation label
        var lblEmailValidation = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.Warning,
            Dock = DockStyle.Bottom,
            Height = 16,
            TextAlign = ContentAlignment.TopRight,
            Visible = false,
            Padding = new Padding(0, DesignTokens.Spacing.Micro, 0, 0)
        };
        dialog.ContentArea.Controls.Add(lblEmailValidation);

        // Email live validation
        txtEmail.TextChanged += (s, e) =>
        {
            var email = txtEmail.Text.Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                lblEmailValidation.Visible = false;
                return;
            }
            if (!IsValidEmailFormat(email))
            {
                lblEmailValidation.Text = "تنسيق البريد الإلكتروني غير صحيح";
                lblEmailValidation.Visible = true;
            }
            else
            {
                lblEmailValidation.Visible = false;
            }
        };

        // Status info panel
        var statusPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
 BackColor = isEdit ? DesignTokens.Colors.Info : DesignTokens.Colors.Success,
            Padding = new Padding(DesignTokens.Spacing.Standard, DesignTokens.Spacing.Micro, DesignTokens.Spacing.Standard, DesignTokens.Spacing.Micro),
            Visible = true
        };
        var statusLabel = new Label
        {
            Text = isEdit ? $"✏️ تعديل مورد: {entry!.Name}" : "➕ إضافة مورد جديد",
            Font = DesignTokens.Typography.Body,
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        statusPanel.Controls.Add(statusLabel);
        dialog.ContentArea.Controls.Add(statusPanel);

        // Dialog actions
        dialog.AddAction(isEdit ? "تحديث" : "إضافة", (s, e) =>
        {
            // Validation
            var errors = ValidateFields(txtName.Text, txtEmail.Text, txtPhone.Text, supplierList, entry);

            if (errors.Count > 0)
            {
                lblValidation.Text = string.Join("\n", errors);
                lblValidation.Visible = true;
                txtName.SetError(errors.Any(er => er.Contains("الاسم")) ? "حقل مطلوب" : "");

                if (errors.Any(er => er.Contains("الاسم")))
                    txtName.Focus();
                return;
            }

            // Clear errors
            lblValidation.Visible = false;
            txtName.ClearError();

            if (isEdit && entry != null)
            {
                entry.Name = txtName.Text.Trim();
                entry.Contact = txtContact.Text.Trim();
                entry.Phone = txtPhone.Text.Trim();
                entry.Email = txtEmail.Text.Trim();
                entry.Address = txtAddress.Text.Trim();
                entry.IsActive = chkActive.Checked;
            }
            else
            {
                var maxId = supplierList.Count > 0 ? supplierList.Max(s => s.Id) : 0;
                var newSupplier = new SupplierData
                {
                    Id = maxId + 1,
                    Name = txtName.Text.Trim(),
                    Contact = txtContact.Text.Trim(),
                    Phone = txtPhone.Text.Trim(),
                    Email = txtEmail.Text.Trim(),
                    Address = txtAddress.Text.Trim(),
                    Balance = 0,
                    IsActive = chkActive.Checked
                };
                supplierList.Add(newSupplier);
            }

            dialog.DialogResult = DialogResult.OK;
            dialog.Close();
        });

        dialog.AddAction("إلغاء", (s, e) =>
        {
            dialog.DialogResult = DialogResult.Cancel;
            dialog.Close();
        }, false);

        return dialog.ShowDialog(owner);
    }

    /// <summary>
    /// Validates supplier form fields and returns a list of Arabic error messages.
    /// Checks: name required, email format, phone format, duplicate name.
    /// </summary>
    private static List<string> ValidateFields(string name, string email, string phone, List<SupplierData> supplierList, SupplierData? existingEntry)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("يرجى إدخال اسم المورد (حقل مطلوب)");
        }
        else
        {
            // Check for duplicate name
            var duplicate = supplierList.FirstOrDefault(s =>
                s.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) &&
                (existingEntry == null || s.Id != existingEntry.Id));

            if (duplicate != null)
            {
                errors.Add("يوجد مورد آخر بنفس الاسم. يرجى اختيار اسم مختلف.");
            }
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            if (!IsValidEmailFormat(email.Trim()))
            {
                errors.Add("يرجى إدخال بريد إلكتروني صحيح (مثال: name@example.com)");
            }
        }

        if (!string.IsNullOrWhiteSpace(phone))
        {
            if (!IsValidPhoneFormat(phone.Trim()))
            {
                errors.Add("يرجى إدخال رقم هاتف صحيح (مثال: 0791234567)");
            }
        }

        return errors;
    }

    /// <summary>
    /// Validates a phone number format (Jordanian mobile: 07XXXXXXXX).
    /// </summary>
    private static bool IsValidPhoneFormat(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return true;
        return System.Text.RegularExpressions.Regex.IsMatch(phone, @"^07[789]\d{7}$");
    }

    /// <summary>
    /// Validates a basic email format.
    /// </summary>
    private static bool IsValidEmailFormat(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return true;
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a right-aligned label for the dialog form.
    /// </summary>
    private static Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextPrimary,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, DesignTokens.Spacing.Micro, 0, 0)
        };
    }

    // --- Public Data Model for Supplier ---
    // This model is shared between SupplierListForm and SupplierForm

    /// <summary>
    /// Data model representing a supplier entry.
    /// SupplierListForm should use this type for its internal list,
    /// or define an equivalent class that casts to this.
    /// </summary>
    public class SupplierData
    {
        public int Id { get; set; }
        public Guid SupplierId { get; set; }
        public string Name { get; set; } = "";
        public string Contact { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Address { get; set; } = "";
        public decimal Balance { get; set; }
        public bool IsActive { get; set; } = true;
    }
}