using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;

using POS.Desktop.Icons;
using POS.Desktop.Themes;
namespace POS.Desktop.Forms;

/// <summary>
/// PROD-001: Products management UserControl.
/// Top: search bar + filter combo + add button.
/// Middle: DataGridView with columns (Name, Barcode, Category, Type, Price, Stock, Status, Actions).
/// Bottom: pagination controls. All Arabic labels.
/// </summary>
public class ProductListForm : UserControl
{
    private enum ProductListState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private readonly IProductService _productService;
    private ProductListState _currentState = ProductListState.Loading;
    private int _currentPage = 1;
    private int _pageSize = 20;
    private int _totalCount = 0;
    private Guid? _filterCategoryId;
    private string? _filterType;
    private string? _filterStatus;
    private string? _searchTerm;

    // UI Controls
    private Panel _toolbarPanel;
    private TextBox _searchTextBox;
    private ComboBox _categoryFilterCombo;
    private ComboBox _typeFilterCombo;
    private ComboBox _statusFilterCombo;
    private Button _searchButton;
    private Button _addButton;
    private Button _refreshButton;
    private DataGridView _productsGrid;
    private Panel _paginationPanel;
    private Label _totalCountLabel;
    private Label _pageInfoLabel;
    private Button _prevPageButton;
    private Button _nextPageButton;
    private Panel _loadingPanel;
    private Panel _emptyPanel;
    private Panel _errorPanel;
    private Panel _permissionPanel;
    private Label _errorLabel;
    private Button _retryButton;

    // Events
    public event EventHandler? AddProductRequested;
    public event EventHandler<ProductDto>? EditProductRequested;
    public event EventHandler<Guid>? DeleteProductRequested;

