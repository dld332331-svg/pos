namespace POS.Desktop.CustomControls;
using System.Drawing;
using System.Windows.Forms;
using POS.Desktop.Themes;

public class RtlButton : Button
{
    private Color _btnColor;
    private Color _hoverColor;
    private Color _pressedColor;
    private Color _textColor;
    private bool _isLoading;
    private string _successText = "";
    private string _errorText = "";
    private Timer? _stateTimer;
    private const int StateDisplayMs = 2000;
    public string? ButtonId { get; set; }
    public string? ArabicText { get => _arabicText; set { _arabicText = value; if (!_isLoading && !_showingSuccess && !_showingError) Text = value; } }
    private string? _arabicText;
    public string? EnglishText { get; set; }
    public string? Purpose { get; set; }
    public string? Permission { get; set; }
    public Keys KeyboardShortcut { get; set; } = Keys.None;
    public Action? SuccessBehavior { get; set; }
    public Action? FailureBehavior { get; set; }

    public RtlButton()
    {
        _btnColor = Colors.Primary;
        _hoverColor = Colors.PrimaryHover;
        _pressedColor = Colors.PrimaryPressed;
        _textColor = Color.White;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = Typography.ButtonBold;
        Height = DesignTokens.ControlHeight.Standard;
        RightToLeft = RightToLeft.Yes;
        Cursor = Cursors.Hand;
        BackColor = _btnColor;
        ForeColor = _textColor;
        Margin = new Padding(DesignTokens.Spacing.Small);
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    public enum ButtonType { Primary, Secondary, Destructive, Ghost, Success }
    private ButtonType _buttonType = ButtonType.Primary;
    public ButtonType Type
    {
        get => _buttonType;
        set
        {
            _buttonType = value;
            switch (value)
            {
                case ButtonType.Primary:
                    _btnColor = Colors.Primary; _hoverColor = Colors.PrimaryHover; _pressedColor = Colors.PrimaryPressed; _textColor = Color.White; break;
                case ButtonType.Secondary:
                    _btnColor = Colors.Surface; _hoverColor = Color.FromArgb(240, 240, 245); _pressedColor = Color.FromArgb(230, 230, 235); _textColor = Colors.TextPrimary; break;
                case ButtonType.Destructive:
                    _btnColor = Colors.Danger; _hoverColor = Colors.DangerHover; _pressedColor = Color.FromArgb(180, 30, 30); _textColor = Color.White; break;
                case ButtonType.Ghost:
                    _btnColor = Color.Transparent; _hoverColor = Color.FromArgb(240, 240, 245); _pressedColor = Color.FromArgb(230, 230, 235); _textColor = Colors.Primary; break;
                case ButtonType.Success:
                    _btnColor = Colors.Success; _hoverColor = Color.FromArgb(38, 140, 57); _pressedColor = Color.FromArgb(30, 120, 47); _textColor = Color.White; break;
            }
            if (!_isLoading && !_showingSuccess && !_showingError) { BackColor = _btnColor; ForeColor = _textColor; }
        }
    }

    public enum ButtonSize { Compact, Standard, Large }
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
                _ => DesignTokens.ControlHeight.Standard
            };
        }
    }

    public bool IsLoading { get => _isLoading; set { _isLoading = value; Enabled = !_isLoading; Text = _isLoading ? "جاري التنفيذ..." : _originalText; if (_isLoading) { _showingSuccess = false; _showingError = false; } Invalidate(); } }
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
        BackColor = Colors.Success;
        ForeColor = Color.White;
        Text = $"✓ {_successText}";
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
        BackColor = Colors.Error;
        ForeColor = Color.White;
        Text = $"✗ {_errorText}";
        Enabled = false;
        FailureBehavior?.Invoke();
        StartStateTimer();
    }

    private void StartStateTimer()
    {
        _stateTimer?.Stop();
        _stateTimer = new Timer { Interval = StateDisplayMs };
        _stateTimer.Tick += (s, e) =>
        {
            _stateTimer.Stop();
            _showingSuccess = false;
            _showingError = false;
            Enabled = true;
            BackColor = _btnColor;
            ForeColor = _textColor;
            Text = _originalText;
            Invalidate();
        };
        _stateTimer.Start();
    }

    public bool HasFocusVisual { get; set; } = true;

    protected override void OnTextChanged(EventArgs e) { if (!_isLoading && !_showingSuccess && !_showingError) { _originalText = Text; _arabicText = Text; } base.OnTextChanged(e); }
    protected override void OnMouseEnter(EventArgs e) { if (Enabled && !_showingSuccess && !_showingError) BackColor = _hoverColor; base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { if (!_showingSuccess && !_showingError) BackColor = _btnColor; base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { if (Enabled && !_showingSuccess && !_showingError) BackColor = _pressedColor; base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { if (Enabled && !_showingSuccess && !_showingError) BackColor = _hoverColor; base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (!Enabled && !_showingSuccess && !_showingError) BackColor = Colors.Disabled; else if (!_showingSuccess && !_showingError) BackColor = _btnColor; }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (HasFocusVisual && Focused && !_showingSuccess && !_showingError)
        {
            using var focusPen = new Pen(Colors.Primary, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dot };
            var r = ClientRectangle;
            r.Inflate(-2, -2);
            e.Graphics.DrawRectangle(focusPen, r);
        }
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
}
