using System.Drawing;
using System.Windows.Forms;
using POS.Application.Services;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;
using POS.Domain.Enums;
using POS.Domain.Interfaces;

namespace POS.Desktop.Forms;

/// <summary>
/// SET-001: Settings form organized in TabControl with full RTL Arabic layout.
/// Tabs: General, Currency, Tax, Security, Backup, Sounds, Appearance.
/// Save and Cancel buttons at the bottom.
/// </summary>
public class SettingsForm : UserControl
{
    private enum SettingsState { Loading, Loaded, Error, PermissionDenied }
    private SettingsState _currentState = SettingsState.Loading;

    // UI Controls - Root
    private Panel _mainPanel;
    private Panel _contentPanel;
    private Panel _footerPanel;

    // TabControl
    private TabControl _tabControl;
    private TabPage _tabGeneral;
    private TabPage _tabCurrency;
    private TabPage _tabTax;
    private TabPage _tabSecurity;
    private TabPage _tabBackup;
    private TabPage _tabSounds;
    private TabPage _tabAppearance;

    // General Tab
    private RtlTextBox _txtBusinessName;
    private RtlTextBox _txtAddress;
    private RtlTextBox _txtPhone;
    private RtlTextBox _txtTaxNumber;

    // Currency Tab
    private RtlTextBox _txtCurrencySymbol;
    private RtlNumericUpDown _numDecimals;

    // Tax Tab
    private RtlNumericUpDown _numDefaultTaxRate;
    private CheckBox _chkTaxInclusive;

    // Security Tab
    private RtlNumericUpDown _numMaxLoginAttempts;
    private RtlNumericUpDown _numSessionTimeout;
    private CheckBox _chkRequireSpecialChars;
    private CheckBox _chkRequireNumbers;
    private NumericUpDown _numMinPasswordLength;

    // Backup Tab
    private CheckBox _chkAutoBackup;
    private RtlComboBox _cmbBackupInterval;
    private RtlTextBox _txtBackupPath;
    private RtlButton _btnBrowseBackupPath;

    // Sounds Tab
    private CheckBox _chkSoundsEnabled;
    private TrackBar _trkVolume;
    private Label _lblVolumeValue;
    private CheckBox _chkOrderSound;
    private CheckBox _chkPaymentSound;
    private CheckBox _chkErrorSound;

    // Appearance Tab (read-only info)
    private Label _lblDesignInfo;

    // Footer
    private RtlButton _btnSave;
    private RtlButton _btnCancel;
    private Label _lblStatus;

    // Overlay panels
    private Panel _loadingPanel = null!;
    private Panel _errorPanel = null!;
    private Panel _permissionPanel = null!;
    private Label _errorLabel = null!;

    // Events
    public event EventHandler? SettingsSaved;
    public event EventHandler? SettingsCancelled;

    private readonly ISoundService? _soundService;

    public SettingsForm()
    {
        InitializeComponent();
        SetState(SettingsState.Loaded);
    }

    public SettingsForm(ISoundService? soundService) : this()
    {
        _soundService = soundService;
        LoadSoundSettings();
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.Colors.Background;
        Font = DesignTokens.Typography.Body;
        Dock = DockStyle.Fill;

        // Main container
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        // Content area (above footer)
        _contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background
        };

