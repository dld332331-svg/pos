namespace POS.Desktop.CustomControls;
using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;

/// <summary>
/// RTL-aware ComboBox with label, placeholder, required indicator,
/// ErrorProvider integration, and DesignTokens styling.
/// </summary>
public class RtlComboBox : UserControl
{
    #region Private Fields

    private Panel _borderPanel;
    private Label _labelControl;
    private ComboBox _comboBox = null!;
    private Label _placeholderLabel;
    private Label _requiredIndicator;
    private readonly ErrorProvider _errorProvider;
    private readonly ToolTip _toolTip;
    private string _labelText;
    private string _placeholderText;
    private bool _isRequired;
    private Color _borderColor;
    private bool _hasError;

    #endregion

    #region Public Properties

    public string? InputId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ValidationRegex { get; set; }

    public enum InputAlignment { Right, Left, Center }
    private InputAlignment _inputAlignment = InputAlignment.Right;
    public InputAlignment Alignment
    {
        get => _inputAlignment;
        set
        {
            _inputAlignment = value;
        }
    }

    public string? LabelText
    {
        get => _labelText;
        set
        {
            _labelText = value ?? string.Empty;
            _labelControl.Visible = !string.IsNullOrEmpty(_labelText);
            _requiredIndicator.Visible = _isRequired && _labelControl.Visible;
            _labelControl.Text = _labelText;
            UpdateLayout();
        }
    }

