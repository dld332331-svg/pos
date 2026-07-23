using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.CustomControls;

using POS.Desktop.Themes;
namespace POS.Desktop.Forms;

/// <summary>
/// PROD-002: Product add/edit dialog with RTL layout.
/// Fields: Arabic Name (required), English Name, SKU, Barcode, Category (combo), Product Type (combo),
/// Unit, Cost, Selling Price, Tax Rate, Min Stock, Supplier (combo - loaded from ISupplierService),
/// Recipe (button - opens recipe editor via IRecipeService), Image (button), Active toggle.
/// Validation on save.
/// </summary>
public class ProductForm : Form
{
    private readonly IProductService _productService;
    private readonly ISupplierService? _supplierService;
    private readonly IRecipeService? _recipeService;
    private readonly ProductDto? _existingProduct;
    private readonly List<CategoryDto> _categories = new();
    private readonly List<SupplierDto> _suppliers = new();

    private enum ProductFormState { Loading, Loaded, Error, PermissionDenied }
    private ProductFormState _currentState = ProductFormState.Loading;

    // UI Controls
    private Panel _mainPanel;
    private Label _titleLabel;
    private ErrorProvider _errorProvider;

    // Field controls
    private Label _arabicNameLabel;
    private TextBox _arabicNameTextBox;
    private Label _englishNameLabel;
    private TextBox _englishNameTextBox;
    private Label _skuLabel;
    private TextBox _skuTextBox;
    private Label _barcodeLabel;
    private TextBox _barcodeTextBox;
    private Label _categoryLabel;
    private ComboBox _categoryComboBox;
    private Label _typeLabel;
    private ComboBox _typeComboBox;
    private Label _unitLabel;
    private ComboBox _unitComboBox;
    private List<UnitOfMeasureDto> _unitsOfMeasure = new();
    private Label _costLabel;
    private NumericUpDown _costNumeric;
    private Label _sellingPriceLabel;
    private NumericUpDown _sellingPriceNumeric;
    private Label _taxRateLabel;
    private NumericUpDown _taxRateNumeric;
    private Label _minStockLabel;
    private NumericUpDown _minStockNumeric;
    private Label _supplierLabel;
    private ComboBox _supplierComboBox;
    private Button _recipeButton;
    private Label _imageLabel;
    private Button _imageButton;
    private CheckBox _activeCheckBox;
    private CheckBox _modifiersCheckBox;

    // Actions
    private Button _saveButton;
    private Button _cancelButton;
    private Label _imagePreviewLabel;
    private Panel _loadingPanel;
    private Panel _errorPanel;
    private Panel _permissionPanel;
    private Label _errorLabel;

    // Events
    public event EventHandler<ProductDto>? ProductSaved;

    public ProductForm(IProductService productService) : this(productService, null, null) { }

    public ProductForm(IProductService productService, ISupplierService? supplierService, IRecipeService? recipeService)
    {
        _productService = productService;
        _supplierService = supplierService;
        _recipeService = recipeService;
        InitializeComponent();
        SetState(ProductFormState.Loading);
        _ = LoadLookupsAsync();
    }

