namespace POS.Desktop.CustomControls;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using POS.Desktop.Themes;

/// <summary>
/// Validation modes for RtlTextBox.
/// </summary>
public enum TextBoxValidationMode
{
    None,
    NumericOnly,
    AlphaOnly,
    Email,
    Barcode
}

/// <summary>
/// Full container-based RTL TextBox with label, placeholder, error icon,
/// MaxLength counter, format validation, password mode, and DesignTokens styling.
/// Modern design: rounded border, focus ring, Font Awesome icons, smooth transitions.
/// </summary>
public class RtlTextBox : UserControl
{
    #region Private Fields

    private Label _labelControl = null!;
    private Label _requiredIndicator = null!;
    private Panel _borderPanel = null!;
    private Panel _innerPanel = null!;
    private TextBox _textBox = null!;
    private PictureBox _errorIcon = null!;
    private Label _charCounterLabel = null!;
    private Label _errorMessageLabel = null!;
    private Button _passwordToggleBtn = null!;
    private Label? _prefixIconLabel;
    private readonly ErrorProvider _errorProvider;
    private readonly ToolTip _toolTip;
    private string _labelText;
    private string _placeholderText;
    private bool _isRequired;
    private bool _hasError;
    private bool _isPasswordMode;
    private bool _isMultiline;
    private Color _borderColor;
    private Color _normalForeColor;
    private bool _autoValidate;
    private bool _showingPlaceholder;
    private bool _suppressTextChanged;
    private int _cornerRadius = DesignTokens.Radius.Md;
    private string? _prefixIcon;

    #endregion

    #region Public Properties

    public string? InputId { get; set; }
    public string? AllowedCharacters { get; set; }
    public string? ValidationRegex { get; set; }
    public string? ErrorMessage { get; set; }

    public enum InputAlignment { Right, Left, Center }
    private InputAlignment _inputAlignment = InputAlignment.Right;
    public InputAlignment Alignment
    {
        get => _inputAlignment;
        set
        {
            _inputAlignment = value;
            _textBox.TextAlign = value switch
            {
                InputAlignment.Left => HorizontalAlignment.Left,
                InputAlignment.Center => HorizontalAlignment.Center,
                _ => HorizontalAlignment.Right
            };
        }
    }

