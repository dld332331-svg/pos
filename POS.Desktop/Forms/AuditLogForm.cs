using System.Drawing;
using System.Windows.Forms;
using POS.Application.Services;
using POS.Application.DTOs;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;
using POS.Desktop.Icons;

namespace POS.Desktop.Forms;

/// <summary>
/// AUD-001: Audit log viewer UserControl.
/// Top filter bar: date from/to, action type, entity type, search button.
/// Main: RtlDataGridView with 8 columns (read-only).
/// Bottom: pagination (prev/next, page label, page size combo).
/// Total count label. Empty state message.
/// </summary>
public class AuditLogForm : UserControl
{
    private enum AuditState
    {
        Idle,
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private AuditState _currentState = AuditState.Idle;
    private readonly IAuditQueryService _auditQueryService;
    private PagedResult<AuditLogDto>? _pagedResult = null;
    private int _currentPage = 1;
    private int _pageSize = 20;

    // UI Controls - Filter Bar
    private Panel _filterPanel;
    private DateTimePicker _dtpFromDate;
    private DateTimePicker _dtpToDate;
    private Label _lblFromDate;
    private Label _lblToDate;
    private Label _lblActionType;
    private Label _lblEntityType;
    private RtlComboBox _cmbActionType;
    private RtlComboBox _cmbEntityType;
    private RtlButton _btnSearch;
    private RtlButton _btnClearFilters;

    // UI Controls - Data Grid
    private RtlDataGridView _logGrid;

    // UI Controls - Pagination
    private Panel _paginationPanel;
    private RtlButton _btnPrevPage;
    private RtlButton _btnNextPage;
    private Label _lblPageInfo;
    private RtlComboBox _cmbPageSize;
    private Label _lblTotalCount;

    // UI Controls - Overlays
    private Panel _loadingOverlay;
    private Panel _emptyOverlay;
    private Panel _permissionPanel = null!;

    public AuditLogForm(IAuditQueryService auditQueryService)
    {
        _auditQueryService = auditQueryService;
        InitializeComponent();
        SetState(AuditState.Idle);
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.Colors.Background;
        Font = DesignTokens.Typography.Body;
        Dock = DockStyle.Fill;

        // === Filter Bar ===
        _filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            Margin = new Padding(0, 0, 0, DesignTokens.Spacing.Compact)
        };

        var filterRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Height = DesignTokens.ControlHeight.Standard
        };

