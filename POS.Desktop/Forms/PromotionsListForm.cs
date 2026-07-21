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
            Text = "âž• Ø¥Ø¶Ø§ÙØ© Ø¹Ø±Ø¶",
            Type = RtlButton.ButtonType.Primary,
            Width = 140,
            Height = DesignTokens.ControlHeight.Standard
        };
        _btnAdd.Click += (s, e) => ShowPromotionDialog(null);

        _btnRefresh = new RtlButton
        {
            Text = "ðŸ”„ ØªØ­Ø¯ÙŠØ«",
            Type = RtlButton.ButtonType.Ghost,
            Width = 90,
            Height = DesignTokens.ControlHeight.Standard,
            Margin = new Padding(DesignTokens.Spacing.Small, 0, 0, 0)
        };
        _btnRefresh.Click += async (s, e) => await LoadDataAsync();

        _lblCount = new Label
        {
            Text = "Ø§Ù„Ø¹Ø±ÙˆØ¶: Ù ",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.TextSecondary,
            AutoSize = true,
            Margin = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Standard, 0)
        };

        _txtSearch = new RtlTextBox
        {
            PlaceholderText = "ðŸ” Ø¨Ø­Ø« Ø¨Ø§Ù„Ø§Ø³Ù…...",
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

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ø§Ø³Ù…", Name = "Name", FillWeight = 18 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ù†ÙˆØ¹", Name = "Type", FillWeight = 10 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ù‚ÙŠÙ…Ø©", Name = "Value", FillWeight = 8 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ù…Ù†", Name = "StartDate", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø¥Ù„Ù‰", Name = "EndDate", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ø£ÙˆÙ„ÙˆÙŠØ©", Name = "Priority", FillWeight = 6 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ø­Ø§Ù„Ø©", Name = "Status", FillWeight = 8 });
        _grid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Ø¥Ø¬Ø±Ø§Ø¡Ø§Øª", Name = "Actions", FillWeight = 8, Text = "Ø¥Ø¬Ø±Ø§Ø¡Ø§Øª", UseColumnTextForButtonValue = true });

        _grid.CellClick += Grid_CellClick;
        _grid.CellFormatting += Grid_CellFormatting;

        _loadingOverlay = ThemeManager.CreateLoadingPanel("Ø¬Ø§Ø±ÙŠ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ø¹Ø±ÙˆØ¶ Ø§Ù„ØªØ±ÙˆÙŠØ¬ÙŠØ©...");
        _loadingOverlay.Visible = false;

        _emptyOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _emptyOverlay.Controls.Add(new Label
        {
            Text = "Ù„Ø§ ØªÙˆØ¬Ø¯ Ø¹Ø±ÙˆØ¶ ØªØ±ÙˆÙŠØ¬ÙŠØ©",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        });

        _errorOverlay = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _errorMessage = new Label
        {
            Text = "Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ø¹Ø±ÙˆØ¶",
            Font = DesignTokens.Typography.SectionTitle,
            ForeColor = DesignTokens.Colors.Error,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        var btnRetry = new RtlButton
        {
            Text = "ðŸ”„ Ø¥Ø¹Ø§Ø¯Ø© Ø§Ù„Ù…Ø­Ø§ÙˆÙ„Ø©",
            Type = RtlButton.ButtonType.Primary,
            Width = 160,
            Height = DesignTokens.ControlHeight.Standard,
            Dock = DockStyle.Bottom
        };
        btnRetry.Click += async (s, e) => await LoadDataAsync();
        _errorOverlay.Controls.Add(btnRetry);
        _errorOverlay.Controls.Add(_errorMessage);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.Colors.Background, Visible = false };
        _permissionPanel.Controls.Add(new Label { Text = "Ù„ÙŠØ³ Ù„Ø¯ÙŠÙƒ ØµÙ„Ø§Ø­ÙŠØ© Ù„Ø¹Ø±Ø¶ Ø§Ù„Ø¹Ø±ÙˆØ¶ Ø§Ù„ØªØ±ÙˆÙŠØ¬ÙŠØ©", Font = DesignTokens.Typography.SectionTitle, ForeColor = DesignTokens.Colors.TextSecondary, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });

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
            _errorMessage.Text = $"Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ø¹Ø±ÙˆØ¶: {ex.Message}";
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
        _lblCount.Text = $"Ø§Ù„Ø¹Ø±ÙˆØ¶: {_filteredPromotions.Count}";
        SetState(_filteredPromotions.Count > 0 ? PromoState.Loaded : PromoState.Empty);
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        foreach (var p in _filteredPromotions)
        {
            var typeLabel = p.Type switch
            {
                "Percentage" => "Ù†Ø³Ø¨Ø© Ù…Ø¦ÙˆÙŠØ©",
                "FixedAmount" => "Ù…Ø¨Ù„Øº Ø«Ø§Ø¨Øª",
                "BuyXGetY" => "Ø§Ø´ØªØ± X ÙˆØ§Ø­ØµÙ„ Ø¹Ù„Ù‰ Y",
                "MultiBuy" => "Ø®ØµÙ… Ø§Ù„ÙƒÙ…ÙŠØ©",
                _ => p.Type
            };
            var valueDisplay = p.Type == "Percentage" ? $"{p.Value}%" : $"{p.Value} Ø¯.Ø£";
            var status = p.IsActive ? "Ù†Ø´Ø·" : "Ù…ØªÙˆÙ‚Ù";

            _grid.Rows.Add(p.Name, typeLabel, valueDisplay,
                p.StartDate.ToString("yyyy-MM-dd"), p.EndDate.ToString("yyyy-MM-dd"),
                p.Priority, status, "Ø¥Ø¬Ø±Ø§Ø¡Ø§Øª");
            _grid.Rows[_grid.Rows.Count - 1].Tag = p;
        }
    }

    private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name == "Status")
        {
            var text = e.Value?.ToString();
            e.CellStyle.ForeColor = text == "Ù†Ø´Ø·" ? DesignTokens.Colors.Success : DesignTokens.Colors.Disabled;
        }
    }

    private void Grid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_grid.Columns[e.ColumnIndex].Name != "Actions") return;

        var promo = _grid.Rows[e.RowIndex].Tag as PromotionDto;
        if (promo == null) return;

        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };
        var editItem = new ToolStripMenuItem("âœï¸ ØªØ¹Ø¯ÙŠÙ„");
        editItem.Click += (s, e) => ShowPromotionDialog(promo);
        menu.Items.Add(editItem);

        menu.Items.Add(new ToolStripSeparator());

        var toggleText = promo.IsActive ? "Ø¥ÙŠÙ‚Ø§Ù" : "ØªÙØ¹ÙŠÙ„";
        var toggleItem = new ToolStripMenuItem(promo.IsActive ? "â¸ Ø¥ÙŠÙ‚Ø§Ù" : "â–¶ï¸ ØªÙØ¹ÙŠÙ„");
        toggleItem.Click += (s, e) => TogglePromotion(promo);
        menu.Items.Add(toggleItem);

        menu.Items.Add(new ToolStripSeparator());

        var deleteItem = new ToolStripMenuItem("ðŸ—‘ Ø­Ø°Ù");
        deleteItem.Click += (s, e) => DeletePromotion(promo);
        menu.Items.Add(deleteItem);

        var cellRect = _grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
        menu.Show(_grid, cellRect.Left, cellRect.Bottom);
    }

    private void ShowPromotionDialog(PromotionDto? existing)
    {
        var isEdit = existing != null;
        var dialog = new RtlDialog(isEdit ? "ØªØ¹Ø¯ÙŠÙ„ Ø¹Ø±Ø¶ ØªØ±ÙˆÙŠØ¬ÙŠ" : "Ø¥Ø¶Ø§ÙØ© Ø¹Ø±Ø¶ ØªØ±ÙˆÙŠØ¬ÙŠ Ø¬Ø¯ÙŠØ¯", 520, 520);

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
        layout.Controls.Add(CreateLabel("Ø§Ù„Ø§Ø³Ù…:"), 0, row);
        var txtName = new RtlTextBox { Text = existing?.Name ?? "", Dock = DockStyle.Fill, IsRequired = true };
        layout.Controls.Add(txtName, 1, row++);

        layout.Controls.Add(CreateLabel("Ø§Ù„ÙˆØµÙ:"), 0, row);
        var txtDesc = new RtlTextBox { Text = existing?.Description ?? "", Dock = DockStyle.Fill };
        layout.Controls.Add(txtDesc, 1, row++);

        layout.Controls.Add(CreateLabel("Ø§Ù„Ù†ÙˆØ¹:"), 0, row);
        var cmbType = new RtlComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        cmbType.Items.AddRange(new object[] { "Percentage", "FixedAmount" });
        cmbType.SelectedItem = existing?.Type ?? "Percentage";
        layout.Controls.Add(cmbType, 1, row++);

        layout.Controls.Add(CreateLabel("Ø§Ù„Ù‚ÙŠÙ…Ø©:"), 0, row);
        var txtValue = new RtlTextBox { Text = existing?.Value.ToString() ?? "10", Dock = DockStyle.Fill };
        layout.Controls.Add(txtValue, 1, row++);

        layout.Controls.Add(CreateLabel("ØªØ§Ø±ÙŠØ® Ø§Ù„Ø¨Ø¯Ø§ÙŠØ©:"), 0, row);
        var dtStart = new DateTimePicker { Value = existing?.StartDate ?? DateTime.Today, Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, Format = DateTimePickerFormat.Short };
        layout.Controls.Add(dtStart, 1, row++);

        layout.Controls.Add(CreateLabel("ØªØ§Ø±ÙŠØ® Ø§Ù„Ù†Ù‡Ø§ÙŠØ©:"), 0, row);
        var dtEnd = new DateTimePicker { Value = existing?.EndDate ?? DateTime.Today.AddMonths(1), Dock = DockStyle.Fill, RightToLeft = RightToLeft.Yes, Format = DateTimePickerFormat.Short };
        layout.Controls.Add(dtEnd, 1, row++);

        layout.Controls.Add(CreateLabel("Ø§Ù„Ø£ÙˆÙ„ÙˆÙŠØ©:"), 0, row);
        var txtPriority = new RtlTextBox { Text = existing?.Priority.ToString() ?? "0", Dock = DockStyle.Fill };
        layout.Controls.Add(txtPriority, 1, row++);

        layout.Controls.Add(CreateLabel("Ø§Ù„Ø­Ø¯ Ø§Ù„Ø£Ø¯Ù†Ù‰ Ù„Ù„Ø´Ø±Ø§Ø¡:"), 0, row);
        var txtMinPurchase = new RtlTextBox
        {
            Text = existing?.MinPurchaseAmount?.ToString() ?? "",
            Dock = DockStyle.Fill,
            PlaceholderText = "0 = Ø¨Ø¯ÙˆÙ† Ø­Ø¯ Ø£Ø¯Ù†Ù‰"
        };
        layout.Controls.Add(txtMinPurchase, 1, row++);

        layout.Controls.Add(CreateLabel("Ø§Ù„Ø­Ø¯ Ø§Ù„Ø£Ù‚ØµÙ‰ Ù„Ù„Ø§Ø³ØªØ®Ø¯Ø§Ù…:"), 0, row);
        var txtMaxApps = new RtlTextBox { Text = existing?.MaxApplications.ToString() ?? "99", Dock = DockStyle.Fill };
        layout.Controls.Add(txtMaxApps, 1, row++);

        layout.Controls.Add(CreateLabel("Ø­Ø§Ù„Ø© Ø§Ù„Ø¹Ø±Ø¶:"), 0, row);
        var chkActive = new CheckBox
        {
            Text = "Ø§Ù„Ø¹Ø±Ø¶ Ù†Ø´Ø·",
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.Body,
            Checked = existing?.IsActive ?? true,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight
        };
        layout.Controls.Add(chkActive, 1, row++);

        dialog.ContentArea.Controls.Add(layout);

        dialog.AddAction(isEdit ? "ØªØ­Ø¯ÙŠØ«" : "Ø¥Ø¶Ø§ÙØ©", async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                RtlMessageBox.Show("ÙŠØ±Ø¬Ù‰ Ø¥Ø¯Ø®Ø§Ù„ Ø§Ø³Ù… Ø§Ù„Ø¹Ø±Ø¶", "ØªÙ†Ø¨ÙŠÙ‡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!decimal.TryParse(txtValue.Text.Trim(), out var value) || value <= 0)
            {
                RtlMessageBox.Show("ÙŠØ±Ø¬Ù‰ Ø¥Ø¯Ø®Ø§Ù„ Ù‚ÙŠÙ…Ø© ØµØ­ÙŠØ­Ø© Ø£ÙƒØ¨Ø± Ù…Ù† ØµÙØ±", "ØªÙ†Ø¨ÙŠÙ‡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (dtEnd.Value <= dtStart.Value)
            {
                RtlMessageBox.Show("ØªØ§Ø±ÙŠØ® Ø§Ù„Ù†Ù‡Ø§ÙŠØ© ÙŠØ¬Ø¨ Ø£Ù† ÙŠÙƒÙˆÙ† Ø¨Ø¹Ø¯ ØªØ§Ø±ÙŠØ® Ø§Ù„Ø¨Ø¯Ø§ÙŠØ©", "ØªÙ†Ø¨ÙŠÙ‡", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                RtlMessageBox.Show($"Ø®Ø·Ø£: {ex.Message}", "Ø®Ø·Ø£", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        });
        dialog.AddAction("Ø¥Ù„ØºØ§Ø¡", (s, e) => dialog.Close(), false);

        dialog.ShowDialog(this.FindForm());
    }

    private async void TogglePromotion(PromotionDto promo)
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
            RtlMessageBox.Show($"Ø®Ø·Ø£: {ex.Message}", "Ø®Ø·Ø£", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void DeletePromotion(PromotionDto promo)
    {
        var result = RtlDialog.ShowDestructiveConfirm(
            "Ø­Ø°Ù Ø¹Ø±Ø¶ ØªØ±ÙˆÙŠØ¬ÙŠ",
            $"Ù‡Ù„ Ø£Ù†Øª Ù…ØªØ£ÙƒØ¯ Ù…Ù† Ø­Ø°Ù Ø§Ù„Ø¹Ø±Ø¶ \"{promo.Name}\"ØŸ"
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
            RtlMessageBox.Show($"Ø®Ø·Ø£: {ex.Message}", "Ø®Ø·Ø£", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