    public enum KeyboardBehaviorType { Default, NumericOnly, AlphaNumeric, AlphaOnly, Email, Barcode }
    private KeyboardBehaviorType _keyboardBehavior = KeyboardBehaviorType.Default;
    public KeyboardBehaviorType KeyboardBehavior
    {
        get => _keyboardBehavior;
        set
        {
            _keyboardBehavior = value;
            if (value != KeyboardBehaviorType.Default)
                ValidationMode = value switch
                {
                    KeyboardBehaviorType.NumericOnly => TextBoxValidationMode.NumericOnly,
                    KeyboardBehaviorType.AlphaOnly => TextBoxValidationMode.AlphaOnly,
                    KeyboardBehaviorType.Email => TextBoxValidationMode.Email,
                    KeyboardBehaviorType.Barcode => TextBoxValidationMode.Barcode,
                    _ => TextBoxValidationMode.None
                };
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
            UpdatePlaceholder();
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

    public new bool AutoValidate
    {
        get => _autoValidate;
        set => _autoValidate = value;
    }

    public TextBoxValidationMode ValidationMode { get; set; } = TextBoxValidationMode.None;

    public new string Text
    {
        get => _showingPlaceholder ? string.Empty : _textBox.Text;
        set
        {
            _showingPlaceholder = false;
            _suppressTextChanged = true;
            _textBox.Text = value ?? string.Empty;
            _textBox.ForeColor = _normalForeColor;
            _suppressTextChanged = false;
            UpdateCharCounter();
        }
    }

    public int MaxLength
    {
        get => _textBox.MaxLength;
        set
        {
            _textBox.MaxLength = value;
            _charCounterLabel.Visible = value > 0 && value < int.MaxValue;
            UpdateCharCounter();
            UpdateLayout();
        }
    }

    public bool Multiline
    {
        get => _isMultiline;
        set
        {
            _isMultiline = value;
            _textBox.Multiline = value;
            _textBox.ScrollBars = value ? ScrollBars.Vertical : ScrollBars.None;
            _textBox.AcceptsReturn = value;
            UpdateLayout();
        }
    }

    public char PasswordChar
    {
        get => _textBox.PasswordChar;
        set
        {
            _textBox.PasswordChar = value;
            _isPasswordMode = value != '\0';
            _passwordToggleBtn.Visible = _isPasswordMode;
            if (_isPasswordMode)
            {
                _passwordToggleBtn.Text = value == '\0' ? FontAwesomeIcons.EyeSlash : FontAwesomeIcons.Eye;
            }
            UpdateLayout();
        }
    }

    public new bool Enabled
    {
        get => _textBox.Enabled;
        set
        {
            _textBox.Enabled = value;
            _textBox.BackColor = value ? Color.White : DesignTokens.Colors.Background;
            _textBox.ForeColor = value ? _normalForeColor : DesignTokens.Colors.DisabledText;
            _borderPanel.BackColor = value ? (_hasError ? DesignTokens.Colors.Error : _borderColor) : DesignTokens.Colors.BorderLight;
            _labelControl.Enabled = value;
            _errorIcon.Visible = false;
            _errorMessageLabel.Visible = false;
            if (_isPasswordMode) _passwordToggleBtn.Enabled = value;
            Invalidate();
        }
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
        get => _toolTip.GetToolTip(_textBox);
        set { if (value != null) _toolTip.SetToolTip(_textBox, value); }
    }

    public HorizontalAlignment TextAlign
    {
        get => _textBox.TextAlign;
        set => _textBox.TextAlign = value;
    }

    public bool ReadOnly
    {
        get => _textBox.ReadOnly;
        set => _textBox.ReadOnly = value;
    }

    public string? PrefixIcon
    {
        get => _prefixIcon;
        set
        {
            _prefixIcon = value;
            if (!string.IsNullOrEmpty(value))
            {
                if (_prefixIconLabel == null)
                {
                    _prefixIconLabel = new Label
                    {
                        Font = Icons.FontLoader.GetFontAwesomeSolid(14f),
                        ForeColor = DesignTokens.Colors.TextHint,
                        TextAlign = ContentAlignment.MiddleCenter,
                        BackColor = Color.Transparent,
                        AutoSize = false,
                        Width = 28,
                        Dock = DockStyle.Right
                    };
                }
                _prefixIconLabel.Text = value;
                if (!_innerPanel.Controls.Contains(_prefixIconLabel))
                    _innerPanel.Controls.Add(_prefixIconLabel);
            }
            else if (_prefixIconLabel != null)
            {
                _innerPanel.Controls.Remove(_prefixIconLabel);
            }
            _textBox.BringToFront();
            UpdateLayout();
        }
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = Math.Max(0, Math.Min(30, value)); UpdateRoundedRegion(); Invalidate(); }
    }

    #endregion

    #region Events

    public new event EventHandler? TextChanged
    {
        add => _textBox.TextChanged += value;
        remove => _textBox.TextChanged -= value;
    }

    public event EventHandler? EnterKeyPressed;

    public new event EventHandler? Enter
    {
        add => _textBox.Enter += value;
        remove => _textBox.Enter -= value;
    }

    public new event EventHandler? Leave
    {
        add => _textBox.Leave += value;
        remove => _textBox.Leave -= value;
    }

    public new event KeyPressEventHandler? KeyPress
    {
        add => _textBox.KeyPress += value;
        remove => _textBox.KeyPress -= value;
    }

    #endregion

    #region Constructor

    public RtlTextBox()
    {
        RightToLeft = RightToLeft.Yes;
        _errorProvider = new ErrorProvider
        {
            BlinkStyle = ErrorBlinkStyle.NeverBlink,
            RightToLeft = true
        };
        _toolTip = new ToolTip { InitialDelay = 500, ReshowDelay = 100 };
        _borderColor = DesignTokens.Colors.Border;
        _normalForeColor = DesignTokens.Colors.TextPrimary;
        _hasError = false;
        _autoValidate = true;
        _isPasswordMode = false;
        _isMultiline = false;
        _labelText = string.Empty;
        _placeholderText = string.Empty;

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

        // Border panel (outer border)
        _borderPanel = new Panel
        {
            BackColor = DesignTokens.Colors.Border,
            Height = DesignTokens.ControlHeight.Standard + 2,
            Padding = new Padding(1)
        };
        _borderPanel.Paint += BorderPanel_Paint;

        // Inner panel (white background with rounded corners)
        _innerPanel = new Panel
        { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(0) };

        // Error icon (Font Awesome circle-exclamation)
        _errorIcon = new PictureBox
        {
            Size = new Size(16, 16),
            Visible = false,
            BackColor = Color.Transparent
        };
        DrawErrorIcon();

        // Text box
        _textBox = new TextBox
        {
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.Typography.Input,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ForeColor = DesignTokens.Colors.TextPrimary,
            TextAlign = HorizontalAlignment.Right,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0)
        };

        _textBox.Enter += OnTextBoxEnter;
        _textBox.Leave += OnTextBoxLeave;
        _textBox.TextChanged += OnTextChanged;
        _textBox.KeyDown += OnTextBoxKeyDown;
        _textBox.KeyPress += OnTextBoxKeyPress;

        // Character counter label
        _charCounterLabel = new Label
        {
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.TextSecondary,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = true,
            Visible = false
        };

        // Error message label
        _errorMessageLabel = new Label
        {
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.Error,
            RightToLeft = RightToLeft.Yes,
            TextAlign = ContentAlignment.MiddleRight,
            AutoSize = true,
            Visible = false
        };

        // Password toggle button
        _passwordToggleBtn = new Button
        {
            Text = FontAwesomeIcons.Eye,
            Font = Icons.FontLoader.GetFontAwesomeSolid(12f),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(32, 0),
            Visible = false,
            Dock = DockStyle.Left,
            BackColor = Color.Transparent,
            ForeColor = DesignTokens.Colors.TextHint,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        _passwordToggleBtn.FlatAppearance.BorderSize = 0;
        _passwordToggleBtn.Click += OnPasswordToggle;

        // Assemble inner panel
        _innerPanel.Controls.Add(_textBox);
        _borderPanel.Controls.Add(_innerPanel);

        Controls.Add(_borderPanel);
        Controls.Add(_errorIcon);
        Controls.Add(_errorMessageLabel);
        Controls.Add(_charCounterLabel);
        Controls.Add(_requiredIndicator);
        Controls.Add(_labelControl);

        Height = DesignTokens.ControlHeight.Standard + 2;
    }

    private void DrawErrorIcon()
    {
        var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var brush = new SolidBrush(DesignTokens.Colors.Error);
            g.FillEllipse(brush, 0, 0, 15, 15);
            using var font = new Font("Arial", 9f, FontStyle.Bold);
            var size = g.MeasureString("!", font);
            g.DrawString("!", font, Brushes.White, (16 - size.Width) / 2, -1);
        }
        _errorIcon.Image?.Dispose();
        _errorIcon.Image = bmp;
    }