        _lblFromDate = CreateFilterLabel("من تاريخ:");
        _dtpFromDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            RightToLeft = RightToLeft.Yes,
            Value = DateTime.Today.AddDays(-30),
            Width = 130,
            Height = DesignTokens.ControlHeight.Standard,
            Font = DesignTokens.Typography.Input,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0)
        };

        _lblToDate = CreateFilterLabel("إلى تاريخ:");
        _dtpToDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            RightToLeft = RightToLeft.Yes,
            Value = DateTime.Now,
            Width = 130,
            Height = DesignTokens.ControlHeight.Standard,
            Font = DesignTokens.Typography.Input,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0)
        };

        _lblActionType = CreateFilterLabel("نوع الإجراء:");
        _cmbActionType = new RtlComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 120,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0)
        };
        _cmbActionType.Items.AddRange(new object[] { "الكل", "إنشاء", "تعديل", "حذف", "تسجيل دخول", "تسجيل خروج" });
        _cmbActionType.SelectedIndex = 0;

        _lblEntityType = CreateFilterLabel("الكيان:");
        _cmbEntityType = new RtlComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 120,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0)
        };
        _cmbEntityType.Items.AddRange(new object[] { "الكل", "منتج", "طلب", "مستخدم", "عميل", "إعدادات", "مخزون" });
        _cmbEntityType.SelectedIndex = 0;

        _btnSearch = new RtlButton
        {
            Text = "🔍 بحث",
            Type = RtlButton.ButtonType.Primary,
            Width = 90,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Small, 0)
        };
        _btnSearch.Click += async (s, e) => await SearchAsync();

        _btnClearFilters = new RtlButton
        {
            Text = "مسح",
            Type = RtlButton.ButtonType.Ghost,
            Width = 70,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, 0, 0, 0)
        };
        _btnClearFilters.Click += (s, e) => ClearFilters();

        filterRow.Controls.Add(_btnSearch);
        filterRow.Controls.Add(_btnClearFilters);
        filterRow.Controls.Add(_lblEntityType);
        filterRow.Controls.Add(_cmbEntityType);
        filterRow.Controls.Add(_lblActionType);
        filterRow.Controls.Add(_cmbActionType);
        filterRow.Controls.Add(_lblToDate);
        filterRow.Controls.Add(_dtpToDate);
        filterRow.Controls.Add(_lblFromDate);
        filterRow.Controls.Add(_dtpFromDate);

        _filterPanel.Controls.Add(filterRow);

        // === Data Grid ===
        _logGrid = new RtlDataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true
        };

        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "التاريخ والوقت", Name = "DateTime", FillWeight = 18 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "المستخدم", Name = "User", FillWeight = 14 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الإجراء", Name = "Action", FillWeight = 10 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الكيان", Name = "Entity", FillWeight = 10 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "معرف الكيان", Name = "EntityId", FillWeight = 10 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "القيمة قبل", Name = "OldValue", FillWeight = 14 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "القيمة بعد", Name = "NewValue", FillWeight = 14 });
        _logGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "السبب", Name = "Reason", FillWeight = 10 });

        _logGrid.CellFormatting += LogGrid_CellFormatting;

        // === Pagination Panel ===
        _paginationPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = DesignTokens.ControlHeight.Large + DesignTokens.Spacing.Compact,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard),
            Margin = new Padding(0, DesignTokens.Spacing.Compact, 0, 0)
        };

        var paginationInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _btnPrevPage = new RtlButton
        {
            Text = $"{RtlIconHelper.GetPaginationArrow(false)} السابق",
            Type = RtlButton.ButtonType.Secondary,
            Width = 100,
            Height = DesignTokens.ControlHeight.Standard
        };
        _btnPrevPage.Click += (s, e) => _ = GoToPageAsync(_currentPage - 1);

        _btnNextPage = new RtlButton
        {
            Text = $"التالي {RtlIconHelper.GetPaginationArrow(true)}",
            Type = RtlButton.ButtonType.Secondary,
            Width = 100,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };
        _btnNextPage.Click += (s, e) => _ = GoToPageAsync(_currentPage + 1);

        _lblPageInfo = new Label
        {
            Text = "صفحة ١ من ١",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextPrimary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Standard, 0),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var lblPageSizeText = new Label
        {
            Text = "حجم الصفحة:",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextPrimary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Micro, 0)
        };

        _cmbPageSize = new RtlComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 70,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Standard, 0)
        };
        _cmbPageSize.Items.AddRange(new object[] { 20, 50, 100 });
        _cmbPageSize.SelectedItem = 20;
        _cmbPageSize.SelectedIndexChanged += async (s, e) =>
        {
            _pageSize = Convert.ToInt32(_cmbPageSize.SelectedItem);
            _currentPage = 1;
            await SearchAsync();
        };

        _lblTotalCount = new Label
        {
            Text = "الإجمالي: ٠ سجل",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0, 0, 0, 0)
        };

        paginationInner.Controls.Add(_btnPrevPage);
        paginationInner.Controls.Add(_btnNextPage);
        paginationInner.Controls.Add(_lblPageInfo);
        paginationInner.Controls.Add(lblPageSizeText);
        paginationInner.Controls.Add(_cmbPageSize);
        paginationInner.Controls.Add(_lblTotalCount);
        _paginationPanel.Controls.Add(paginationInner);

        // === Loading Overlay ===
        _loadingOverlay = ThemeManager.CreateLoadingPanel("جاري تحميل سجلات المراجعة...");
        _loadingOverlay.Visible = false;

        // === Empty Overlay ===
        _emptyOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background,
            Visible = false
        };
        var emptyIcon = new Label
        {
            Text = "📝",
            Font = new Font("Segoe UI", 48f),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 80
        };
        var emptyLabel = new Label
        {
            Text = "لا توجد سجلات مراجعة",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        _emptyOverlay.Controls.Add(emptyLabel);
        _emptyOverlay.Controls.Add(emptyIcon);

        // === Permission Overlay ===
        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label { Text = "ليس لديك صلاحية لعرض سجلات المراجعة", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        // Assemble
        Controls.Add(_loadingOverlay);
        Controls.Add(_emptyOverlay);
        Controls.Add(_permissionPanel);
        Controls.Add(_logGrid);
        Controls.Add(_paginationPanel);
        Controls.Add(_filterPanel);
    }

    // --- Helper Methods ---

    private Label CreateFilterLabel(string text)
    {
        return new Label
        {
            Text = text,
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextPrimary,
            AutoSize = true,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Micro, 0),
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    // --- State Management ---

    private void SetState(AuditState state)
    {
        _currentState = state;
        _loadingOverlay.Visible = state == AuditState.Loading;
        _emptyOverlay.Visible = state == AuditState.Empty;
        _permissionPanel.Visible = state == AuditState.PermissionDenied;
        _logGrid.Visible = state == AuditState.Loaded || state == AuditState.Error;
        _paginationPanel.Visible = state == AuditState.Loaded;
        _btnSearch.Enabled = state != AuditState.Loading;
    }

    // --- Data Loading ---

    private async Task SearchAsync()
    {
        SetState(AuditState.Loading);
        _currentPage = 1;
        await LoadPageAsync();
    }

    private void ClearFilters()
    {
        _dtpFromDate.Value = DateTime.Today.AddDays(-30);
        _dtpToDate.Value = DateTime.Now;
        _cmbActionType.SelectedIndex = 0;
        _cmbEntityType.SelectedIndex = 0;
    }

    private async Task LoadPageAsync()
    {
        try
        {
            var fromDate = _dtpFromDate.Value.Date;
            var toDate = _dtpToDate.Value.Date.AddDays(1);
            var actionFilter = _cmbActionType.SelectedIndex > 0 ? _cmbActionType.SelectedItem?.ToString() : null;
            var entityFilter = _cmbEntityType.SelectedIndex > 0 ? _cmbEntityType.SelectedItem?.ToString() : null;

            _pagedResult = await _auditQueryService.GetAuditLogsAsync(fromDate, toDate, actionFilter, entityFilter, _currentPage, _pageSize);

            _logGrid.Rows.Clear();

            if (_pagedResult.Items.Count == 0)
            {
                SetState(AuditState.Empty);
                return;
            }

            foreach (var log in _pagedResult.Items)
            {
                _logGrid.Rows.Add(
                    log.Timestamp.ToString("yyyy/MM/dd HH:mm:ss"),
                    log.UserName,
                    log.ActionType,
                    log.EntityName,
                    log.EntityId ?? "",
                    log.BeforeValue ?? "-",
                    log.AfterValue ?? "-",
                    log.Reason ?? ""
                );
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling((double)_pagedResult.TotalCount / _pagedResult.PageSize));
            _lblPageInfo.Text = $"صفحة {_pagedResult.Page} من {totalPages}";
            _lblTotalCount.Text = $"الإجمالي: {_pagedResult.TotalCount} سجل";
            _btnPrevPage.Enabled = _currentPage > 1;
            _btnNextPage.Enabled = _currentPage < totalPages;

            _logGrid.ShowEmptyMessage("لا توجد سجلات مراجعة");
            SetState(AuditState.Loaded);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[AuditLogForm] LoadPageAsync failed: {ex}");
            SetState(AuditState.Error);
            _logGrid.Rows.Clear();
            _logGrid.ShowEmptyMessage("حدث خطأ أثناء التحميل");
        }
    }

    private async Task GoToPageAsync(int page)
    {
        try
        {
            if (page < 1 || _pagedResult == null) return;
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)_pagedResult.TotalCount / _pageSize));
            if (page > totalPages) return;
            _currentPage = page;
            await LoadPageAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[AuditLogForm] GoToPageAsync failed: {ex}");
        }
    }

    // --- Cell Formatting ---

    private void LogGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;

        var actionCol = _logGrid.Columns["Action"]?.Index ?? -1;
        if (e.ColumnIndex == actionCol)
        {
            var text = e.Value?.ToString() ?? "";
            e.CellStyle.ForeColor = text switch
            {
                "إنشاء" => DesignTokens.Colors.Success,
                "تعديل" => DesignTokens.Colors.Info,
                "حذف" => DesignTokens.Colors.Error,
                "تسجيل دخول" => DesignTokens.Colors.Primary,
                "تسجيل خروج" => DesignTokens.Colors.TextSecondary,
                _ => DesignTokens.Colors.TextPrimary
            };
            e.CellStyle.Font = DesignTokens.Typography.BodyBold;
        }

        var oldValueCol = _logGrid.Columns["OldValue"]?.Index ?? -1;
        var newValueCol = _logGrid.Columns["NewValue"]?.Index ?? -1;
        if (e.ColumnIndex == oldValueCol || e.ColumnIndex == newValueCol)
        {
            var text = e.Value?.ToString() ?? "";
            if (text == "-")
            {
                e.CellStyle.ForeColor = DesignTokens.Colors.TextSecondary;
                e.CellStyle.Font = DesignTokens.Typography.Secondary;
            }
        }
    }


}