        // === TabControl ===
        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.BodyBold,
            Padding = new Point(DesignTokens.Spacing.Standard, DesignTokens.Spacing.Compact),
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Standard)
        };

        // --- General Tab ---
        _tabGeneral = new TabPage { Text = "عام", RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        var generalLayout = new TableLayoutPanel { ColumnCount = 2, RowCount = 8, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface };
        generalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        generalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 8; i++) generalLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        generalLayout.Controls.Add(CreateLabel("اسم المنشأة:"), 0, 0);
        _txtBusinessName = CreateTextBox("اسم المتجر");
        generalLayout.Controls.Add(_txtBusinessName, 1, 0);

        generalLayout.Controls.Add(CreateLabel("العنوان:"), 0, 1);
        _txtAddress = CreateTextBox("عنوان المنشأة");
        generalLayout.Controls.Add(_txtAddress, 1, 1);

        generalLayout.Controls.Add(CreateLabel("رقم الهاتف:"), 0, 2);
        _txtPhone = CreateTextBox("رقم الهاتف");
        generalLayout.Controls.Add(_txtPhone, 1, 2);

        generalLayout.Controls.Add(CreateLabel("الرقم الضريبي:"), 0, 3);
        _txtTaxNumber = CreateTextBox("الرقم الضريبي");
        generalLayout.Controls.Add(_txtTaxNumber, 1, 3);

        _tabGeneral.Controls.Add(generalLayout);

        // --- Currency Tab ---
        _tabCurrency = new TabPage { Text = "العملة", RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        var currencyLayout = new TableLayoutPanel { ColumnCount = 2, RowCount = 4, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface };
        currencyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        currencyLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) currencyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        currencyLayout.Controls.Add(CreateLabel("رمز العملة:"), 0, 0);
        _txtCurrencySymbol = CreateTextBox("JOD");
        _txtCurrencySymbol.ReadOnly = true;
        _txtCurrencySymbol.BackColor = DesignTokens.Colors.Background;
        generalLayout.Controls.Add(CreateInfoLabel("العملة ثابتة حسب النظام"), 1, 5);
        currencyLayout.Controls.Add(_txtCurrencySymbol, 1, 0);

        currencyLayout.Controls.Add(CreateLabel("عدد المنازل العشرية:"), 0, 1);
        _numDecimals = new RtlNumericUpDown { Value = 3, Minimum = 0, Maximum = 4, DecimalPlaces = 0, ReadOnly = true, BackColor = DesignTokens.Colors.Background };
        currencyLayout.Controls.Add(_numDecimals, 1, 1);

        currencyLayout.Controls.Add(CreateInfoLabel("إعدادات العملة محددة مسبقاً ولا يمكن تغييرها"), 0, 2);
        currencyLayout.SetColumnSpan(currencyLayout.Controls[4], 2);

        _tabCurrency.Controls.Add(currencyLayout);

        // --- Tax Tab ---
        _tabTax = new TabPage { Text = "الضريبة", RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        var taxLayout = new TableLayoutPanel { ColumnCount = 2, RowCount = 4, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface };
        taxLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        taxLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 4; i++) taxLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        taxLayout.Controls.Add(CreateLabel("نسبة الضريبة الافتراضية:"), 0, 0);
        _numDefaultTaxRate = new RtlNumericUpDown { Value = 16, Minimum = 0, Maximum = 100, DecimalPlaces = 2, Increment = 0.5m };
        taxLayout.Controls.Add(_numDefaultTaxRate, 1, 0);

        taxLayout.Controls.Add(CreateLabel("الأسعار شاملة الضريبة:"), 0, 1);
        _chkTaxInclusive = new CheckBox { Text = "الأسعار المعروضة تشمل الضريبة", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, ForeColor = DesignTokens.Colors.TextPrimary, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        taxLayout.Controls.Add(_chkTaxInclusive, 1, 1);

        taxLayout.Controls.Add(CreateInfoLabel("نسبة الضريبة الافتراضية تُطبق على الأصناف الجديدة"), 0, 2);
        taxLayout.SetColumnSpan(taxLayout.Controls[4], 2);

        _tabTax.Controls.Add(taxLayout);

        // --- Security Tab ---
        _tabSecurity = new TabPage { Text = "الأمان", RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        var secLayout = new TableLayoutPanel { ColumnCount = 2, RowCount = 10, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface };
        secLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        secLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 10; i++) secLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        secLayout.Controls.Add(CreateLabel("أقصى عدد محاولات الدخول:"), 0, 0);
        _numMaxLoginAttempts = new RtlNumericUpDown { Value = 5, Minimum = 1, Maximum = 20, DecimalPlaces = 0, Increment = 1, Width = 120 };
        secLayout.Controls.Add(_numMaxLoginAttempts, 1, 0);

        secLayout.Controls.Add(CreateLabel("مدة الجلسة (بالدقائق):"), 0, 1);
        _numSessionTimeout = new RtlNumericUpDown { Value = 30, Minimum = 5, Maximum = 480, DecimalPlaces = 0, Increment = 5, Width = 120 };
        secLayout.Controls.Add(_numSessionTimeout, 1, 1);

        secLayout.Controls.Add(CreateLabel("الحد الأدنى لطول كلمة المرور:"), 0, 2);
        _numMinPasswordLength = new NumericUpDown { Value = 6, Minimum = 4, Maximum = 32, DecimalPlaces = 0, Width = 120, RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Input };
        secLayout.Controls.Add(_numMinPasswordLength, 1, 2);

        secLayout.Controls.Add(CreateLabel("متطلبات كلمة المرور:"), 0, 3);
        _chkRequireSpecialChars = new CheckBox { Text = "أحرف خاصة", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        secLayout.Controls.Add(_chkRequireSpecialChars, 1, 3);

        secLayout.Controls.Add(new Control(), 0, 4);
        _chkRequireNumbers = new CheckBox { Text = "أرقام", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        secLayout.Controls.Add(_chkRequireNumbers, 1, 4);

        _tabSecurity.Controls.Add(secLayout);

        // --- Backup Tab ---
        _tabBackup = new TabPage { Text = "النسخ الاحتياطي", RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        var bkpLayout = new TableLayoutPanel { ColumnCount = 2, RowCount = 8, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface };
        bkpLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        bkpLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 8; i++) bkpLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        bkpLayout.Controls.Add(CreateLabel("النسخ الاحتياطي التلقائي:"), 0, 0);
        _chkAutoBackup = new CheckBox { Text = "تفعيل النسخ الاحتياطي التلقائي", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight };
        bkpLayout.Controls.Add(_chkAutoBackup, 1, 0);

        bkpLayout.Controls.Add(CreateLabel("الفاصل الزمني:"), 0, 1);
        _cmbBackupInterval = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbBackupInterval.Items.AddRange(new object[] { "كل ساعة", "كل 4 ساعات", "كل 8 ساعات", "يومياً", "أسبوعياً" });
        _cmbBackupInterval.SelectedIndex = 2;
        bkpLayout.Controls.Add(_cmbBackupInterval, 1, 1);

        bkpLayout.Controls.Add(CreateLabel("مسار الحفظ:"), 0, 2);
        var pathPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Surface };
        _txtBackupPath = new RtlTextBox { Text = @"C:\POS_Backups", Width = 250, Anchor = AnchorStyles.Right };
        _btnBrowseBackupPath = new RtlButton { Text = "تصفح", Type = RtlButton.ButtonType.Secondary, Width = 80, Height = DesignTokens.ControlHeight.Standard, Anchor = AnchorStyles.Left };
        _btnBrowseBackupPath.Click += BtnBrowseBackupPath_Click;
        pathPanel.Controls.Add(_btnBrowseBackupPath);
        pathPanel.Controls.Add(_txtBackupPath);
        bkpLayout.Controls.Add(pathPanel, 1, 2);

        _tabBackup.Controls.Add(bkpLayout);

        // --- Sounds Tab ---
        _tabSounds = new TabPage { Text = "الأصوات", RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        var sndLayout = new TableLayoutPanel { ColumnCount = 2, RowCount = 8, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface };
        sndLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        sndLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 8; i++) sndLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        sndLayout.Controls.Add(CreateLabel("الأصوات:"), 0, 0);
        _chkSoundsEnabled = new CheckBox { Text = "تفعيل الأصوات", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Checked = true };
        _chkSoundsEnabled.CheckedChanged += (s, e) => ToggleSoundsPanel();
        sndLayout.Controls.Add(_chkSoundsEnabled, 1, 0);

        sndLayout.Controls.Add(CreateLabel("مستوى الصوت:"), 0, 1);
        var volumePanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Surface };
        _trkVolume = new TrackBar { Minimum = 0, Maximum = 100, Value = 70, Width = 200, TickFrequency = 10, RightToLeft = RightToLeft.Yes, Anchor = AnchorStyles.Right };
        _trkVolume.Scroll += (s, e) => { _lblVolumeValue.Text = $"{_trkVolume.Value}%"; };
        _lblVolumeValue = new Label { Text = "70%", Font = DesignTokens.Typography.Body, Width = 50, TextAlign = ContentAlignment.MiddleCenter, Anchor = AnchorStyles.Left };
        volumePanel.Controls.Add(_lblVolumeValue);
        volumePanel.Controls.Add(_trkVolume);
        sndLayout.Controls.Add(volumePanel, 1, 1);

        sndLayout.Controls.Add(new Control(), 0, 2);
        _chkOrderSound = new CheckBox { Text = "صوت الطلب الجديد", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Checked = true };
        sndLayout.Controls.Add(_chkOrderSound, 1, 2);

        sndLayout.Controls.Add(new Control(), 0, 3);
        _chkPaymentSound = new CheckBox { Text = "صوت الدفع الناجح", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Checked = true };
        sndLayout.Controls.Add(_chkPaymentSound, 1, 3);

        sndLayout.Controls.Add(new Control(), 0, 4);
        _chkErrorSound = new CheckBox { Text = "صوت الخطأ", RightToLeft = RightToLeft.Yes, Font = DesignTokens.Typography.Body, AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Checked = true };
        sndLayout.Controls.Add(_chkErrorSound, 1, 4);

        _tabSounds.Controls.Add(sndLayout);

        // --- Appearance Tab (read-only) ---
        _tabAppearance = new TabPage { Text = "المظهر", RightToLeft = RightToLeft.Yes, BackColor = DesignTokens.Colors.Surface, Padding = new Padding(DesignTokens.Spacing.Standard) };
        var appearancePanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Surface };
        _lblDesignInfo = new Label
        {
            Text = "معلومات التصميم\n\n" +
                   "النظام يعمل بتصميم موحد يعتمد على:\n" +
                   "• نظام ألوان متسق (Primary, Success, Warning, Error)\n" +
                   "• خطوط عربية قياسية (Microsoft Sans Serif)\n" +
                   "• مسافات متناسقة بين العناصر\n" +
                   "• دعم كامل للاتجاه من اليمين لليسار\n" +
                   "• عناصر تحكم مخصصة (RTL Buttons, ComboBox, TextBox)\n\n" +
                   "الألوان الرئيسية:\n" +
                   "• اللون الأساسي: أزرق (#2962FF)\n" +
                   "• لون النجاح: أخضر (#2EA043)\n" +
                   "• لون التحذير: برتقالي (#FF9800)\n" +
                   "• لون الخطأ: أحمر (#E53935)\n\n" +
                   "ملاحظة: إعدادات المظهر تُدار عبر نظام التصميم (DesignTokens)\n" +
                   "ولا يمكن تغييرها يدوياً من هنا.",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopRight,
            BackColor = DesignTokens.Colors.Background,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            BorderStyle = BorderStyle.FixedSingle
        };
        appearancePanel.Controls.Add(_lblDesignInfo);
        _tabAppearance.Controls.Add(appearancePanel);

        // Add all tabs
        _tabControl.TabPages.AddRange(new TabPage[] { _tabGeneral, _tabCurrency, _tabTax, _tabSecurity, _tabBackup, _tabSounds, _tabAppearance });
        _contentPanel.Controls.Add(_tabControl);

        // === Footer Panel ===
        _footerPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = DesignTokens.ControlHeight.Large + DesignTokens.Spacing.Standard,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            Margin = new Padding(0, DesignTokens.Spacing.Standard, 0, 0)
        };

        var footerInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = DesignTokens.Colors.Surface
        };

        _btnSave = new RtlButton
        {
            Text = "حفظ الإعدادات",
            Type = RtlButton.ButtonType.Success,
            Width = 140,
            Height = DesignTokens.ControlHeight.Standard
        };
        _btnSave.Click += BtnSave_Click;

        _btnCancel = new RtlButton
        {
            Text = "إلغاء",
            Type = RtlButton.ButtonType.Secondary,
            Width = 100,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };
        _btnCancel.Click += (s, e) => SettingsCancelled?.Invoke(this, EventArgs.Empty);

        _lblStatus = new Label
        {
            Text = "",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.Success,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, 0, 0),
            Anchor = AnchorStyles.Left
        };

        footerInner.Controls.Add(_btnSave);
        footerInner.Controls.Add(_btnCancel);
        footerInner.Controls.Add(_lblStatus);
        _footerPanel.Controls.Add(footerInner);

        // Assemble
        _mainPanel.Controls.Add(_contentPanel);
        _mainPanel.Controls.Add(_footerPanel);
        Controls.Add(_mainPanel);

        // Overlay panels
        _loadingPanel = ThemeManager.CreateLoadingPanel("Ø¬Ø§Ø±ÙŠ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ø¥Ø¹Ø¯Ø§Ø¯Ø§Øª...");
        _loadingPanel.Visible = false;

        _errorPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _errorLabel = new Label
        {
            Text = "Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ø¥Ø¹Ø¯Ø§Ø¯Ø§Øª",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.Error,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var retryButton = new RtlButton
        {
            Text = "Ø¥Ø¹Ø§Ø¯Ø© Ø§Ù„Ù…Ø­Ø§ÙˆÙ„Ø©",
            Type = RtlButton.ButtonType.Primary,
            Width = 160,
            Height = DesignTokens.ControlHeight.Standard,
            Dock = DockStyle.Bottom
        };
        retryButton.Click += (s, args) => { SetState(SettingsState.Loading); LoadDefaults(); SetState(SettingsState.Loaded); };
        _errorPanel.Controls.Add(retryButton);
        _errorPanel.Controls.Add(_errorLabel);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label
        {
            Text = "Ù„ÙŠØ³ Ù„Ø¯ÙŠÙƒ ØµÙ„Ø§Ø­ÙŠØ© Ù„Ø¥Ø¯Ø§Ø±Ø© Ø§Ù„Ø¥Ø¹Ø¯Ø§Ø¯Ø§Øª",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        });

        Controls.Add(_loadingPanel);
        Controls.Add(_errorPanel);
        Controls.Add(_permissionPanel);

        // Load defaults
        LoadDefaults();
    }

    // --- Helper Methods ---

    private Label CreateLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextPrimary,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, DesignTokens.Spacing.Small, 0, 0)
        };
    }

    private Label CreateInfoLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleRight,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Margin = new Padding(0, DesignTokens.Spacing.Compact, 0, 0)
        };
    }

    private RtlTextBox CreateTextBox(string placeholder)
    {
        return new RtlTextBox
        {
            PlaceholderText = placeholder,
            Dock = DockStyle.Fill,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, DesignTokens.Spacing.Small, 0, 0)
        };
    }

    private void SetState(SettingsState state)
    {
        _currentState = state;
        _loadingPanel.Visible = state == SettingsState.Loading;
        _errorPanel.Visible = state == SettingsState.Error;
        _permissionPanel.Visible = state == SettingsState.PermissionDenied;
        _mainPanel.Visible = state == SettingsState.Loaded;
    }

    // --- Event Handlers ---

    private void BtnBrowseBackupPath_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "اختر مجلد حفظ النسخ الاحتياطية",
            ShowNewFolderButton = true,
            SelectedPath = _txtBackupPath.Text
        };
        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _txtBackupPath.Text = dialog.SelectedPath;
        }
    }

    private void ToggleSoundsPanel()
    {
        var enabled = _chkSoundsEnabled.Checked;
        _trkVolume.Enabled = enabled;
        _lblVolumeValue.Enabled = enabled;
        _chkOrderSound.Enabled = enabled;
        _chkPaymentSound.Enabled = enabled;
        _chkErrorSound.Enabled = enabled;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(_txtBusinessName.Text))
        {
            _lblStatus.Text = "⚠ اسم المنشأة مطلوب";
            _lblStatus.ForeColor = DesignTokens.Colors.Error;
            _tabControl.SelectedTab = _tabGeneral;
            _txtBusinessName.Focus();
            return;
        }

        // Collect and save settings
        var settings = CollectSettings();
        SaveSoundSettings();

        // Simulate save
        _btnSave.IsLoading = true;
        _lblStatus.Text = "جاري الحفظ...";
        _lblStatus.ForeColor = DesignTokens.Colors.TextSecondary;

        var timer = new Timer { Interval = 800 };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            timer.Dispose();
            _btnSave.IsLoading = false;

            if (this.FindForm() != null && !this.IsDisposed)
            {
                _lblStatus.Text = "✓ تم حفظ الإعدادات بنجاح";
                _lblStatus.ForeColor = DesignTokens.Colors.Success;
                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }
        };
        timer.Start();
    }

    private void LoadSoundSettings()
    {
        if (_soundService is null) return;
        _chkSoundsEnabled.Checked = _soundService.Enabled;
        _trkVolume.Value = _soundService.Volume;
        _lblVolumeValue.Text = $"{_soundService.Volume}%";
        _chkOrderSound.Checked = _soundService.IsEventEnabled(SoundEvent.KitchenOrder);
        _chkPaymentSound.Checked = _soundService.IsEventEnabled(SoundEvent.PaymentSuccess);
        _chkErrorSound.Checked = _soundService.IsEventEnabled(SoundEvent.SystemError);
    }

    private void SaveSoundSettings()
    {
        if (_soundService is null) return;
        _soundService.Enabled = _chkSoundsEnabled.Checked;
        _soundService.Volume = _trkVolume.Value;
        _soundService.SetEventEnabled(SoundEvent.KitchenOrder, _chkOrderSound.Checked);
        _soundService.SetEventEnabled(SoundEvent.PaymentSuccess, _chkPaymentSound.Checked);
        _soundService.SetEventEnabled(SoundEvent.SystemError, _chkErrorSound.Checked);
    }

    // --- Public Methods ---

    public void LoadDefaults()
    {
        _txtBusinessName.Text = "مؤسسة النقاط";
        _txtAddress.Text = "عمان، الأردن";
        _txtPhone.Text = "+962790000000";
        _txtTaxNumber.Text = "";
        _txtCurrencySymbol.Text = "JOD";
        _numDecimals.Value = 3;
        _numDefaultTaxRate.Value = 16;
        _chkTaxInclusive.Checked = false;
        _numMaxLoginAttempts.Value = 5;
        _numSessionTimeout.Value = 30;
        _numMinPasswordLength.Value = 6;
        _chkRequireSpecialChars.Checked = true;
        _chkRequireNumbers.Checked = true;
        _chkAutoBackup.Checked = true;
        _cmbBackupInterval.SelectedIndex = 2;
        _txtBackupPath.Text = @"C:\POS_Backups";
        _chkSoundsEnabled.Checked = true;
        _trkVolume.Value = 70;
        _lblVolumeValue.Text = "70%";
        _chkOrderSound.Checked = true;
        _chkPaymentSound.Checked = true;
        _chkErrorSound.Checked = true;
    }

    public Dictionary<string, object> CollectSettings()
    {
        return new Dictionary<string, object>
        {
            ["BusinessName"] = _txtBusinessName.Text,
            ["Address"] = _txtAddress.Text,
            ["Phone"] = _txtPhone.Text,
            ["TaxNumber"] = _txtTaxNumber.Text,
            ["CurrencySymbol"] = _txtCurrencySymbol.Text,
            ["Decimals"] = (int)_numDecimals.Value,
            ["DefaultTaxRate"] = _numDefaultTaxRate.Value,
            ["TaxInclusive"] = _chkTaxInclusive.Checked,
            ["MaxLoginAttempts"] = (int)_numMaxLoginAttempts.Value,
            ["SessionTimeout"] = (int)_numSessionTimeout.Value,
            ["MinPasswordLength"] = (int)_numMinPasswordLength.Value,
            ["RequireSpecialChars"] = _chkRequireSpecialChars.Checked,
            ["RequireNumbers"] = _chkRequireNumbers.Checked,
            ["AutoBackup"] = _chkAutoBackup.Checked,
            ["BackupInterval"] = _cmbBackupInterval.SelectedIndex,
            ["BackupPath"] = _txtBackupPath.Text,
            ["SoundsEnabled"] = _chkSoundsEnabled.Checked,
            ["Volume"] = _trkVolume.Value,
            ["OrderSound"] = _chkOrderSound.Checked,
            ["PaymentSound"] = _chkPaymentSound.Checked,
            ["ErrorSound"] = _chkErrorSound.Checked
        };
    }

    public void LoadSettings(Dictionary<string, object> settings)
    {
        if (settings.TryGetValue("BusinessName", out var bn)) _txtBusinessName.Text = bn?.ToString() ?? "";
        if (settings.TryGetValue("Address", out var addr)) _txtAddress.Text = addr?.ToString() ?? "";
        if (settings.TryGetValue("Phone", out var ph)) _txtPhone.Text = ph?.ToString() ?? "";
        if (settings.TryGetValue("TaxNumber", out var tn)) _txtTaxNumber.Text = tn?.ToString() ?? "";
        if (settings.TryGetValue("DefaultTaxRate", out var tr)) _numDefaultTaxRate.Value = Convert.ToDecimal(tr);
        if (settings.TryGetValue("TaxInclusive", out var ti)) _chkTaxInclusive.Checked = (bool)ti;
        if (settings.TryGetValue("MaxLoginAttempts", out var mla)) _numMaxLoginAttempts.Value = Convert.ToDecimal(mla);
        if (settings.TryGetValue("SessionTimeout", out var st)) _numSessionTimeout.Value = Convert.ToDecimal(st);
        if (settings.TryGetValue("AutoBackup", out var ab)) _chkAutoBackup.Checked = (bool)ab;
        if (settings.TryGetValue("SoundsEnabled", out var se)) _chkSoundsEnabled.Checked = (bool)se;
        if (settings.TryGetValue("Volume", out var vol)) _trkVolume.Value = Convert.ToInt32(vol);
    }
}