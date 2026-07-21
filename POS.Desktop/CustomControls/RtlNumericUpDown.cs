namespace POS.Desktop.CustomControls;
using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;

/// <summary>
/// RTL-aware NumericUpDown with label, JOD 3-decimal formatting,
/// required indicator, ErrorProvider, keyboard shortcuts, and Arabic validation.
/// </summary>
public class RtlNumericUpDown : UserControl
{
    #region Private Fields

    private Label _labelControl;
    private Label _requiredIndicator;
    private Panel _borderPanel;
    private NumericUpDown _numericUpDown;
    private readonly ErrorProvider _errorProvider;
    private readonly ToolTip _toolTip;
    private string _labelText;
    private bool _isRequired;
    private Color _borderColor;
    private bool _hasError;

    #endregion

    #region Public Properties

    public string? InputId { get; set; }
    public string? ErrorMessage { get; set; }

    public string? PlaceholderText
    {
        get => _labelText;
        set { } // NumericUpDown has no placeholder; kept for contract compliance
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
        get => _numericUpDown.Enabled;
        set
        {
            _numericUpDown.Enabled = value;
            _numericUpDown.BackColor = value ? Color.White : DesignTokens.Colors.Background;
            _numericUpDown.ForeColor = value ? DesignTokens.Colors.TextPrimary : DesignTokens.Colors.Disabled;
            _labelControl.Enabled = value;
        }
    }

    public decimal Value
    {
        get => _numericUpDown.Value;
        set => _numericUpDown.Value = value;
    }

    public decimal Minimum
    {
        get => _numericUpDown.Minimum;
        set => _numericUpDown.Minimum = value;
    }

    public decimal Maximum
    {
        get => _numericUpDown.Maximum;
        set => _numericUpDown.Maximum = value;
    }

    public int DecimalPlacesJOD
    {
        get => _numericUpDown.DecimalPlaces;
        set => _numericUpDown.DecimalPlaces = value;
    }

    public int DecimalPlaces
    {
        get => _numericUpDown.DecimalPlaces;
        set => _numericUpDown.DecimalPlaces = value;
    }

    public decimal Increment
    {
        get => _numericUpDown.Increment;
        set => _numericUpDown.Increment = value;
    }

    public bool ThousandsSeparator
    {
        get => _numericUpDown.ThousandsSeparator;
        set => _numericUpDown.ThousandsSeparator = value;
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
        get => _toolTip.GetToolTip(_numericUpDown);
        set
        {
            if (value != null)
                _toolTip.SetToolTip(_numericUpDown, value);
        }
    }

    public bool ReadOnly
    {
        get => _numericUpDown.ReadOnly;
        set => _numericUpDown.ReadOnly = value;
    }

    #endregion

    #region Events

    public event EventHandler? ValueChanged
    {
        add => _numericUpDown.ValueChanged += value;
        remove => _numericUpDown.ValueChanged -= value;
    }

    public new event EventHandler? Enter
    {
        add => _numericUpDown.Enter += value;
        remove => _numericUpDown.Enter -= value;
    }

    public new event EventHandler? Leave
    {
        add => _numericUpDown.Leave += value;
        remove => _numericUpDown.Leave -= value;
    }

    public new event KeyEventHandler? KeyDown
    {
        add => _numericUpDown.KeyDown += value;
        remove => _numericUpDown.KeyDown -= value;
    }

    #endregion

    #region Constructor

    public RtlNumericUpDown()
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
        _labelText = string.Empty;

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

        // Border panel
        _borderPanel = new Panel
        {
            BackColor = DesignTokens.Colors.Border,
            Height = DesignTokens.ControlHeight.Standard + 2
        };

        // NumericUpDown
        _numericUpDown = new NumericUpDown
        {
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.Input,
            Height = DesignTokens.ControlHeight.Standard,
            BackColor = Color.White,
            ForeColor = DesignTokens.Colors.TextPrimary,
            DecimalPlaces = 3,
            Minimum = 0,
            Maximum = 999999999,
            Increment = 0.100m,
            ThousandsSeparator = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            TextAlign = HorizontalAlignment.Left
        };

        _numericUpDown.Enter += OnEnter;
        _numericUpDown.Leave += OnLeave;
        _numericUpDown.KeyDown += OnKeyDown;
        _numericUpDown.Validating += OnValidating;

        // Assemble
        _borderPanel.Controls.Add(_numericUpDown);

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

    #region Keyboard Shortcuts

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Add:
            case Keys.Oemplus:
                _numericUpDown.Value = Math.Min(_numericUpDown.Value + _numericUpDown.Increment, _numericUpDown.Maximum);
                e.Handled = true;
                break;

            case Keys.Subtract:
            case Keys.OemMinus:
                _numericUpDown.Value = Math.Max(_numericUpDown.Value - _numericUpDown.Increment, _numericUpDown.Minimum);
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Focus & Validation

    private void OnEnter(object? sender, EventArgs e)
    {
        if (!_hasError)
            _borderPanel.BackColor = DesignTokens.Colors.Primary;
    }

    private void OnLeave(object? sender, EventArgs e)
    {
        if (!_hasError)
            _borderPanel.BackColor = _borderColor;
    }

    private void OnValidating(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        ValidateRange();
    }

    private bool ValidateRange()
    {
        if (_numericUpDown.Value < _numericUpDown.Minimum)
        {
            SetError($"القيمة يجب أن تكون {DesignTokens.FormatJOD(_numericUpDown.Minimum)} على الأقل");
            return false;
        }

        if (_numericUpDown.Value > _numericUpDown.Maximum)
        {
            SetError($"القيمة يجب أن تكون {DesignTokens.FormatJOD(_numericUpDown.Maximum)} كحد أقصى");
            return false;
        }

        ClearError();
        return true;
    }

    #endregion

    #region Error Provider

    public void SetError(string message)
    {
        _hasError = true;
        _errorProvider.SetError(_numericUpDown, message);
        _borderPanel.BackColor = DesignTokens.Colors.Error;
    }

    public void ClearError()
    {
        _hasError = false;
        _errorProvider.Clear();
        _borderPanel.BackColor = _borderColor;
    }

    /// <summary>
    /// Validates the current value and returns true if valid.
    /// Sets Arabic error messages on failure.
    /// </summary>
    public new bool Validate()
    {
        if (_isRequired && _numericUpDown.Value == 0 && string.IsNullOrEmpty(_numericUpDown.Text))
        {
            SetError("هذا الحقل مطلوب");
            return false;
        }

        return ValidateRange();
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