    public ProductForm(IProductService productService, ISupplierService? supplierService,
        IRecipeService? recipeService, ProductDto product) : this(productService, supplierService, recipeService)
    {
        _existingProduct = product;
        Text = $"تعديل المنتج: {product.ArabicName}";
        PopulateFields(product);
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Text = "إضافة منتج جديد";
        ClientSize = new Size(520, 720);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = DesignTokens.BackgroundColor;
        Font = DesignTokens.DefaultFont;
        AutoScroll = true;

        _errorProvider = new ErrorProvider { RightToLeft = true };

        // Main panel
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingLG)
        };

        // Title
        _titleLabel = new Label
        {
            Text = _existingProduct != null ? "تعديل المنتج" : "إضافة منتج جديد",
            Font = DesignTokens.HeadingFont,
            ForeColor = DesignTokens.PrimaryColor,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Create all field controls
        int y = 10;
        int labelX = 300;
        int fieldX = 10;
        int fieldWidth = 280;
        int rowHeight = 48;

        _arabicNameLabel = CreateFieldLabel("الاسم بالعربية *", labelX, y);
        _arabicNameTextBox = CreateFieldTextBox(fieldX, y + 22, fieldWidth);
        y += rowHeight;

        _englishNameLabel = CreateFieldLabel("الاسم بالإنجليزية", labelX, y);
        _englishNameTextBox = CreateFieldTextBox(fieldX, y + 22, fieldWidth);
        y += rowHeight;

        _skuLabel = CreateFieldLabel("رمز المنتج (SKU)", labelX, y);
        _skuTextBox = CreateFieldTextBox(fieldX, y + 22, fieldWidth);
        y += rowHeight;

        _barcodeLabel = CreateFieldLabel("الباركود", labelX, y);
        _barcodeTextBox = CreateFieldTextBox(fieldX, y + 22, fieldWidth);
        y += rowHeight;

        _categoryLabel = CreateFieldLabel("الفئة", labelX, y);
        _categoryComboBox = new ComboBox
        {
            Location = new Point(fieldX, y + 22),
            Size = new Size(fieldWidth, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes
        };
        y += rowHeight;

        _typeLabel = CreateFieldLabel("نوع المنتج", labelX, y);
        _typeComboBox = new ComboBox
        {
            Location = new Point(fieldX, y + 22),
            Size = new Size(fieldWidth, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes
        };
        _typeComboBox.Items.AddRange(new object[] { "بسيط", "متغير", "مركب" });
        _typeComboBox.SelectedIndex = 0;
        y += rowHeight;

        _unitLabel = CreateFieldLabel("الوحدة", labelX, y);
        _unitComboBox = new ComboBox
        {
            Location = new Point(fieldX, y + 22),
            Size = new Size(fieldWidth, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes
        };
        y += rowHeight;

        _costLabel = CreateFieldLabel("التكلفة", labelX, y);
        _costNumeric = CreateDecimalNumeric(fieldX, y + 22, fieldWidth);
        y += rowHeight;

        _sellingPriceLabel = CreateFieldLabel("سعر البيع *", labelX, y);
        _sellingPriceNumeric = CreateDecimalNumeric(fieldX, y + 22, fieldWidth);
        y += rowHeight;

        _taxRateLabel = CreateFieldLabel("نسبة الضريبة %", labelX, y);
        _taxRateNumeric = CreateDecimalNumeric(fieldX, y + 22, fieldWidth, 0, 100, 15m);
        y += rowHeight;

        _minStockLabel = CreateFieldLabel("الحد الأدنى للمخزون", labelX, y);
        _minStockNumeric = CreateDecimalNumeric(fieldX, y + 22, fieldWidth, 0, 99999, 5m);
        y += rowHeight;

        _supplierLabel = CreateFieldLabel("المورد", labelX, y);
        _supplierComboBox = new ComboBox
        {
            Location = new Point(fieldX, y + 22),
            Size = new Size(fieldWidth, 26),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes
        };
        y += rowHeight;

        // Recipe button (only shown when editing an existing product)
        _recipeButton = new Button
        {
            Text = "📋 وصفة التصنيع",
            Font = DesignTokens.DefaultFont,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(fieldX, y + 22),
            Size = new Size(fieldWidth, 28),
            BackColor = DesignTokens.InfoColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Visible = _existingProduct != null && _recipeService != null
        };
        _recipeButton.Click += RecipeButton_Click;
        y += rowHeight;

        _imageLabel = CreateFieldLabel("صورة المنتج", labelX, y);
        _imageButton = new Button
        {
            Text = "📁 اختر صورة",
            Font = DesignTokens.DefaultFont,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(fieldX, y + 22),
            Size = new Size(fieldWidth - 60, 28),
            BackColor = DesignTokens.CardColor,
            Cursor = Cursors.Hand
        };
        _imageButton.Click += ImageButton_Click;

        _imagePreviewLabel = new Label
        {
            Text = "لا توجد صورة",
            Font = DesignTokens.SmallFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            Location = new Point(fieldX + fieldWidth - 55, y + 25),
            Size = new Size(55, 25),
            TextAlign = ContentAlignment.MiddleCenter
        };
        y += rowHeight;

        _activeCheckBox = new CheckBox
        {
            Text = "المنتج نشط",
            Font = DesignTokens.DefaultFont,
            Location = new Point(fieldX, y),
            Size = new Size(fieldWidth, 26),
            Checked = true,
            RightToLeft = RightToLeft.Yes
        };

        _modifiersCheckBox = new CheckBox
        {
            Text = "يسمح بالإضافات",
            Font = DesignTokens.DefaultFont,
            Location = new Point(fieldX, y + 28),
            Size = new Size(fieldWidth, 26),
            RightToLeft = RightToLeft.Yes
        };

        // Actions panel
        var actionsPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingMD)
        };

        _cancelButton = new Button
        {
            Text = "إلغاء",
            Font = DesignTokens.ButtonFont,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 40),
            Dock = DockStyle.Left,
            Cursor = Cursors.Hand,
            BackColor = DesignTokens.BorderColor,
            ForeColor = DesignTokens.TextPrimaryColor
        };

        _saveButton = new Button
        {
            Text = "حفظ المنتج",
            Font = DesignTokens.ButtonFont,
            ForeColor = Color.White,
            BackColor = DesignTokens.SuccessColor,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(180, 40),
            Dock = DockStyle.Right,
            Cursor = Cursors.Hand
        };

        actionsPanel.Controls.Add(_saveButton);
        actionsPanel.Controls.Add(_cancelButton);

        // Add all controls to main panel
        var fieldsPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(0, 0, 0, DesignTokens.SpacingSM) };
        var fieldControls = new Control[]
        {
            _arabicNameLabel, _arabicNameTextBox,
            _englishNameLabel, _englishNameTextBox,
            _skuLabel, _skuTextBox,
            _barcodeLabel, _barcodeTextBox,
            _categoryLabel, _categoryComboBox,
            _typeLabel, _typeComboBox,
            _unitLabel, _unitComboBox,
            _costLabel, _costNumeric,
            _sellingPriceLabel, _sellingPriceNumeric,
            _taxRateLabel, _taxRateNumeric,
            _minStockLabel, _minStockNumeric,
            _supplierLabel, _supplierComboBox,
            _recipeButton,
            _imageLabel, _imageButton, _imagePreviewLabel,
            _activeCheckBox, _modifiersCheckBox
        };
        fieldsPanel.Controls.AddRange(fieldControls);

        _mainPanel.Controls.Add(fieldsPanel);
        _mainPanel.Controls.Add(_titleLabel);
        Controls.Add(_mainPanel);
        Controls.Add(actionsPanel);

        // Overlay panels
        _loadingPanel = ThemeManager.CreateLoadingPanel("جاري تحميل بيانات المنتج...");
        _loadingPanel.Visible = false;

        _errorPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor, Visible = false };
        _errorLabel = new Label
        {
            Text = "حدث خطأ أثناء تحميل البيانات",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.ErrorColor,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var retryButton = new Button
        {
            Text = "إعادة المحاولة",
            Font = DesignTokens.ButtonFont,
            BackColor = DesignTokens.PrimaryColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 40),
            Cursor = Cursors.Hand
        };
        retryButton.Anchor = AnchorStyles.None;
        retryButton.Click += async (s, e) => await LoadLookupsAsync();
        _errorPanel.Controls.Add(retryButton);
        _errorPanel.Controls.Add(_errorLabel);

        _permissionPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor, Visible = false };
        _permissionPanel.Controls.Add(new Label
        {
            Text = "ليس لديك صلاحية لإدارة المنتجات",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        });

        Controls.Add(_loadingPanel);
        Controls.Add(_errorPanel);
        Controls.Add(_permissionPanel);

        // Events
        _saveButton.Click += async (s, e) => await SaveAsync();
        _cancelButton.Click += (s, e) => Close();
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };
    }

    private Label CreateFieldLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(x, y),
            Size = new Size(190, 20),
            TextAlign = ContentAlignment.MiddleRight
        };
    }

    private TextBox CreateFieldTextBox(int x, int y, int width)
    {
        return new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 26),
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private NumericUpDown CreateDecimalNumeric(int x, int y, int width, decimal min = 0, decimal max = 999999, decimal val = 0)
    {
        return new NumericUpDown
        {
            Location = new Point(x, y),
            Size = new Size(width, 26),
            Font = DesignTokens.DefaultFont,
            DecimalPlaces = 3,
            Minimum = min,
            Maximum = max,
            Value = val,
            ThousandsSeparator = true,
            RightToLeft = RightToLeft.Yes,
            TextAlign = HorizontalAlignment.Left
        };
    }

    private void ImageButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "اختر صورة المنتج",
            Filter = "صور|*.jpg;*.jpeg;*.png;*.bmp"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _imagePreviewLabel.Text = "✅";
            _imagePreviewLabel.ForeColor = DesignTokens.SuccessColor;
        }
    }

    private async void RecipeButton_Click(object? sender, EventArgs e)
    {
        if (_existingProduct == null || _recipeService == null) return;

        try
        {
            var recipe = await _recipeService.GetRecipeByProductAsync(_existingProduct.Id);

            // Show recipe details in a simple dialog
            var dialog = new RtlDialog($"وصفة: {_existingProduct.ArabicName}", 500, 400);

            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = DesignTokens.SurfaceColor,
                Padding = new Padding(DesignTokens.SpacingMD)
            };

            var infoLabel = new Label
            {
                Text = recipe != null
                    ? $"الوصفة موجودة - {recipe.Ingredients.Count} مكونات"
                    : "لا توجد وصفة لهذا المنتج بعد",
                Font = DesignTokens.DefaultFont,
                ForeColor = DesignTokens.TextSecondaryColor,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter
            };
            panel.Controls.Add(infoLabel);

            if (recipe != null)
            {
                var costLabel = new Label
                {
                    Text = $"التكلفة الإجمالية: {recipe.TotalCost:N3} JOD",
                    Font = DesignTokens.HeadingFont,
                    ForeColor = DesignTokens.PrimaryColor,
                    Dock = DockStyle.Bottom,
                    Height = 30,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                panel.Controls.Add(costLabel);

                var ingredientsList = new ListBox
                {
                    Dock = DockStyle.Fill,
                    Font = DesignTokens.DefaultFont,
                    RightToLeft = RightToLeft.Yes
                };
                foreach (var ing in recipe.Ingredients)
                {
                    ingredientsList.Items.Add($"{ing.ItemName} - الكمية: {ing.Quantity} {ing.Unit}");
                }
                panel.Controls.Add(ingredientsList);
            }
            else
            {
                var noRecipeLabel = new Label
                {
                    Text = "يمكنك إنشاء وصفة تصنيع لهذا المنتج من خلال إدارة الوصفات",
                    Font = DesignTokens.Typography.Secondary,
                    ForeColor = DesignTokens.TextSecondaryColor,
                    Dock = DockStyle.Top,
                    Height = 30,
                    TextAlign = ContentAlignment.MiddleCenter
                };
                panel.Controls.Add(noRecipeLabel);
            }

            dialog.ContentArea.Controls.Add(panel);
            dialog.AddAction("إغلاق", (s, e) => { dialog.DialogResult = DialogResult.OK; dialog.Close(); });
            dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[ProductForm] RecipeButton_Click failed: {ex}");
            RtlMessageBox.Show("حدث خطأ أثناء تحميل الوصفة", "خطأ",
                MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
    }

    private void PopulateFields(ProductDto product)
    {
        _arabicNameTextBox.Text = product.ArabicName;
        _englishNameTextBox.Text = product.EnglishName ?? "";
        _skuTextBox.Text = product.Sku ?? "";
        _barcodeTextBox.Text = product.Barcode ?? "";
        _typeComboBox.SelectedItem = product.ProductType switch
        {
            "Simple" => "بسيط",
            "Variable" => "متغير",
            "Composite" => "مركب",
            _ => "بسيط"
        };
        if (product.Unit != null)
        {
            for (int i = 0; i < _unitComboBox.Items.Count; i++)
            {
                var itemText = _unitComboBox.Items[i]?.ToString() ?? "";
                var uomName = _unitsOfMeasure.Count > i ? _unitsOfMeasure[i].ArabicName : null;
                if (itemText == product.Unit || uomName == product.Unit)
                {
                    _unitComboBox.SelectedIndex = i;
                    break;
                }
            }
        }
        if (_unitComboBox.SelectedIndex < 0)
            _unitComboBox.SelectedIndex = 0;
        _costNumeric.Value = product.Cost;
        _sellingPriceNumeric.Value = product.SellingPrice;
        _taxRateNumeric.Value = product.TaxRate;
        _minStockNumeric.Value = product.MinStock;
        _activeCheckBox.Checked = product.Status == "Active";
        _modifiersCheckBox.Checked = product.AllowModifiers;
    }

    private bool ValidateForm()
    {
        _errorProvider.Clear();
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(_arabicNameTextBox.Text))
        {
            _errorProvider.SetError(_arabicNameTextBox, "الاسم بالعربية مطلوب");
            isValid = false;
        }

        if (_sellingPriceNumeric.Value <= 0)
        {
            _errorProvider.SetError(_sellingPriceNumeric, "سعر البيع يجب أن يكون أكبر من صفر");
            isValid = false;
        }

        if (_costNumeric.Value < 0)
        {
            _errorProvider.SetError(_costNumeric, "التكلفة لا يمكن أن تكون سالبة");
            isValid = false;
        }

        return isValid;
    }

    private async Task SaveAsync()
    {
        if (!ValidateForm()) return;

        _saveButton.Enabled = false;
        _saveButton.Text = "جاري الحفظ...";

        try
        {
            var categoryIdx = _categoryComboBox.SelectedIndex;
            var categoryId = categoryIdx > 0 && _categories.Count > 0 ? _categories[categoryIdx - 1].Id : (Guid?)null;
            var supplierIdx = _supplierComboBox.SelectedIndex;
            var supplierId = supplierIdx > 0 && _suppliers.Count > 0 ? _suppliers[supplierIdx - 1].Id : (Guid?)null;

            var typeMap = new Dictionary<string, string> { { "بسيط", "Simple" }, { "متغير", "Variable" }, { "مركب", "Composite" } };
            var productType = typeMap.TryGetValue(_typeComboBox.SelectedItem?.ToString() ?? "", out var pt) ? pt : "Simple";

            if (_existingProduct != null)
            {
                var request = new UpdateProductRequest(
                    _existingProduct.Id, _arabicNameTextBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(_englishNameTextBox.Text) ? null : _englishNameTextBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(_skuTextBox.Text) ? null : _skuTextBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(_barcodeTextBox.Text) ? null : _barcodeTextBox.Text.Trim(),
                    categoryId, productType, GetSelectedUnit(),
                    _costNumeric.Value, _sellingPriceNumeric.Value, _taxRateNumeric.Value,
                    _minStockNumeric.Value, supplierId, _modifiersCheckBox.Checked,
                    _activeCheckBox.Checked ? "Active" : "Inactive");

                var result = await _productService.UpdateProductAsync(request);
                ProductSaved?.Invoke(this, result);
            }
            else
            {
                var request = new CreateProductRequest(
                    _arabicNameTextBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(_englishNameTextBox.Text) ? null : _englishNameTextBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(_skuTextBox.Text) ? null : _skuTextBox.Text.Trim(),
                    string.IsNullOrWhiteSpace(_barcodeTextBox.Text) ? null : _barcodeTextBox.Text.Trim(),
                    categoryId, productType, GetSelectedUnit(),
                    _costNumeric.Value, _sellingPriceNumeric.Value, _taxRateNumeric.Value,
                    _minStockNumeric.Value, supplierId, _modifiersCheckBox.Checked);

                var result = await _productService.CreateProductAsync(request);
                ProductSaved?.Invoke(this, result);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError($"[ProductForm] Save failed: {ex}");
            RtlMessageBox.Show("حدث خطأ أثناء حفظ المنتج", "خطأ", MessageBoxButtons.OK, MessageBoxIcon.Error,
                MessageBoxDefaultButton.Button1, MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading);
        }
        finally
        {
            _saveButton.Enabled = true;
            _saveButton.Text = "حفظ المنتج";
        }
    }

    private void SetState(ProductFormState state)
    {
        _currentState = state;
        _loadingPanel.Visible = state == ProductFormState.Loading;
        _errorPanel.Visible = state == ProductFormState.Error;
        _permissionPanel.Visible = state == ProductFormState.PermissionDenied;
        _mainPanel.Visible = state == ProductFormState.Loaded;
    }

    private string GetSelectedUnit()
    {
        if (_unitComboBox.SelectedItem == null || _unitComboBox.SelectedIndex < 0)
            return "piece";
        var selected = _unitComboBox.SelectedItem.ToString() ?? "piece";
        return selected;
    }

    private async Task LoadLookupsAsync()
    {
        SetState(ProductFormState.Loading);

        try
        {
            _categories.Clear();
            var cats = await _productService.GetCategoriesAsync();
            _categories.AddRange(cats);

            _categoryComboBox.Items.Clear();
            _categoryComboBox.Items.Add("— بدون فئة —");
            foreach (var cat in _categories)
                _categoryComboBox.Items.Add(cat.Name);
            _categoryComboBox.SelectedIndex = 0;

            // Load Units of Measure
            _unitComboBox.Items.Clear();
            try
            {
                _unitsOfMeasure = await _productService.GetUnitsOfMeasureAsync();
                foreach (var uom in _unitsOfMeasure)
                    _unitComboBox.Items.Add(uom.ArabicName ?? uom.Name);
            }
            catch
            {
                // Fallback: common units
                _unitComboBox.Items.AddRange(new object[] { "قطعة", "كغ", "غم", "لتر", "مل", "دزينة" });
            }
            if (_unitComboBox.Items.Count > 0)
                _unitComboBox.SelectedIndex = 0;

            // Load real suppliers from service
            _supplierComboBox.Items.Clear();
            _supplierComboBox.Items.Add("— بدون مورد —");

            if (_supplierService != null)
            {
                try
                {
                    var suppliers = await _supplierService.GetSuppliersAsync();
                    _suppliers.Clear();
                    _suppliers.AddRange(suppliers);
                    foreach (var s in suppliers)
                        _supplierComboBox.Items.Add(s.Name);
                }
                catch
                {
                    // Fallback silently if service fails
                }
            }

            _supplierComboBox.SelectedIndex = 0;

            SetState(ProductFormState.Loaded);
        }
        catch
        {
            _errorLabel.Text = "حدث خطأ أثناء تحميل البيانات";
            SetState(ProductFormState.Error);
        }
    }
}
