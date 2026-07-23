using System.Data;
using System.Drawing;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

/// <summary>
/// BKP-001: Backup management UserControl using DevExpress GridControl.
/// Top: backup button + auto-backup config.
/// Main: RtlGridControl (Date, Size, User, Verified, Actions).
/// Footer: Restore (double confirmation), Delete buttons. Status bar.
/// </summary>
public class BackupForm : UserControl
{
    private enum BackupState { Idle, Loading, BackingUp, Restoring, Loaded, Empty, Error, PermissionDenied }

    private BackupState _currentState = BackupState.Loading;
    private readonly IBackupManagementService _backupService;
    private List<BackupDto> _backups = new();

    // UI Controls
    private Panel _toolbarPanel = null!;
    private RtlButton _btnCreateBackup = null!;
    private CheckBox _chkAutoBackup = null!;
    private RtlComboBox _cmbAutoInterval = null!;
    private RtlGridControl _backupsGrid = null!;
    private Panel _footerPanel = null!;
    private RtlButton _btnRestore = null!;
    private RtlButton _btnDelete = null!;
    private Panel _statusBar = null!;
    private Label _lblStatusIcon = null!;
    private Label _lblStatusText = null!;
    private Panel _loadingOverlay = null!;
    private Panel _emptyOverlay = null!;
    private Panel _permissionPanel = null!;

    public event EventHandler<string>? BackupCompleted;
    public event EventHandler<string>? RestoreCompleted;
    public Guid CurrentUserId { get; set; }

