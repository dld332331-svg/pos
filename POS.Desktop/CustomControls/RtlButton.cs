namespace POS.Desktop.CustomControls;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using POS.Desktop.Themes;

/// <summary>
/// Modern RTL button with rounded corners, icon support, loading/success/error states,
/// keyboard shortcut, and smooth hover/pressed transitions.
/// </summary>
public class RtlButton : Button
{
    private Color _btnColor;
    private Color _hoverColor;
    private Color _pressedColor;
    private Color _textColor;
    private Color _borderColor = Color.Transparent;
    private bool _isLoading;
    private string _successText = "";
    private string _errorText = "";
    private Timer? _stateTimer;
    private const int StateDisplayMs = 2000;
    private int _cornerRadius = DesignTokens.Radius.Md;
    private bool _suppressTextChanged;

    public string? ButtonId { get; set; }
    public string? ArabicText { get => _arabicText; set { _arabicText = value; if (!_isLoading && !_showingSuccess && !_showingError) Text = value; } }
    private string? _arabicText;
    public string? EnglishText { get; set; }
    public string? Purpose { get; set; }
    public string? Permission { get; set; }
    public Keys KeyboardShortcut { get; set; } = Keys.None;
    public Action? SuccessBehavior { get; set; }
    public Action? FailureBehavior { get; set; }

    public string? IconText { get; set; }
    public float IconSize { get; set; } = 14f;
    public int IconSpacing { get; set; } = 8;
    public bool ShowIconBeforeText { get; set; } = true;

    public RtlButton()
    {
        _btnColor = DesignTokens.Colors.Primary;
        _hoverColor = DesignTokens.Colors.PrimaryHover;
        _pressedColor = DesignTokens.Colors.PrimaryPressed;
        _textColor = Color.White;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = DesignTokens.Typography.ButtonBold;
        Height = DesignTokens.ControlHeight.Standard;
        RightToLeft = RightToLeft.Yes;
        Cursor = Cursors.Hand;
        BackColor = _btnColor;
        ForeColor = _textColor;
        Margin = new Padding(DesignTokens.Spacing.Small);
        Padding = new Padding(DesignTokens.Spacing.Standard, 0, DesignTokens.Spacing.Standard, 0);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        UseVisualStyleBackColor = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
    }

    public enum ButtonType { Primary, Secondary, Destructive, Ghost, Success, Outline, Accent }
    private ButtonType _buttonType = ButtonType.Primary;
    public ButtonType Type
    {
        get => _buttonType;
        set
        {
            _buttonType = value;
            ApplyButtonType();
            if (!_isLoading && !_showingSuccess && !_showingError) { BackColor = _btnColor; ForeColor = _textColor; }
            Invalidate();
        }
    }

    private void ApplyButtonType()
    {
        switch (_buttonType)
        {
            case ButtonType.Primary:
                _btnColor = DesignTokens.Colors.Primary;
                _hoverColor = DesignTokens.Colors.PrimaryHover;
                _pressedColor = DesignTokens.Colors.PrimaryPressed;
                _textColor = Color.White;
                _borderColor = Color.Transparent;
                break;
            case ButtonType.Secondary:
                _btnColor = DesignTokens.Colors.Surface;
                _hoverColor = DesignTokens.Colors.Background;
                _pressedColor = DesignTokens.Colors.BorderLight;
                _textColor = DesignTokens.Colors.TextPrimary;
                _borderColor = DesignTokens.Colors.Border;
                break;
            case ButtonType.Destructive:
                _btnColor = DesignTokens.Colors.Danger;
                _hoverColor = DesignTokens.Colors.DangerHover;
                _pressedColor = Color.FromArgb(185, 28, 28);
                _textColor = Color.White;
                _borderColor = Color.Transparent;
                break;
            case ButtonType.Ghost:
                _btnColor = Color.Transparent;
                _hoverColor = DesignTokens.Colors.Background;
                _pressedColor = DesignTokens.Colors.BorderLight;
                _textColor = DesignTokens.Colors.Primary;
                _borderColor = Color.Transparent;
                break;
            case ButtonType.Success:
                _btnColor = DesignTokens.Colors.Success;
                _hoverColor = Color.FromArgb(5, 150, 105);
                _pressedColor = Color.FromArgb(4, 120, 87);
                _textColor = Color.White;
                _borderColor = Color.Transparent;
                break;
            case ButtonType.Outline:
                _btnColor = Color.Transparent;
                _hoverColor = DesignTokens.Colors.PrimaryLighter;
                _pressedColor = DesignTokens.Colors.PrimaryLight;
                _textColor = DesignTokens.Colors.Primary;
                _borderColor = DesignTokens.Colors.Primary;
                break;
            case ButtonType.Accent:
                _btnColor = DesignTokens.Colors.Accent;
                _hoverColor = DesignTokens.Colors.AccentHover;
                _pressedColor = Color.FromArgb(190, 18, 60);
                _textColor = Color.White;
                _borderColor = Color.Transparent;
                break;
        }
    }