    public ProductListForm(IProductService productService)
    {
        _productService = productService;
        InitializeComponent();
        SetState(ProductListState.Loading);
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.BackgroundColor;
        Font = DesignTokens.DefaultFont;
        Dock = DockStyle.Fill;

        // Toolbar
        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM),
            Margin = new Padding(0, 0, 0, DesignTokens.SpacingSM)
        };

        _searchTextBox = new TextBox
        {
            Location = new Point(280, 10),
            Size = new Size(200, 28),
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes,
            PlaceholderText = "ðŸ” Ø¨Ø­Ø« Ø¨Ø§Ù„Ø§Ø³Ù… Ø£Ùˆ Ø§Ù„Ø¨Ø§Ø±ÙƒÙˆØ¯..."
        };
        _searchTextBox.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; _ = SearchAsync(); } };

        _categoryFilterCombo = CreateFilterCombo(new[] { "Ø¬Ù…ÙŠØ¹ Ø§Ù„ÙØ¦Ø§Øª" }, 240, 10);
        _typeFilterCombo = CreateFilterCombo(new[] { "Ø¬Ù…ÙŠØ¹ Ø§Ù„Ø£Ù†ÙˆØ§Ø¹", "Ø¨Ø³ÙŠØ·", "Ù…ØªØºÙŠØ±", "Ù…Ø±ÙƒØ¨" }, 165, 10);
        _statusFilterCombo = CreateFilterCombo(new[] { "Ø¬Ù…ÙŠØ¹ Ø§Ù„Ø­Ø§Ù„Ø§Øª", "Ù†Ø´Ø·", "ØºÙŠØ± Ù†Ø´Ø·", "Ø£Ø±Ø´ÙŠÙ" }, 90, 10);

        _searchButton = new Button { Text = "Ø¨Ø­Ø«", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Location = new Point(480, 10), Size = new Size(60, 28), BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, Cursor = Cursors.Hand };
        _searchButton.Click += async (s, e) => await SearchAsync();

        _refreshButton = new Button { Text = "ðŸ”„", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Location = new Point(545, 10), Size = new Size(28, 28), BackColor = DesignTokens.CardColor, Cursor = Cursors.Hand };
        _refreshButton.Click += async (s, e) => await LoadDataAsync();

        _addButton = new Button { Text = "âž• Ø¥Ø¶Ø§ÙØ© Ù…Ù†ØªØ¬", Font = DesignTokens.ButtonFont, FlatStyle = FlatStyle.Flat, Location = new Point(10, 8), Size = new Size(130, 32), BackColor = DesignTokens.SuccessColor, ForeColor = Color.White, Cursor = Cursors.Hand };
        _addButton.Click += (s, e) => AddProductRequested?.Invoke(this, EventArgs.Empty);

        _toolbarPanel.Controls.Add(_searchButton);
        _toolbarPanel.Controls.Add(_refreshButton);
        _toolbarPanel.Controls.Add(_searchTextBox);
        _toolbarPanel.Controls.Add(_categoryFilterCombo);
        _toolbarPanel.Controls.Add(_typeFilterCombo);
        _toolbarPanel.Controls.Add(_statusFilterCombo);
        _toolbarPanel.Controls.Add(_addButton);

        // Products DataGridView
        _productsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = DesignTokens.SurfaceColor,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = DesignTokens.BorderColor,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.DataFont,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ø§Ø³Ù…", Name = "Name", FillWeight = 20 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ø¨Ø§Ø±ÙƒÙˆØ¯", Name = "Barcode", FillWeight = 12 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„ÙØ¦Ø©", Name = "Category", FillWeight = 12 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ù†ÙˆØ¹", Name = "Type", FillWeight = 8 });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ø³Ø¹Ø±", Name = "Price", FillWeight = 10, DefaultCellStyle = new DataGridViewCellStyle { Format = "N3", Alignment = DataGridViewContentAlignment.MiddleLeft } });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ù…Ø®Ø²ÙˆÙ†", Name = "Stock", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _productsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ø§Ù„Ø­Ø§Ù„Ø©", Name = "Status", FillWeight = 8 });
        _productsGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "Ø¥Ø¬Ø±Ø§Ø¡Ø§Øª", Name = "Actions", FillWeight = 10, Text = "ØªØ¹Ø¯ÙŠÙ„ / Ø­Ø°Ù", UseColumnTextForButtonValue = true });

        _productsGrid.CellClick += ProductsGrid_CellClick;
        _productsGrid.CellFormatting += ProductsGrid_CellFormatting;

        // Pagination
        _paginationPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM)
        };

        _totalCountLabel = new Label
        {
            Text = "Ø¥Ø¬Ù…Ø§Ù„ÙŠ: Ù ",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Dock = DockStyle.Right,
            Width = 200,
            TextAlign = ContentAlignment.MiddleRight
        };

        _prevPageButton = new Button { Text = $"{RtlIconHelper.GetPaginationArrow(false)} Ø§Ù„Ø³Ø§Ø¨Ù‚", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Size = new Size(90, 28), Dock = DockStyle.Left, Cursor = Cursors.Hand, Enabled = false };
        _prevPageButton.Click += async (s, e) => { _currentPage--; await LoadDataAsync(); };

        _nextPageButton = new Button { Text = $"Ø§Ù„ØªØ§Ù„ÙŠ {RtlIconHelper.GetPaginationArrow(true)}", Font = DesignTokens.DefaultFont, FlatStyle = FlatStyle.Flat, Size = new Size(90, 28), Dock = DockStyle.Left, Cursor = Cursors.Hand, Enabled = false };
        _nextPageButton.Click += async (s, e) => { _currentPage++; await LoadDataAsync(); };

        _pageInfoLabel = new Label
        {
            Text = "ØµÙØ­Ø© Ù¡ Ù…Ù† Ù¡",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _paginationPanel.Controls.Add(_totalCountLabel);
        _paginationPanel.Controls.Add(_prevPageButton);
        _paginationPanel.Controls.Add(_nextPageButton);
        _paginationPanel.Controls.Add(_pageInfoLabel);

        // Loading panel
        _loadingPanel = CreateOverlayPanel("Ø¬Ø§Ø±ÙŠ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ù…Ù†ØªØ¬Ø§Øª...", DesignTokens.TextSecondaryColor);
        _emptyPanel = CreateOverlayPanel("Ù„Ø§ ØªÙˆØ¬Ø¯ Ù…Ù†ØªØ¬Ø§Øª", DesignTokens.TextSecondaryColor);
        _emptyPanel.Visible = false;

        _errorPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor, Visible = false };
        _errorLabel = new Label { Text = "Ø­Ø¯Ø« Ø®Ø·Ø£ Ø£Ø«Ù†Ø§Ø¡ ØªØ­Ù…ÙŠÙ„ Ø§Ù„Ù…Ù†ØªØ¬Ø§Øª", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.ErrorColor, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
        _retryButton = new Button { Text = "Ø¥Ø¹Ø§Ø¯Ø© Ø§Ù„Ù…Ø­Ø§ÙˆÙ„Ø©", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(150, 40), Cursor = Cursors.Hand };
        _retryButton.Location = new Point((Width / 2) - 75, (Height / 2));
        _retryButton.Anchor = AnchorStyles.None;
        _retryButton.Click += async (s, e) => await LoadDataAsync();
        _errorPanel.Controls.Add(_retryButton);
        _errorPanel.Controls.Add(_errorLabel);

        _permissionPanel = CreateOverlayPanel("Ù„ÙŠØ³ Ù„Ø¯ÙŠÙƒ ØµÙ„Ø§Ø­ÙŠØ© Ù„Ø¥Ø¯Ø§Ø±Ø© Ø§Ù„Ù…Ù†ØªØ¬Ø§Øª", DesignTokens.WarningColor);
        _permissionPanel.Visible = false;

        Controls.Add(_loadingPanel);
        Controls.Add(_emptyPanel);
        Controls.Add(_errorPanel);
        Controls.Add(_permissionPanel);
        Controls.Add(_productsGrid);
        Controls.Add(_paginationPanel);
        Controls.Add(_toolbarPanel);
    }

    private ComboBox CreateFilterCombo(string[] items, int x, int y)
    {
        var combo = new ComboBox
        {
            Location = new Point(x, y),
            Width = 70,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = DesignTokens.SmallFont,
            RightToLeft = RightToLeft.Yes,
            FlatStyle = FlatStyle.Flat
        };
        foreach (var item in items) combo.Items.Add(item);
        combo.SelectedIndex = 0;
        // Calculate width based on longest item text
        using (var g = combo.CreateGraphics())
        {
            float maxWidth = items.Max(i => g.MeasureString(i, combo.Font).Width);
            combo.Width = (int)maxWidth + 30;
        }
        return combo;
    }

    private Panel CreateOverlayPanel(string text, Color color)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor };
        var label = new Label
        {
            Text = text,
            Font = DesignTokens.SubheadingFont,
            ForeColor = color,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        panel.Controls.Add(label);
        return panel;
    }

    private void SetState(ProductListState state)
    {
        _currentState = state;
        _loadingPanel.Visible = state == ProductListState.Loading;
        _emptyPanel.Visible = state == ProductListState.Empty;
        _errorPanel.Visible = state == ProductListState.Error;
        _permissionPanel.Visible = state == ProductListState.PermissionDenied;
        _productsGrid.Visible = state == ProductListState.Loaded;
        _paginationPanel.Visible = state == ProductListState.Loaded;
        _addButton.Enabled = state != ProductListState.Loading && state != ProductListState.PermissionDenied;
    }

    public async Task LoadDataAsync()
    {
        SetState(ProductListState.Loading);

        try
        {
            var filter = new ProductFilterDto(_searchTerm, _filterCategoryId, _filterType, _filterStatus, _currentPage, _pageSize);
            var result = await _productService.GetProductsAsync(filter);

            _productsGrid.Rows.Clear();
            foreach (var p in result.Items)
            {
                _productsGrid.Rows.Add(p.ArabicName ?? "", p.Barcode ?? "â€”", p.CategoryName ?? "â€”",
                    p.ProductType, p.SellingPrice, p.CurrentStock, p.Status, "ØªØ¹Ø¯ÙŠÙ„ / Ø­Ø°Ù");
                _productsGrid.Rows[_productsGrid.Rows.Count - 1].Tag = p;
            }

            _totalCount = result.TotalCount;
            _totalCountLabel.Text = $"Ø¥Ø¬Ù…Ø§Ù„ÙŠ: {_totalCount}";
            UpdatePagination();

            SetState(result.Items.Count > 0 ? ProductListState.Loaded : ProductListState.Empty);
        }
        catch (UnauthorizedAccessException)
        {
            SetState(ProductListState.PermissionDenied);
        }
        catch
        {
            SetState(ProductListState.Error);
        }
    }



    private async Task SearchAsync()
    {
        _searchTerm = _searchTextBox.Text.Trim();
        _filterCategoryId = _categoryFilterCombo.SelectedIndex > 0 ? Guid.NewGuid() : null;
        _filterType = _typeFilterCombo.SelectedIndex > 0 ? _typeFilterCombo.SelectedItem?.ToString() : null;
        _filterStatus = _statusFilterCombo.SelectedIndex > 0 ? _statusFilterCombo.SelectedItem?.ToString() : null;
        _currentPage = 1;
        await LoadDataAsync();
    }

    private void UpdatePagination()
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalCount / _pageSize));
        _pageInfoLabel.Text = $"ØµÙØ­Ø© {_currentPage} Ù…Ù† {totalPages}";
        _prevPageButton.Enabled = _currentPage > 1;
        _nextPageButton.Enabled = _currentPage < totalPages;
    }

    private void ProductsGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_productsGrid.Columns[e.ColumnIndex].Name != "Actions") return;

        var row = _productsGrid.Rows[e.RowIndex];
        var product = row.Tag as ProductDto;
        if (product == null) return;

        var cellRect = _productsGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };

        var editItem = new ToolStripMenuItem("âœï¸ ØªØ¹Ø¯ÙŠÙ„");
        editItem.Click += (s, e) => EditProductRequested?.Invoke(this, product);
        menu.Items.Add(editItem);

        var recipeItem = new ToolStripMenuItem("ðŸ“‹ Ø§Ù„ÙˆØµÙØ©");
        recipeItem.Click += (s, e) => ShowRecipeDialog(product);
        menu.Items.Add(recipeItem);

        menu.Items.Add(new ToolStripSeparator());

        var deleteItem = new ToolStripMenuItem("ðŸ—‘ï¸ Ø­Ø°Ù");
        deleteItem.Click += (s, e) => ConfirmDelete(product);
        menu.Items.Add(deleteItem);

        menu.Show(_productsGrid, cellRect.Left, cellRect.Bottom);
    }

    private void ProductsGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;

        if (_productsGrid.Columns[e.ColumnIndex].Name == "Stock")
        {
            if (e.Value is decimal stock && stock < 5)
            {
                e.CellStyle.ForeColor = DesignTokens.ErrorColor;
                e.CellStyle.Font = new Font(DesignTokens.DataFont, FontStyle.Bold);
            }
        }

        if (_productsGrid.Columns[e.ColumnIndex].Name == "Status")
        {
            if (e.Value?.ToString() == "Ù†Ø´Ø·" || e.Value?.ToString() == "Active")
                e.CellStyle.ForeColor = DesignTokens.SuccessColor;
            else
                e.CellStyle.ForeColor = DesignTokens.ErrorColor;
        }
    }

    private void ShowRecipeDialog(ProductDto product)
    {
        // Resolve IRecipeService from the app-level service provider
        var recipeService = AppServiceProvider.Provider?.GetService(typeof(IRecipeService)) as IRecipeService;
        if (recipeService == null)
        {
            RtlMessageBox.Show("Ø®Ø¯Ù…Ø© Ø§Ù„ÙˆØµÙØ§Øª ØºÙŠØ± Ù…ØªÙˆÙØ±Ø©", "Ø®Ø·Ø£",
                MessageBoxButtons.OK, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
            return;
        }

        // Open ProductForm in read-only recipe view with the product service
        var form = new ProductForm(_productService, null, recipeService, product);
        form.ShowDialog(this.FindForm());
    }

    private void ConfirmDelete(ProductDto product)
    {
        var result = RtlMessageBox.Show(
            $"Ù‡Ù„ Ø£Ù†Øª Ù…ØªØ£ÙƒØ¯ Ù…Ù† Ø­Ø°Ù Ø§Ù„Ù…Ù†ØªØ¬ '{product.ArabicName}'ØŸ",
            "ØªØ£ÙƒÙŠØ¯ Ø§Ù„Ø­Ø°Ù",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2,
            MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);

        if (result == DialogResult.Yes)
            DeleteProductRequested?.Invoke(this, product.Id);
    }

    public async Task LoadCategoriesAsync()
    {
        try
        {
            var cats = await _productService.GetCategoriesAsync();
            _categoryFilterCombo.Items.Clear();
            _categoryFilterCombo.Items.Add("Ø¬Ù…ÙŠØ¹ Ø§Ù„ÙØ¦Ø§Øª");
            foreach (var cat in cats) _categoryFilterCombo.Items.Add(cat.Name);
            _categoryFilterCombo.SelectedIndex = 0;
        }
        catch { /* silent */ }
    }
}