    public string? PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value ?? string.Empty;
            UpdatePlaceholderVisibility();
        }
    }

    public bool IsRequired
    {
        get => _isRequired;
        set
        {
            _isRequired = value;
            _requiredIndicator.Visible = _isRequired && _labelControl.Visible;
        }
    }

    public new bool Enabled
    {
        get => _comboBox.Enabled;
        set
        {
            _comboBox.Enabled = value;
            _comboBox.BackColor = value ? Color.White : DesignTokens.Colors.Background;
            _comboBox.ForeColor = value ? DesignTokens.Colors.TextPrimary : DesignTokens.Colors.Disabled;
            _labelControl.Enabled = value;
            _placeholderLabel.Enabled = value;
        }
    }

    public object? SelectedValue
    {
        get => _comboBox.SelectedValue;
        set => _comboBox.SelectedValue = value!;
    }

    public object? SelectedItem
    {
        get => _comboBox.SelectedItem;
        set => _comboBox.SelectedItem = value;
    }

    public int SelectedIndex
    {
        get => _comboBox.SelectedIndex;
        set => _comboBox.SelectedIndex = value;
    }

    public new string Text
    {
        get => _comboBox.Text;
        set => _comboBox.Text = value;
    }

    public object? DataSource
    {
        get => _comboBox.DataSource;
        set => _comboBox.DataSource = value;
    }

    public string? DisplayMember
    {
        get => _comboBox.DisplayMember;
        set => _comboBox.DisplayMember = value;
    }

    public string? ValueMember
    {
        get => _comboBox.ValueMember;
        set => _comboBox.ValueMember = value;
    }

    public ComboBox.ObjectCollection Items => _comboBox!.Items!;

    public int ItemHeight
    {
        get => _comboBox.ItemHeight;
        set => _comboBox.ItemHeight = value;
    }

    public ComboBoxStyle DropDownStyle
    {
        get => _comboBox.DropDownStyle;
        set => _comboBox.DropDownStyle = value;
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            if (!_hasError)
                _borderPanel.BackColor = value;
        }
    }

    public string? ToolTipText
    {
        get => _toolTip.GetToolTip(_comboBox);
        set
        {
            if (value != null)
                _toolTip.SetToolTip(_comboBox, value);
        }
    }

    #endregion

    #region Events

    public event EventHandler? SelectedValueChanged
    {
        add => _comboBox.SelectedValueChanged += value;
        remove => _comboBox.SelectedValueChanged -= value;
    }

    public event EventHandler? SelectedIndexChanged
    {
        add => _comboBox.SelectedIndexChanged += value;
        remove => _comboBox.SelectedIndexChanged -= value;
    }

    public event EventHandler? DropDown
    {
        add => _comboBox.DropDown += value;
        remove => _comboBox.DropDown -= value;
    }

    #endregion

    #region Constructor

    public RtlComboBox()
    {
        RightToLeft = RightToLeft.Yes;
        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink,
            RightToLeft = true
        };
        _toolTip = new ToolTip { InitialDelay = 500, ReshowDelay = 100 };
        _borderColor = DesignTokens.Colors.Border;
        _hasError = false;

        InitializeComponents();
        UpdateLayout();
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        // Label
        _labelControl = new Label
        {
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextPrimary,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = true,
            Visible = false
        };

        // Required asterisk
        _requiredIndicator = new Label
        {
            Text = " *",
            Font = DesignTokens.Typography.BodyBold,
            ForeColor = DesignTokens.Colors.Error,
            AutoSize = true,
            Visible = false
        };

        // Border panel (mimics flat border)
        _borderPanel = new Panel
        {
            BackColor = DesignTokens.Colors.Border,
            Height = DesignTokens.ControlHeight.Standard + 2
        };

        // Inner combo box
        _comboBox = new ComboBox
        {
            RightToLeft = RightToLeft.Yes,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = DesignTokens.Typography.Input,
            BackColor = Color.White,
            ForeColor = DesignTokens.Colors.TextPrimary,
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        _comboBox.SelectedValueChanged += OnSelectedValueChanged;
        _comboBox.Enter += OnComboBoxEnter;
        _comboBox.Leave += OnComboBoxLeave;

        // Placeholder label overlay
        _placeholderLabel = new Label
        {
            Font = DesignTokens.Typography.Input,
            ForeColor = DesignTokens.Colors.TextSecondary,
            BackColor = Color.White,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight,
            Visible = false,
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 0, 3, 0),
            Enabled = false
        };

        // Assemble
        _borderPanel.Controls.Add(_comboBox);
        _borderPanel.Controls.Add(_placeholderLabel);

        Controls.Add(_borderPanel);
        Controls.Add(_requiredIndicator);
        Controls.Add(_labelControl);

        Height = DesignTokens.ControlHeight.Standard + 2;
    }

    #endregion

    #region Layout

    private void UpdateLayout()
    {
        var spacing = DesignTokens.Spacing.Small;

        if (_labelControl.Visible)
        {
            _labelControl.Location = new Point(0, 0);
            _labelControl.Width = Width - 20;
            _requiredIndicator.Location = new Point(Width - 18, 0);
            _borderPanel.Location = new Point(0, _labelControl.PreferredHeight + spacing);
            _borderPanel.Width = Width;
            Height = _labelControl.PreferredHeight + spacing + _borderPanel.Height;
        }
        else
        {
            _borderPanel.Location = new Point(0, 0);
            _borderPanel.Width = Width;
            Height = _borderPanel.Height;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateLayout();
    }

    #endregion

    #region Placeholder

    private void UpdatePlaceholderVisibility()
    {
        bool showPlaceholder = _comboBox.SelectedIndex < 0
            && !string.IsNullOrEmpty(_placeholderText)
            && !_comboBox.DroppedDown;

        _placeholderLabel.Text = _placeholderText;
        _placeholderLabel.Visible = showPlaceholder;
        _placeholderLabel.BringToFront();
    }

    private void OnSelectedValueChanged(object? sender, EventArgs e)
    {
        UpdatePlaceholderVisibility();
    }

    private void OnComboBoxEnter(object? sender, EventArgs e)
    {
        _borderPanel.BackColor = DesignTokens.Colors.Primary;
        _placeholderLabel.Visible = false;
    }

    private void OnComboBoxLeave(object? sender, EventArgs e)
    {
        if (!_hasError)
            _borderPanel.BackColor = _borderColor;
        UpdatePlaceholderVisibility();
    }

    #endregion

    #region Error Provider

    public void SetError(string message)
    {
        _hasError = true;
        _errorProvider.SetError(_comboBox, message);
        _borderPanel.BackColor = DesignTokens.Colors.Error;
    }

    public void ClearError()
    {
        _hasError = false;
        _errorProvider.Clear();
        _borderPanel.BackColor = _borderColor;
    }

    #endregion

    #region Public Methods

    public void BeginUpdate()
    {
        _comboBox.BeginUpdate();
    }

    public void EndUpdate()
    {
        _comboBox.EndUpdate();
        UpdatePlaceholderVisibility();
    }

    public void ClearSelection()
    {
        _comboBox.SelectedIndex = -1;
        UpdatePlaceholderVisibility();
    }

    #endregion

    #region Cleanup

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _errorProvider?.Dispose();
            _toolTip?.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}