using System.Drawing;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Themes;
using POS.Desktop.CustomControls;

namespace POS.Desktop.Forms;

public class PromotionsListForm : UserControl
{
    private enum PromoState { Loading, Loaded, Empty, Error, PermissionDenied }

    private PromoState _currentState = PromoState.Loading;
    private List<PromotionDto> _promotions = new();
    private List<PromotionDto> _filteredPromotions = new();
    private string _searchText = "";
    private readonly IPromotionService _promotionService;

    private Panel _toolbarPanel = null!;
    private RtlTextBox _txtSearch = null!;
    private RtlButton _btnAdd = null!;
    private RtlButton _btnRefresh = null!;
    private Label _lblCount = null!;
    private RtlDataGridView _grid = null!;
    private Panel _loadingOverlay = null!;
    private Panel _emptyOverlay = null!;
    private Panel _errorOverlay = null!;
    private Label _errorMessage = null!;
    private Panel _permissionPanel = null!;

    public PromotionsListForm(IPromotionService promotionService)
    {
        _promotionService = promotionService;
        InitializeComponent();
        SetState(PromoState.Loading);
        _ = LoadDataAsync();
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.Colors.Background;
        Font = DesignTokens.Typography.Body;
        Dock = DockStyle.Fill;

        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = DesignTokens.ControlHeight.Large + DesignTokens.Spacing.Compact,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };

        var toolbarInner = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false
        };

        _btnAdd = new RtlButton
        {
            Text = "➕ إضافة عرض",
            Type = RtlButton.ButtonType.Primary,
            Width = 140,
            Height = DesignTokens.ControlHeight.Standard
        };
        _btnAdd.Click += (s, e) => ShowPromotionDialog(null);

        _btnRefresh = new RtlButton
        {
            Text = "🔄 تحديث",
            Type = RtlButton.ButtonType.Ghost,
            Width = 90,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };
        _btnRefresh.Click += async (s, e) => await LoadDataAsync();

        _lblCount = new Label
        {
            Text = "العروض: ٠",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Standard, 0)
        };

        _txtSearch = new RtlTextBox
        {
            PlaceholderText = "🔍 بحث بالاسم...",
            Width = 300,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(0, 0, DesignTokens.Spacing.Compact, 0)
        };
        _txtSearch.TextChanged += (s, e) =>
        {
            _searchText = _txtSearch.Text.Trim();
            ApplyFilter();
        };

        toolbarInner.Controls.Add(_btnAdd);
        toolbarInner.Controls.Add(_btnRefresh);
        toolbarInner.Controls.Add(_lblCount);
        toolbarInner.Controls.Add(_txtSearch);
        _toolbarPanel.Controls.Add(toolbarInner);

        _grid = new RtlDataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = DesignTokens.Colors.Background,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الاسم", Name = "Name", FillWeight = 18 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "النوع", Name = "Type", FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "القيمة", Name = "Value", FillWeight = 8 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "من", Name = "StartDate", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "إلى", Name = "EndDate", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الأولوية", Name = "Priority", FillWeight = 6 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الحالة", Name = "Status", FillWeight = 8 });
        _grid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "إجراءات", Name = "Actions", FillWeight = 8, Text = "إجراءات", UseColumnTextForButtonValue = true });

        _grid.CellClick += Grid_CellClick;
        _grid.CellFormatting += Grid_CellFormatting;

        _loadingOverlay = ThemeManager.CreateLoadingPanel("جاري تحميل العروض الترويجية...");
        _loadingOverlay.Visible = false;

        _emptyOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _emptyOverlay.Controls.Add(new Label
        {
            Text = "لا توجد عروض ترويجية",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        });

        _errorOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _errorMessage = new Label
        {
            Text = "حدث خطأ أثناء تحميل العروض",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.Error,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        var btnRetry = new RtlButton
        {
            Text = "🔄 إعادة المحاولة",
            Type = RtlButton.ButtonType.Primary,
            Width = 160,
            Height = DesignTokens.ControlHeight.Standard,
            Dock = DockStyle.Bottom
        };
        btnRetry.Click += async (s, e) => await LoadDataAsync();
        _errorOverlay.Controls.Add(btnRetry);
        _errorOverlay.Controls.Add(_errorMessage);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label { Text = "ليس لديك صلاحية لعرض العروض الترويجية", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

        Controls.Add(_loadingOverlay);
        Controls.Add(_emptyOverlay);
        Controls.Add(_errorOverlay);
        Controls.Add(_permissionPanel);
        Controls.Add(_grid);
        Controls.Add(_toolbarPanel);
    }

    private void SetState(PromoState state)
    {
        _currentState = state;
        _loadingOverlay.Visible = state == PromoState.Loading;
        _emptyOverlay.Visible = state == PromoState.Empty;
        _errorOverlay.Visible = state == PromoState.Error;
        _permissionPanel.Visible = state == PromoState.PermissionDenied;
        _grid.Visible = state == PromoState.Loaded;
        _btnAdd.Enabled = state == PromoState.Loaded;
        _btnRefresh.Enabled = state != PromoState.Loading;
    }

    private async Task LoadDataAsync()
    {
        SetState(PromoState.Loading);
        try
        {
            _promotions = await _promotionService.GetAllAsync();
            ApplyFilter();
            SetState(_filteredPromotions.Count > 0 ? PromoState.Loaded : PromoState.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError("Load promotions failed: {0}", ex);
            _errorMessage.Text = "حدث خطأ أثناء تحميل العروض";
            SetState(PromoState.Error);
        }
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrEmpty(_searchText))
            _filteredPromotions = _promotions.ToList();
        else
        {
            var search = _searchText.ToLower();
            _filteredPromotions = _promotions.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        PopulateGrid();
        _lblCount.Text = $"العروض: {_filteredPromotions.Count}";
        SetState(_filteredPromotions.Count > 0 ? PromoState.Loaded : PromoState.Empty);
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        foreach (var p in _filteredPromotions)
        {
            var typeLabel = p.Type switch
            {
                "Percentage" => "نسبة مئوية",
                "FixedAmount" => "مبلغ ثابت",
                "BuyXGetY" => "اشتر X واحصل على Y",
                "MultiBuy" => "خصم الكمية",
                _ => p.Type
            };
            var valueDisplay = p.Type == "Percentage" ? $"{p.Value}%" : $"{p.Value} د.أ";
            var status = p.IsActive ? "نشط" : "متوقف";

            _grid.Rows.Add(p.Name, typeLabel, valueDisplay,
                p.StartDate.ToString("yyyy-MM-dd"), p.EndDate.ToString("yyyy-MM-dd"),
                p.Priority, status, "إجراءات");
            _grid.Rows[_grid.Rows.Count - 1].Tag = p;
        }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name == "Status")
        {
            var text = e.Value?.ToString();
            e.CellStyle.ForeColor = text == "نشط" ? DesignTokens.Colors.Success : DesignTokens.Colors.Disabled;
        }
    }

    private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name != "Actions") return;

        var promo = _grid.Rows[e.RowIndex].Tag as PromotionDto;
        if (promo == null) return;

        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
        var editItem = new ToolStripMenuItem("✏️ تعديل");
        editItem.Click += (s, e) => ShowPromotionDialog(promo);
        menu.Items.Add(editItem);

        menu.Items.Add(new ToolStripSeparator());

        var toggleText = promo.IsActive ? "إيقاف" : "تفعيل";
        var toggleItem = new ToolStripMenuItem(promo.IsActive ? "⏸ إيقاف" : "▶️ تفعيل");
        toggleItem.Click += (s, e) => _ = TogglePromotionAsync(promo);
        menu.Items.Add(toggleItem);

        menu.Items.Add(new ToolStripSeparator());

        var deleteItem = new ToolStripMenuItem("🗑 حذف");
        deleteItem.Click += (s, e) => DeletePromotion(promo);
        menu.Items.Add(deleteItem);

        var cellRect = _grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
        menu.Show(_grid, cellRect.Left, cellRect.Bottom);
    }

    private void ShowPromotionDialog(PromotionDto? existing)
    {
        var isEdit = existing != null;
        var dialog = new RtlDialog(isEdit ? "تعديل عرض ترويجي" : "إضافة عرض ترويجي جديد", 520, 520);

        var layout = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 11,
            Dock = DockStyle.Fill,
            RightToLeft = RightToLeft.Yes,
            BackColor = DesignTokens.Colors.Surface,
            Padding = new Padding(DesignTokens.Spacing.Standard)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 11; i++) layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

        int row = 0;
        layout.Controls.Add(CreateLabel("الاسم:"), 0, row);
        var txtName = new RtlTextBox { Text = existing?.Name ?? "", Dock = DockStyle.Fill, IsRequired = true };
        layout.Controls.Add(txtName, 1, row++);

        layout.Controls.Add(CreateLabel("الوصف:"), 0, row);
        var txtDesc = new RtlTextBox { Text = existing?.Description ?? "", Dock = DockStyle.Fill };
        layout.Controls.Add(txtDesc, 1, row++);

        layout.Controls.Add(CreateLabel("النوع:"), 0, row);
        var cmbType = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        cmbType.Items.AddRange(new object[] { "Percentage", "FixedAmount" });
        cmbType.SelectedItem = existing?.Type ?? "Percentage";
        layout.Controls.Add(cmbType, 1, row++);

        layout.Controls.Add(CreateLabel("القيمة:"), 0, row);
        var txtValue = new RtlTextBox { Text = existing?.Value.ToString() ?? "10", Dock = DockStyle.Fill };
        layout.Controls.Add(txtValue, 1, row++);

        layout.Controls.Add(CreateLabel("تاريخ البداية:"), 0, row);
        var dtStart = new DateTimePicker { Value = existing?.StartDate ?? DateTime.Today, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, Format = DateTimePickerFormat.Short };
        layout.Controls.Add(dtStart, 1, row++);

        layout.Controls.Add(CreateLabel("تاريخ النهاية:"), 0, row);
        var dtEnd = new DateTimePicker { Value = existing?.EndDate ?? DateTime.Today.AddMonths(1), Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, Format = DateTimePickerFormat.Short };
        layout.Controls.Add(dtEnd, 1, row++);

        layout.Controls.Add(CreateLabel("الأولوية:"), 0, row);
        var txtPriority = new RtlTextBox { Text = existing?.Priority.ToString() ?? "0", Dock = DockStyle.Fill };
        layout.Controls.Add(txtPriority, 1, row++);

        layout.Controls.Add(CreateLabel("الحد الأدنى للشراء:"), 0, row);
        var txtMinPurchase = new RtlTextBox
        {
            Text = existing?.MinPurchaseAmount?.ToString() ?? "",
            Dock = DockStyle.Fill,
            PlaceholderText = "0 = بدون حد أدنى"
        };
        layout.Controls.Add(txtMinPurchase, 1, row++);

        layout.Controls.Add(CreateLabel("الحد الأقصى للاستخدام:"), 0, row);
        var txtMaxApps = new RtlTextBox { Text = existing?.MaxApplications.ToString() ?? "99", Dock = DockStyle.Fill };
        layout.Controls.Add(txtMaxApps, 1, row++);

        layout.Controls.Add(CreateLabel("حالة العرض:"), 0, row);
        var chkActive = new CheckBox
        {
            Text = "العرض نشط",
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.Body,
            Checked = existing?.IsActive ?? true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        layout.Controls.Add(chkActive, 1, row++);

        dialog.ContentArea.Controls.Add(layout);

        dialog.AddAction(isEdit ? "تحديث" : "إضافة", async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                RtlMessageBox.Show("يرجى إدخال اسم العرض", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtValue.Text.Trim(), out var value) || value <= 0)
            {
                RtlMessageBox.Show("يرجى إدخال قيمة صحيحة أكبر من صفر", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (dtEnd.Value <= dtStart.Value)
            {
                RtlMessageBox.Show("تاريخ النهاية يجب أن يكون بعد تاريخ البداية", "تنبيه", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var type = cmbType.SelectedItem?.ToString() ?? "Percentage";
                decimal? minPurchase = null;
                if (decimal.TryParse(txtMinPurchase.Text.Trim(), out var mp) && mp > 0)
                    minPurchase = mp;
                int maxApps = int.TryParse(txtMaxApps.Text.Trim(), out var ma) ? ma : 99;
                int priority = int.TryParse(txtPriority.Text.Trim(), out var pr) ? pr : 0;

                if (isEdit && existing != null)
                {
                    await _promotionService.UpdateAsync(new UpdatePromotionRequest(
                        existing.Id, txtName.Text, txtDesc.Text, type, value,
                        dtStart.Value, dtEnd.Value, chkActive.Checked, priority,
                        minPurchase, null, null, null, maxApps));
                }
                else
                {
                    await _promotionService.CreateAsync(new CreatePromotionRequest(
                        txtName.Text, txtDesc.Text, type, value,
                        dtStart.Value, dtEnd.Value, minPurchase,
                        null, null, null, maxApps));
                }

                dialog.Close();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"[PromotionsListForm] SavePromotion failed: {ex}");
                RtlMessageBox.Show("حدث خطأ أثناء حفظ العرض", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        dialog.AddAction("إلغاء", (s, e) => dialog.Close(), false);

        dialog.ShowDialog(this.FindForm());
    }

    private async Task TogglePromotionAsync(PromotionDto promo)
    {
        try
        {
            await _promotionService.UpdateAsync(new UpdatePromotionRequest(
                promo.Id, promo.Name, promo.Description, promo.Type, promo.Value,
                promo.StartDate, promo.EndDate, !promo.IsActive, promo.Priority,
                promo.MinPurchaseAmount, promo.MinQuantity, promo.BuyQuantity,
                promo.FreeQuantity, promo.MaxApplications,
                promo.ApplicableProductIdsJson, promo.ApplicableCategoryIdsJson));
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PromotionsListForm] TogglePromotionAsync failed: {ex}");
            RtlMessageBox.Show("حدث خطأ أثناء تحديث العرض", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeletePromotion(PromotionDto promo)
    {
        var result = RtlDialog.ShowDestructiveConfirm(
            "حذف عرض ترويجي",
            $"هل أنت متأكد من حذف العرض \"{promo.Name}\"؟"
        );
        if (result == DialogResult.OK)
        {
            _ = DeletePromotionAsync(promo);
        }
    }

    private async Task DeletePromotionAsync(PromotionDto promo)
    {
        try
        {
            await _promotionService.DeleteAsync(promo.Id);
            _promotions.Remove(promo);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[PromotionsListForm] DeletePromotionAsync failed: {ex}");
            RtlMessageBox.Show("حدث خطأ أثناء حذف العرض", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

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
}