    private void BorderPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = DesignTokens.CreateRoundedRect(_borderPanel.ClientRectangle, _cornerRadius);
        _borderPanel.Region = new Region(path);
    }

    private void UpdateRoundedRegion()
    {
        if (_borderPanel == null) return;
        using var outerPath = DesignTokens.CreateRoundedRect(_borderPanel.ClientRectangle, _cornerRadius);
        _borderPanel.Region = new Region(outerPath);

        if (_innerPanel != null)
        {
            var innerRect = new Rectangle(0, 0, _innerPanel.Width, _innerPanel.Height);
            using var innerPath = DesignTokens.CreateRoundedRect(innerRect, Math.Max(0, _cornerRadius - 1));
            _innerPanel.Region = new Region(innerPath);
        }
    }

    #endregion

    #region Layout

    private void UpdateLayout()
    {
        var spacing = DesignTokens.Spacing.Small;
        int textBoxHeight = _isMultiline
            ? DesignTokens.ControlHeight.Standard * 4
            : DesignTokens.ControlHeight.Standard;

        _borderPanel.Height = textBoxHeight + 2;

        if (_labelControl.Visible)
        {
            _labelControl.Location = new Point(0, 0);
            _labelControl.Width = Width - 20;
            _requiredIndicator.Location = new Point(Width - 18, 0);
            _borderPanel.Location = new Point(0, _labelControl.PreferredHeight + spacing);
            _borderPanel.Width = Width;
            Height = _labelControl.PreferredHeight + spacing + _borderPanel.Height
                    + (_errorMessageLabel.Visible ? _errorMessageLabel.PreferredHeight + 2 : 0)
                    + (_charCounterLabel.Visible ? _charCounterLabel.PreferredHeight + 2 : 0);
        }
        else
        {
            _borderPanel.Location = new Point(0, 0);
            _borderPanel.Width = Width;
            Height = _borderPanel.Height
                    + (_errorMessageLabel.Visible ? _errorMessageLabel.PreferredHeight + 2 : 0)
                    + (_charCounterLabel.Visible ? _charCounterLabel.PreferredHeight + 2 : 0);
        }

        // Position error icon and message
        _errorIcon.Location = new Point(Width - 18, _borderPanel.Bottom + 3);
        _errorMessageLabel.Location = new Point(0, _borderPanel.Bottom + 2);
        _errorMessageLabel.Width = Width - 22;

        // Position char counter
        _charCounterLabel.Location = new Point(0, _errorMessageLabel.Visible ? _errorMessageLabel.Bottom + 1 : _borderPanel.Bottom + 2);
        _charCounterLabel.Width = Width - 20;

        UpdateRoundedRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateLayout();
    }

    #endregion

    #region Placeholder

    private void ShowPlaceholder()
    {
        if (string.IsNullOrEmpty(_placeholderText) || _textBox.Focused || _showingPlaceholder)
            return;
        _showingPlaceholder = true;
        _suppressTextChanged = true;
        _textBox.Text = _placeholderText;
        _textBox.ForeColor = DesignTokens.Colors.TextHint;
        _suppressTextChanged = false;
    }

    private void HidePlaceholder()
    {
        if (!_showingPlaceholder)
            return;
        _showingPlaceholder = false;
        _suppressTextChanged = true;
        _textBox.Text = string.Empty;
        _textBox.ForeColor = _normalForeColor;
        _suppressTextChanged = false;
    }

    private void UpdatePlaceholder()
    {
        if (string.IsNullOrEmpty(_textBox.Text) && !_textBox.Focused)
            ShowPlaceholder();
        else if (_showingPlaceholder)
            HidePlaceholder();
    }

    private void OnTextBoxEnter(object? sender, EventArgs e)
    {
        HidePlaceholder();
        if (!_hasError)
            _borderPanel.BackColor = DesignTokens.Colors.BorderFocus;
    }

    private void OnTextBoxLeave(object? sender, EventArgs e)
    {
        if (!_hasError)
            _borderPanel.BackColor = _borderColor;

        // Show placeholder if empty
        if (string.IsNullOrEmpty(_textBox.Text))
            ShowPlaceholder();

        // Auto-validate on leave
        if (_autoValidate)
            Validate();
    }

    #endregion

    #region Text Changed & Counter

    private void OnTextChanged(object? sender, EventArgs e)
    {
        if (_suppressTextChanged)
            return;
        UpdateCharCounter();
    }

    private void UpdateCharCounter()
    {
        if (_charCounterLabel.Visible && MaxLength > 0 && MaxLength < int.MaxValue)
        {
            int len = string.IsNullOrEmpty(Text) || Text == _placeholderText ? 0 : Text.Length;
            _charCounterLabel.Text = $"{len}/{MaxLength}";
            _charCounterLabel.ForeColor = len >= MaxLength
                ? DesignTokens.Colors.Error
                : DesignTokens.Colors.TextSecondary;
        }
    }

    #endregion

    #region Key Handling

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter key for form submission
        if (e.KeyCode == Keys.Enter && !_isMultiline)
        {
            e.Handled = true;
            EnterKeyPressed?.Invoke(this, EventArgs.Empty);
            Parent?.SelectNextControl(_textBox, true, true, true, true);
        }

        // Tab/Shift+Tab navigation
        if (e.KeyCode == Keys.Tab)
        {
            bool forward = !e.Shift;
            Parent?.SelectNextControl(_textBox, forward, true, true, true);
            e.Handled = true;
        }
    }

    private void OnTextBoxKeyPress(object? sender, KeyPressEventArgs e)
    {
        if (_hasError)
        {
            // Clear error on any key press
            ClearError();
        }

        // Don't filter if it's a control character
        if (char.IsControl(e.KeyChar))
            return;

        switch (ValidationMode)
        {
            case TextBoxValidationMode.NumericOnly:
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != '.' && e.KeyChar != '-')
                    e.Handled = true;
                break;

            case TextBoxValidationMode.AlphaOnly:
                if (!char.IsLetter(e.KeyChar) && e.KeyChar != ' ' && !char.IsControl(e.KeyChar))
                    e.Handled = true;
                break;

            case TextBoxValidationMode.Email:
                // Allow letters, digits, @, ., _, -
                if (!char.IsLetterOrDigit(e.KeyChar) && e.KeyChar != '@'
                    && e.KeyChar != '.' && e.KeyChar != '_' && e.KeyChar != '-')
                    e.Handled = true;
                break;

            case TextBoxValidationMode.Barcode:
                // Allow digits and common barcode chars
                if (!char.IsDigit(e.KeyChar) && e.KeyChar != '-'
                    && e.KeyChar != '*' && e.KeyChar != '/')
                    e.Handled = true;
                break;
        }
    }

    #endregion

    #region Password Toggle

    private void OnPasswordToggle(object? sender, EventArgs e)
    {
        if (_textBox.PasswordChar == '\0')
        {
            _textBox.PasswordChar = '●';
            _passwordToggleBtn.Text = FontAwesomeIcons.Eye;
        }
        else
        {
            _textBox.PasswordChar = '\0';
            _passwordToggleBtn.Text = FontAwesomeIcons.EyeSlash;
        }
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates the text box content. Returns true if valid.
    /// </summary>
    public new bool Validate()
    {
        string actualText = GetActualText();

        // Required check
        if (_isRequired && string.IsNullOrWhiteSpace(actualText))
        {
            SetError("هذا الحقل مطلوب");
            return false;
        }

        // Validation mode checks
        switch (ValidationMode)
        {
            case TextBoxValidationMode.NumericOnly:
                if (!string.IsNullOrEmpty(actualText) && !decimal.TryParse(actualText, out _))
                {
                    SetError("يرجى إدخال رقم صحيح");
                    return false;
                }
                break;

            case TextBoxValidationMode.Email:
                if (!string.IsNullOrEmpty(actualText)
                    && !Regex.IsMatch(actualText, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    SetError("يرجى إدخال بريد إلكتروني صحيح");
                    return false;
                }
                break;

            case TextBoxValidationMode.Barcode:
                if (!string.IsNullOrEmpty(actualText) && actualText.Length < 3)
                {
                    SetError("يرجى إدخال باركود صحيح (3 أحرف على الأقل)");
                    return false;
                }
                break;
        }

        ClearError();
        return true;
    }

    /// <summary>
    /// Gets the actual user-entered text, excluding placeholder.
    /// </summary>
    public string GetActualText()
    {
        return _showingPlaceholder ? string.Empty : _textBox.Text;
    }

    #endregion

    #region Error Provider

    public void SetError(string message)
    {
        _hasError = true;
        _errorProvider.SetError(_textBox, message);
        _borderPanel.BackColor = DesignTokens.Colors.Error;
        _errorMessageLabel.Text = message;
        _errorMessageLabel.Visible = true;
        _errorIcon.Visible = true;
        UpdateLayout();
    }

    public void ClearError()
    {
        _hasError = false;
        _errorProvider.Clear();
        _borderPanel.BackColor = _borderColor;
        _errorMessageLabel.Visible = false;
        _errorIcon.Visible = false;
        UpdateLayout();
    }

    #endregion

    #region Public Helpers

    public void Clear()
    {
        _showingPlaceholder = false;
        _suppressTextChanged = true;
        _textBox.Text = string.Empty;
        _textBox.ForeColor = _normalForeColor;
        _suppressTextChanged = false;
        ClearError();
        UpdatePlaceholder();
        UpdateCharCounter();
    }

    public void SelectAll()
    {
        _textBox.SelectAll();
    }

    public new void Focus()
    {
        _textBox.Focus();
    }

    #endregion

    #region Cleanup

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _errorProvider?.Dispose();
            _toolTip?.Dispose();
            _errorIcon?.Image?.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion
}