    public enum ButtonSize { Compact, Standard, Large, Extra }
    private ButtonSize _buttonSize = ButtonSize.Standard;
    public ButtonSize SizeType
    {
        get => _buttonSize;
        set
        {
            _buttonSize = value;
            Height = value switch
            {
                ButtonSize.Compact => DesignTokens.ControlHeight.Compact,
                ButtonSize.Large => DesignTokens.ControlHeight.Large,
                ButtonSize.Extra => DesignTokens.ControlHeight.Touch,
                _ => DesignTokens.ControlHeight.Standard
            };
            Invalidate();
        }
    }

    public int CornerRadius
    {
        get => _cornerRadius;
        set { _cornerRadius = Math.Max(0, Math.Min(30, value)); Invalidate(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            Enabled = !_isLoading;
            _suppressTextChanged = true;
            Text = _isLoading ? "جاري التنفيذ..." : _originalText;
            _suppressTextChanged = false;
            if (_isLoading) { _showingSuccess = false; _showingError = false; }
            Invalidate();
        }
    }

    private string _originalText = "";

    public enum ButtonState { Normal, Hover, Pressed, Focused, Disabled, Loading, Success, Error }
    public ButtonState CurrentVisualState
    {
        get
        {
            if (_showingSuccess) return ButtonState.Success;
            if (_showingError) return ButtonState.Error;
            if (_isLoading) return ButtonState.Loading;
            if (!Enabled) return ButtonState.Disabled;
            if (Focused) return ButtonState.Focused;
            return ButtonState.Normal;
        }
    }

    private bool _showingSuccess;
    public void ShowSuccess(string? message = null)
    {
        _showingSuccess = true;
        _showingError = false;
        _isLoading = false;
        _successText = message ?? "تم بنجاح";
        BackColor = DesignTokens.Colors.Success;
        ForeColor = Color.White;
        _suppressTextChanged = true;
        Text = $"{FontAwesomeIcons.Success} {_successText}";
        _suppressTextChanged = false;
        Enabled = false;
        SuccessBehavior?.Invoke();
        StartStateTimer();
    }

    private bool _showingError;
    public void ShowError(string? message = null)
    {
        _showingError = true;
        _showingSuccess = false;
        _isLoading = false;
        _errorText = message ?? "خطأ";
        BackColor = DesignTokens.Colors.Error;
        ForeColor = Color.White;
        _suppressTextChanged = true;
        Text = $"{FontAwesomeIcons.Error} {_errorText}";
        _suppressTextChanged = false;
        Enabled = false;
        FailureBehavior?.Invoke();
        StartStateTimer();
    }

    private void StartStateTimer()
    {
        _stateTimer?.Stop();
        _stateTimer?.Dispose();
        _stateTimer = new Timer { Interval = StateDisplayMs };
        _stateTimer.Tick += (s, e) =>
        {
            _stateTimer.Stop();
            _showingSuccess = false;
            _showingError = false;
            Enabled = true;
            BackColor = _btnColor;
            ForeColor = _textColor;
            _suppressTextChanged = true;
            Text = _originalText;
            _suppressTextChanged = false;
            Invalidate();
        };
        _stateTimer.Start();
    }

    public bool HasFocusVisual { get; set; } = true;

    protected override void OnTextChanged(EventArgs e)
    {
        if (!_suppressTextChanged && !_isLoading && !_showingSuccess && !_showingError)
        {
            _originalText = Text;
            _arabicText = Text;
        }
        base.OnTextChanged(e);
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        if (Enabled && !_showingSuccess && !_showingError)
            BackColor = _hoverColor;
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        if (!_showingSuccess && !_showingError)
            BackColor = _btnColor;
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (Enabled && !_showingSuccess && !_showingError)
            BackColor = _pressedColor;
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (Enabled && !_showingSuccess && !_showingError)
            BackColor = _hoverColor;
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (!Enabled && !_showingSuccess && !_showingError)
        {
            BackColor = DesignTokens.Colors.DisabledBg;
            ForeColor = DesignTokens.Colors.DisabledText;
        }
        else if (!_showingSuccess && !_showingError)
        {
            BackColor = _btnColor;
            ForeColor = _textColor;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var rect = ClientRectangle;
        rect.Inflate(-1, -1);
        int radius = _cornerRadius;

        using var path = DesignTokens.CreateRoundedRect(rect, radius);
        Region = new Region(path);

        // Background fill
        using (var bgBrush = new SolidBrush(BackColor))
        {
            g.FillPath(bgBrush, path);
        }

        // Border for outline/secondary
        if (_borderColor != Color.Transparent && _buttonType is ButtonType.Secondary or ButtonType.Outline)
        {
            using var borderPen = new Pen(_borderColor, 1);
            g.DrawPath(borderPen, path);
        }

        // Focus indicator
        if (HasFocusVisual && Focused && !_showingSuccess && !_showingError && _buttonType != ButtonType.Outline)
        {
            using var focusPen = new Pen(DesignTokens.Colors.Primary.WithAlpha(120), 2) { DashStyle = DashStyle.Dot };
            var r = rect;
            r.Inflate(-3, -3);
            using var focusPath = DesignTokens.CreateRoundedRect(r, Math.Max(0, radius - 3));
            g.DrawPath(focusPen, focusPath);
        }

        // Text + icon layout
        string displayText = _isLoading ? Text : (_showingSuccess ? $"{FontAwesomeIcons.Success} {_successText}" : (_showingError ? $"{FontAwesomeIcons.Error} {_errorText}" : _originalText));
        if (string.IsNullOrEmpty(displayText) && string.IsNullOrEmpty(IconText)) return;

        var textSize = g.MeasureString(displayText, Font);
        using var iconFont = Icons.FontLoader.GetFontAwesomeSolid(IconSize);
        var iconSize = string.IsNullOrEmpty(IconText) ? SizeF.Empty : g.MeasureString(IconText, iconFont);
        int gap = string.IsNullOrEmpty(IconText) || string.IsNullOrEmpty(displayText) ? 0 : IconSpacing;
        float totalWidth = iconSize.Width + gap + textSize.Width;
        float startX = (rect.Width - totalWidth) / 2;
        float centerY = rect.Height / 2f;

        using var textBrush = new SolidBrush(ForeColor);
        using var iconBrush = new SolidBrush(ForeColor);

        // Layout in normal coordinates (X=0 left, X=width right). In RTL visual terms,
        // ShowIconBeforeText=true means icon on the right (higher X), text on the left.
        float iconX = ShowIconBeforeText ? startX + textSize.Width + gap : startX;
        float textX = ShowIconBeforeText ? startX : startX + iconSize.Width + gap;

        if (!string.IsNullOrEmpty(IconText))
        {
            g.DrawString(IconText, iconFont, iconBrush, iconX, centerY - iconSize.Height / 2);
        }

        using (var format = new StringFormat(StringFormatFlags.DirectionRightToLeft))
        {
            format.Alignment = StringAlignment.Near;
            format.LineAlignment = StringAlignment.Center;
            // With RTL + Near, x is the right edge of the text block.
            g.DrawString(displayText, Font, textBrush, textX + textSize.Width, centerY, format);
        }
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _stateTimer?.Stop();
            _stateTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