    public BackupForm(IBackupManagementService backupService)
    {
        _backupService = backupService;
        InitializeComponent();
        SetState(BackupState.Loading);
        _ = LoadDataAsync();
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.Colors.Background;
        Font = DesignTokens.Typography.Body;
        Dock = DockStyle.Fill;

        // Toolbar
        _toolbarPanel = new Panel { Dock = DockStyle.Top, Height = 90, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };

        var topRow = new FlowLayoutPanel { Dock = DockStyle.Top, Height = DesignTokens.ControlHeight.Large, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _btnCreateBackup = new RtlButton { Text = "💾 إنشاء نسخة احتياطية", Type = RtlButton.ButtonType.Primary, Width = 200, Height = DesignTokens.ControlHeight.Large };
        _btnCreateBackup.Click += async (s, e) => await CreateBackupAsync();
        var lblLastBackup = new Label { Text = "آخر نسخة: لم يتم إنشاء نسخة بعد", Font = DesignTokens.Typography.Secondary, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleRight, AutoSize = true, Margin = new Padding(DesignTokens.Spacing.Standard, 0, 0, 0) };
        lblLastBackup.Name = "lblLastBackup";
        topRow.Controls.Add(_btnCreateBackup);
        topRow.Controls.Add(lblLastBackup);
        _toolbarPanel.Controls.Add(topRow);

        var autoRow = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = DesignTokens.ControlHeight.Standard, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        var lblAutoTitle = new Label { Text = "النسخ الاحتياطية التلقائية:", Font = DesignTokens.Typography.BodyBold, ForeColor = DesignTokens.Colors.TextPrimary, AutoSize = true, Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0) };
        _chkAutoBackup = new CheckBox { Text = "تفعيل", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, AutoSize = true, Checked = true, Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0) };
        _cmbAutoInterval = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120, Height = DesignTokens.ControlHeight.Standard };
        _cmbAutoInterval.Items.AddRange(new object[] { "كل ساعة", "كل 4 ساعات", "كل 8 ساعات", "يومياً", "أسبوعياً" });
        _cmbAutoInterval.SelectedIndex = 2;
        autoRow.Controls.Add(lblAutoTitle);
        autoRow.Controls.Add(_chkAutoBackup);
        autoRow.Controls.Add(new Label { Text = "الفاصل:", Font = DesignTokens.Typography.Body, ForeColor = DesignTokens.Colors.TextPrimary, AutoSize = true, Margin = new Padding(0, 0, DesignTokens.Spacing.Micro, 0) });
        autoRow.Controls.Add(_cmbAutoInterval);
        _toolbarPanel.Controls.Add(autoRow);

        // DevExpress Grid
        _backupsGrid = new RtlGridControl();
        _backupsGrid.AddTextColumn("Date", "التاريخ", 180);
        _backupsGrid.AddTextColumn("Size", "الحجم", 100, DevExpress.Utils.HorzAlignment.Center);
        _backupsGrid.AddTextColumn("User", "المستخدم", 150);
        _backupsGrid.AddTextColumn("Verified", "مُتحقق", 100, DevExpress.Utils.HorzAlignment.Center);
        _backupsGrid.AddActionsColumn("إجراءات", 80);

        // Format verified and actions
        _backupsGrid.GridViewCore.RowCellStyle += (s, e) =>
        {
            if (e.Column.FieldName == "Verified")
            {
                var verified = Convert.ToBoolean(_backupsGrid.GridViewCore.GetRowCellValue(e.RowHandle, "RawVerified"));
                e.Appearance.ForeColor = verified ? DesignTokens.Colors.Success : DesignTokens.Colors.TextSecondary;
                e.Appearance.Options.UseForeColor = true;
            }
        };

        // Action button: download trigger
        _backupsGrid.ActionButtonClick += (s, e) =>
        {
            if (e.RowData is DataRowView row)
                UpdateStatus(true, $"تم تحضير الملف: backup_{Convert.ToDateTime(row["RawDate"]):yyyyMMdd_HHmmss}.zip");
        };

        // Row focus for restore/delete
        _backupsGrid.GridViewCore.FocusedRowChanged += (s, e) => UpdateActionButtons();

        // Footer
        _footerPanel = new Panel { Dock = DockStyle.Bottom, Height = DesignTokens.ControlHeight.Large + DesignTokens.Spacing.Compact, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        var footerInner = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false };
        _btnRestore = new RtlButton { Text = "🔄 استعادة", Type = RtlButton.ButtonType.Destructive, Width = 120, Height = DesignTokens.ControlHeight.Standard };
        _btnRestore.Click += async (s, e) => await RestoreBackupAsync();
        _btnDelete = new RtlButton { Text = "🗑 حذف", Type = RtlButton.ButtonType.Secondary, Width = 100, Height = DesignTokens.ControlHeight.Standard, Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0) };
        _btnDelete.Click += DeleteBackup_Click;
        footerInner.Controls.Add(_btnRestore);
        footerInner.Controls.Add(_btnDelete);
        _footerPanel.Controls.Add(footerInner);

        // Status bar
        _statusBar = new Panel { Dock = DockStyle.Bottom, Height = 36, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard), BorderStyle = BorderStyle.FixedSingle };
        _lblStatusIcon = new Label { Text = "●", Font = new Font("Segoe UI", 12f), ForeColor = DesignTokens.Colors.TextSecondary, AutoSize = true, Dock = DockStyle.Right, Width = 20, TextAlign = ContentAlignment.MiddleCenter };
        _lblStatusText = new Label { Text = "جاهز", Font = DesignTokens.Typography.Secondary, ForeColor = DesignTokens.Colors.TextSecondary, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        _statusBar.Controls.Add(_lblStatusText);
        _statusBar.Controls.Add(_lblStatusIcon);

        // Overlays
        _loadingOverlay = ThemeManager.CreateLoadingPanel("جاري إنشاء نسخة احتياطية...");
        _loadingOverlay.Visible = false;
        _emptyOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        var emptyIcon = new Label { Text = "💾", Font = new Font("Segoe UI", 48f), TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Top, Height = 80 };
        var emptyLabel = new Label { Text = "لا توجد نسخ احتياطية", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };
        _emptyOverlay.Controls.Add(emptyLabel);
        _emptyOverlay.Controls.Add(emptyIcon);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label { Text = "ليس لديك صلاحية لإدارة النسخ الاحتياطية", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        Controls.Add(_loadingOverlay);
        Controls.Add(_emptyOverlay);
        Controls.Add(_permissionPanel);
        Controls.Add(_backupsGrid);
        Controls.Add(_statusBar);
        Controls.Add(_footerPanel);
        Controls.Add(_toolbarPanel);
    }

    private void SetState(BackupState state)
    {
        _currentState = state;
        _loadingOverlay.Visible = state == BackupState.BackingUp || state == BackupState.Restoring || state == BackupState.Loading;
        _emptyOverlay.Visible = state == BackupState.Empty;
        _permissionPanel.Visible = state == BackupState.PermissionDenied;
        _backupsGrid.Visible = state == BackupState.Loaded || state == BackupState.Idle;
        _btnCreateBackup.Enabled = state != BackupState.BackingUp && state != BackupState.Restoring;
        UpdateActionButtons();
        if (_loadingOverlay.Visible)
        {
            var lbl = _loadingOverlay.Controls.OfType<Label>().LastOrDefault();
            if (lbl != null) lbl.Text = state == BackupState.Restoring ? "جاري استعادة النسخة الاحتياطية..." : "جاري إنشاء نسخة احتياطية...";
        }
    }

    private void UpdateActionButtons()
    {
        bool hasSelection = _backupsGrid.GridViewCore.FocusedRowHandle >= 0;
        _btnRestore.Enabled = hasSelection && _currentState != BackupState.BackingUp && _currentState != BackupState.Restoring;
        _btnDelete.Enabled = hasSelection && _currentState != BackupState.BackingUp && _currentState != BackupState.Restoring;
    }

    private async Task LoadDataAsync()
    {
        SetState(BackupState.Loading);
        try
        {
            _backups = await _backupService.GetBackupHistoryAsync();
            PopulateGrid();
            SetState(_backups.Count > 0 ? BackupState.Loaded : BackupState.Empty);
            UpdateLastBackupLabel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[BackupForm] LoadBackupsAsync failed: {ex}");
            SetState(BackupState.Error);
            UpdateStatus(false, "فشل تحميل سجل النسخ");
        }
    }

    private void PopulateGrid()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(Guid));
        table.Columns.Add("Date", typeof(string));
        table.Columns.Add("Size", typeof(string));
        table.Columns.Add("UserM", typeof(string));
        table.Columns.Add("Verified", typeof(string));
        table.Columns.Add("RawVerified", typeof(bool));
        table.Columns.Add("RawDate", typeof(DateTime));

        foreach (var b in _backups)
        {
            var row = table.NewRow();
            row["Id"] = b.Id;
            row["Date"] = b.CreatedAt.ToString("yyyy/MM/dd HH:mm");
            row["Size"] = FormatFileSize(b.FileSize);
            row["UserM"] = "";
            row["Verified"] = b.IsVerified ? "✓ متحقق" : "غير متحقق";
            row["RawVerified"] = b.IsVerified;
            row["RawDate"] = b.CreatedAt;
            table.Rows.Add(row);
        }
        _backupsGrid.SetDataSource(table);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }

    private BackupDto? GetSelectedBackup()
    {
        if (_backupsGrid.GridViewCore.FocusedRowHandle < 0) return null;
        var row = _backupsGrid.GridViewCore.GetFocusedRow() as DataRowView;
        if (row == null) return null;
        var id = (Guid)row["Id"];
        return _backups.FirstOrDefault(b => b.Id == id);
    }

    private async Task CreateBackupAsync()
    {
        SetState(BackupState.BackingUp);
        UpdateStatus(null, "جاري إنشاء نسخة احتياطية...");
        try
        {
            var backup = await _backupService.CreateBackupAsync(CurrentUserId);
            _backups.Insert(0, backup);
            PopulateGrid();
            SetState(BackupState.Loaded);
            UpdateStatus(true, $"تم إنشاء النسخة الاحتياطية بنجاح");
            BackupCompleted?.Invoke(this, "تم إنشاء النسخة الاحتياطية بنجاح");
            UpdateLastBackupLabel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[BackupForm] CreateBackupAsync failed: {ex}");
            SetState(BackupState.Loaded);
            UpdateStatus(false, "فشل إنشاء النسخة");
        }
    }

    private async Task RestoreBackupAsync()
    {
        var backup = GetSelectedBackup();
        if (backup == null) { RtlMessageBox.Show("يرجى اختيار نسخة احتياطية للاستعادة", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        if (RtlDialog.ShowConfirm("تأكيد الاستعادة", $"هل أنت متأكد من استعادة النسخة الاحتياطية بتاريخ {backup.CreatedAt:yyyy/MM/dd HH:mm}؟\n\nسيتم استبدال جميع البيانات الحالية.", "نعم، متأكد", "إلغاء") != DialogResult.OK) return;
        if (RtlDialog.ShowDestructiveConfirm("تحذير نهائي - استعادة البيانات", $"⚠ تحذير: هذه العملية لا يمكن التراجع عنها!\n\nسيتم حذف جميع البيانات الحالية واستبدالها ببيانات النسخة الاحتياطية.\nالتاريخ: {backup.CreatedAt:yyyy/MM/dd HH:mm}\n\nاضغط \"استعادة\" لتأكيد العملية.") != DialogResult.OK) return;

        SetState(BackupState.Restoring);
        try
        {
            var result = await _backupService.RestoreBackupAsync(backup.Id, CurrentUserId);
            SetState(BackupState.Loaded);
            UpdateStatus(result.Success, result.Success ? "تمت استعادة النسخة الاحتياطية بنجاح" : result.ErrorMessage ?? "فشلت الاستعادة");
            if (result.Success)
                RestoreCompleted?.Invoke(this, $"تمت الاستعادة من نسخة {backup.CreatedAt:yyyy/MM/dd}");
            _ = LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Restore backup failed: {0}", ex);
            SetState(BackupState.Loaded);
            UpdateStatus(false, "فشلت الاستعادة");
        }
    }

    private async void DeleteBackup_Click(object? sender, EventArgs e)
    {
        var backup = GetSelectedBackup();
        if (backup == null) { RtlMessageBox.Show("يرجى اختيار نسخة احتياطية للحذف", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
        if (RtlDialog.ShowDestructiveConfirm("حذف نسخة احتياطية", $"هل أنت متأكد من حذف النسخة الاحتياطية بتاريخ {backup.CreatedAt:yyyy/MM/dd HH:mm}؟\n\nلا يمكن التراجع عن هذه العملية.") != DialogResult.OK) return;

        try
        {
            await _backupService.DeleteBackupAsync(backup.Id);
            _backups.Remove(backup);
            PopulateGrid();
            SetState(_backups.Count > 0 ? BackupState.Loaded : BackupState.Empty);
            UpdateStatus(true, "تم حذف النسخة الاحتياطية بنجاح");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Delete backup failed: {0}", ex);
            UpdateStatus(false, "فشل حذف النسخة");
        }
    }

    private void UpdateStatus(bool? success, string message)
    {
        if (_lblStatusText.IsDisposed || _lblStatusIcon.IsDisposed) return;
        _lblStatusText.Text = message;
        _lblStatusIcon.ForeColor = success == true ? DesignTokens.Colors.Success : success == false ? DesignTokens.Colors.Error : DesignTokens.Colors.Warning;
        _lblStatusText.ForeColor = _lblStatusIcon.ForeColor;
    }

    private void UpdateLastBackupLabel()
    {
        var first = _backups.FirstOrDefault();
        var lblLast = _toolbarPanel.Controls.OfType<FlowLayoutPanel>()
            .SelectMany(p => p.Controls.OfType<Label>())
            .FirstOrDefault(l => l.Name == "lblLastBackup");
        if (lblLast != null)
            lblLast.Text = first != null
                ? $"آخر نسخة: {first.CreatedAt:yyyy/MM/dd HH:mm}"
                : "آخر نسخة: لم يتم إنشاء نسخة بعد";
    }
}
