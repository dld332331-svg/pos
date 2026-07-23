using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.CustomControls;
using POS.Desktop.Icons;
using POS.Desktop.Themes;
using POS.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace POS.Desktop.Forms;

/// <summary>
/// AUTH-001: Modern login form with RTL layout, Arabic text, and full state management.
/// Redesigned with modern card layout, soft gradient background, rounded inputs,
/// Font Awesome iconography, and smooth loading states.
/// </summary>
public class LoginForm : Form
{
    private enum LoginState
    {
        Initial,
        Loading,
        InvalidCredentials,
        LockedUser,
        DisabledUser,
        DatabaseUnavailable
    }

    private readonly IAuthService? _authService;
    private LoginState _currentState = LoginState.Initial;

    // UI Controls
    private Panel _mainPanel = null!;
    private Panel _loginCard = null!;
    private Panel _logoIconPanel = null!;
    private Label _logoIconLabel = null!;
    private Label _appNameLabel = null!;
    private Label _businessNameLabel = null!;
    private RtlTextBox _usernameTextBox = null!;
    private RtlTextBox _passwordTextBox = null!;
    private RtlButton _loginButton = null!;
    private Panel _dbStatusPanel = null!;
    private Label _dbStatusLabel = null!;
    private PictureBox _dbStatusIcon = null!;
    private Label _versionLabel = null!;
    private Panel _errorMessagePanel = null!;
    private Label _errorIconLabel = null!;
    private Label _errorMessageLabel = null!;
    private Panel _loadingOverlay = null!;
    private Label _spinnerLabel = null!;
    private Timer _spinnerTimer = null!;
    private int _spinnerFrame;

    // Braille spinner characters for smooth animation
    private static readonly char[] SpinnerChars = {
        '\u280B', '\u2819', '\u2839', '\u2838', '\u283C', '\u2834', '\u2826', '\u2827', '\u2807', '\u280F'
    };

    public event EventHandler<LoginResponse>? LoginSuccessful;
    public event EventHandler<string>? DatabaseConnectionChanged;

    public LoginForm()
    {
        InitializeComponent();
        SetState(LoginState.Initial);
    }

    public LoginForm(IAuthService authService) : this()
    {
        _authService = authService;
    }

    private void InitializeComponent()
    {
        // ── Form Setup ──
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Text = "تسجيل الدخول - نظام نقاط البيع";
        ClientSize = new Size(520, 720);
        MinimumSize = new Size(460, 660);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = DesignTokens.Colors.Background;
        Font = DesignTokens.Typography.Body;

        // ── Main Panel: full-size background with gradient feel ──
        _mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = DesignTokens.Colors.Background
        };
        _mainPanel.Paint += MainPanel_Paint;

        // ── Login Card: centered white card with elevation ──
        _loginCard = new Panel
        {
            Size = new Size(420, 580),
            BackColor = DesignTokens.Colors.Surface,
            BorderStyle = BorderStyle.None
        };
        _loginCard.Paint += LoginCard_Paint;

        // ── Logo Icon: circular badge with POS icon ──
        _logoIconPanel = new Panel
        {
            Size = new Size(84, 84),
            BackColor = DesignTokens.Colors.Primary
        };
        _logoIconPanel.Paint += LogoIconPanel_Paint;

        _logoIconLabel = new Label
        {
            Text = FontAwesomeIcons.PosTerminal,
            Font = FontLoader.GetFontAwesomeSolid(36f),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        _logoIconPanel.Controls.Add(_logoIconLabel);

        // ── App Name ──
        _appNameLabel = new Label
        {
            Text = "نظام نقاط البيع",
            Font = DesignTokens.Typography.AppTitle,
            ForeColor = DesignTokens.Colors.TextPrimary,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(320, 40),
            BackColor = Color.Transparent
        };

        // ── Business Name ──
        _businessNameLabel = new Label
        {
            Text = "اسم المنشأة",
            Font = DesignTokens.Typography.Secondary,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(320, 24),
            BackColor = Color.Transparent
        };

        // ── Decorative accent bar ──
        var accentBar = new Panel
        {
            Size = new Size(80, 4),
            BackColor = DesignTokens.Colors.Primary,
            BorderStyle = BorderStyle.None
        };
        accentBar.Paint += (s, e) =>
        {
            using var path = DesignTokens.CreateRoundedRect(accentBar.ClientRectangle, 2);
            accentBar.Region = new Region(path);
        };

        // ── Username TextBox ──
        _usernameTextBox = new RtlTextBox
        {
            Size = new Size(320, DesignTokens.ControlHeight.Standard + 8),
            LabelText = "اسم المستخدم",
            PlaceholderText = "أدخل اسم المستخدم",
            IsRequired = true,
            PrefixIcon = FontAwesomeIcons.User,
            CornerRadius = DesignTokens.Radius.Lg
        };

        // ── Password TextBox ──
        _passwordTextBox = new RtlTextBox
        {
            Size = new Size(320, DesignTokens.ControlHeight.Standard + 8),
            LabelText = "كلمة المرور",
            PlaceholderText = "أدخل كلمة المرور",
            IsRequired = true,
            PasswordChar = '●',
            PrefixIcon = FontAwesomeIcons.Lock,
            CornerRadius = DesignTokens.Radius.Lg
        };

        // ── Login Button ──
        _loginButton = new RtlButton
        {
            Text = "تسجيل الدخول",
            Size = new Size(320, DesignTokens.ControlHeight.Large),
            Type = RtlButton.ButtonType.Primary,
            IconText = FontAwesomeIcons.Login,
            ShowIconBeforeText = false,
            CornerRadius = DesignTokens.Radius.Lg,
            SizeType = RtlButton.ButtonSize.Large
        };

        // ── Error Message Panel ──
        _errorMessagePanel = new Panel
        {
            Size = new Size(320, 40),
            BackColor = Color.Transparent,
            Visible = false
        };

        _errorIconLabel = new Label
        {
            Font = FontLoader.GetFontAwesomeSolid(16f),
            Size = new Size(24, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Location = new Point(296, 8)
        };

        _errorMessageLabel = new Label
        {
            Font = DesignTokens.Typography.Secondary,
            TextAlign = ContentAlignment.MiddleRight,
            Size = new Size(270, 40),
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes,
            Location = new Point(20, 0)
        };

        _errorMessagePanel.Controls.Add(_errorIconLabel);
        _errorMessagePanel.Controls.Add(_errorMessageLabel);

        // ── Database Status ──
        _dbStatusPanel = new Panel
        {
            Size = new Size(320, 28),
            BackColor = Color.Transparent
        };

        _dbStatusIcon = new PictureBox
        {
            Size = new Size(12, 12),
            BackColor = DesignTokens.Colors.Disabled,
            SizeMode = PictureBoxSizeMode.CenterImage,
            Location = new Point(4, 8)
        };
        using (var iconPath = new GraphicsPath())
        {
            iconPath.AddEllipse(0, 0, 12, 12);
            _dbStatusIcon.Region = new Region(iconPath);
        }

        _dbStatusLabel = new Label
        {
            Text = "جاري التحقق من قاعدة البيانات...",
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.TextHint,
            TextAlign = ContentAlignment.MiddleRight,
            Size = new Size(296, 24),
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes,
            Location = new Point(24, 0)
        };

        // ── Loading Overlay ──
        _loadingOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(230, DesignTokens.Colors.Surface),
            Visible = false
        };

        _spinnerLabel = new Label
        {
            Text = "",
            Font = FontLoader.GetFontAwesomeSolid(40f),
            ForeColor = DesignTokens.Colors.Primary,
            Size = new Size(60, 60),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };

        var loadingLabel = new Label
        {
            Text = "جاري تسجيل الدخول...",
            Font = DesignTokens.Typography.Body,
            ForeColor = DesignTokens.Colors.TextSecondary,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(220, 28),
            BackColor = Color.Transparent,
            RightToLeft = RightToLeft.Yes
        };

        // ── Spinner Timer (animated spinner) ──
        _spinnerTimer = new Timer { Interval = 100 };
        _spinnerTimer.Tick += (s, e) =>
        {
            _spinnerFrame = (_spinnerFrame + 1) % SpinnerChars.Length;
            if (!_spinnerLabel.IsDisposed)
                _spinnerLabel.Text = SpinnerChars[_spinnerFrame].ToString();
        };

        // ── Version Label ──
        _versionLabel = new Label
        {
            Text = "الإصدار 1.0.0",
            Font = DesignTokens.Typography.Caption,
            ForeColor = DesignTokens.Colors.TextHint,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(320, 20),
            BackColor = Color.Transparent
        };

        // ── Position All Controls within _loginCard ──
        _logoIconPanel.Location = new Point((420 - 84) / 2, 28);
        _appNameLabel.Location = new Point(50, 124);
        _businessNameLabel.Location = new Point(50, 166);
        accentBar.Location = new Point((420 - 80) / 2, 196);

        _usernameTextBox.Location = new Point(50, 228);
        _passwordTextBox.Location = new Point(50, 306);
        _loginButton.Location = new Point(50, 388);
        _errorMessagePanel.Location = new Point(50, 442);

        _dbStatusPanel.Location = new Point(50, 494);
        _versionLabel.Location = new Point(50, 528);

        _spinnerLabel.Location = new Point((420 - 60) / 2, 220);
        loadingLabel.Location = new Point((420 - 220) / 2, 288);

        // Assemble DB status panel
        _dbStatusPanel.Controls.Add(_dbStatusIcon);
        _dbStatusPanel.Controls.Add(_dbStatusLabel);

        // Assemble loading overlay
        _loadingOverlay.Controls.Add(loadingLabel);
        _loadingOverlay.Controls.Add(_spinnerLabel);

        // Assemble login card
        _loginCard.Controls.AddRange(new Control[]
        {
            _logoIconPanel,
            _appNameLabel,
            _businessNameLabel,
            accentBar,
            _usernameTextBox,
            _passwordTextBox,
            _loginButton,
            _errorMessagePanel,
            _dbStatusPanel,
            _versionLabel,
            _loadingOverlay
        });

        // Assemble main panel
        _mainPanel.Controls.Add(_loginCard);

        // Assemble form
        Controls.Add(_mainPanel);

        // ── Events ──
        _loginButton.Click += async (s, e) => await AttemptLoginAsync();
        _passwordTextBox.EnterKeyPressed += async (s, e) => await AttemptLoginAsync();
        _usernameTextBox.EnterKeyPressed += (s, e) => _passwordTextBox.Focus();

        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };
        Load += async (s, e) => await CheckDatabaseConnectionAsync();
        Resize += (s, e) => CenterCard();
    }

    private void MainPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = _mainPanel.ClientRectangle;

        // Soft gradient background
        using var brush = new LinearGradientBrush(
            rect,
            DesignTokens.Colors.Background,
            DesignTokens.Colors.PrimaryLighter,
            LinearGradientMode.Vertical);
        g.FillRectangle(brush, rect);

        // Decorative circles
        using (var circleBrush = new SolidBrush(DesignTokens.Colors.PrimaryLight))
        {
            g.FillEllipse(circleBrush, -60, -60, 240, 240);
            g.FillEllipse(circleBrush, rect.Width - 180, rect.Height - 180, 260, 260);
        }
    }

    private void LoginCard_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = _loginCard.ClientRectangle;

        // Soft shadow
        for (int i = 0; i < 8; i++)
        {
            using var shadowPen = new Pen(Color.FromArgb(12 - i, 0, 0, 0), 1);
            var shadowRect = new Rectangle(rect.X + i, rect.Y + i, rect.Width - i * 2, rect.Height - i * 2);
            using var shadowPath = DesignTokens.CreateRoundedRect(shadowRect, DesignTokens.Radius.Xl);
            g.DrawPath(shadowPen, shadowPath);
        }

        // Card rounded region
        using var path = DesignTokens.CreateRoundedRect(rect, DesignTokens.Radius.Xl);
        _loginCard.Region = new Region(path);
    }

    private void LogoIconPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = new GraphicsPath();
        path.AddEllipse(0, 0, _logoIconPanel.Width - 1, _logoIconPanel.Height - 1);
        _logoIconPanel.Region = new Region(path);

        // Subtle inner glow
        using var glow = new SolidBrush(DesignTokens.Colors.PrimarySoft);
        g.FillEllipse(glow, 0, 0, _logoIconPanel.Width - 1, _logoIconPanel.Height - 1);
    }

    private void CenterCard()
    {
        _loginCard.Location = new Point(
            (_mainPanel.Width - _loginCard.Width) / 2,
            (_mainPanel.Height - _loginCard.Height) / 2 - 16);
    }

    private async Task CheckDatabaseConnectionAsync()
    {
        SetDbStatus("جاري التحقق من قاعدة البيانات...", DesignTokens.Colors.Warning);

        try
        {
            IAuthService? authService = _authService;
            if (authService == null && AppServiceProvider.Provider != null)
            {
                using var scope = AppServiceProvider.Provider.CreateScope();
                authService = scope.ServiceProvider.GetService(typeof(IAuthService)) as IAuthService;
            }

            if (authService != null)
            {
                var connected = await authService.CheckDatabaseConnectionAsync();
                if (connected)
                {
                    SetDbStatus("متصل بقاعدة البيانات", DesignTokens.Colors.Success);
                    DatabaseConnectionChanged?.Invoke(this, "connected");
                }
                else
                {
                    SetDbStatus("غير متصل بقاعدة البيانات", DesignTokens.Colors.Error);
                    DatabaseConnectionChanged?.Invoke(this, "disconnected");
                    SetState(LoginState.DatabaseUnavailable);
                }
            }
            else
            {
                SetDbStatus("الخدمة غير متوفرة (وضع العرض)", DesignTokens.Colors.Warning);
            }
        }
        catch
        {
            SetDbStatus("غير متصل بقاعدة البيانات", DesignTokens.Colors.Error);
            DatabaseConnectionChanged?.Invoke(this, "disconnected");
            SetState(LoginState.DatabaseUnavailable);
        }
    }

    private void SetDbStatus(string text, Color color)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => SetDbStatus(text, color)));
            return;
        }
        _dbStatusLabel.Text = text;
        _dbStatusIcon.BackColor = color;
    }

    private void SetError(string message, string icon, Color color)
    {
        _errorMessagePanel.Visible = true;
        _errorIconLabel.Text = icon;
        _errorIconLabel.ForeColor = color;
        _errorMessageLabel.Text = message;
        _errorMessageLabel.ForeColor = color;
    }

    private void HideError()
    {
        _errorMessagePanel.Visible = false;
    }

    private void SetState(LoginState state)
    {
        _currentState = state;

        // Stop spinner timer on any state transition
        _spinnerTimer.Stop();

        switch (state)
        {
            case LoginState.Initial:
                HideError();
                _loadingOverlay.Visible = false;
                _loginButton.Enabled = true;
                _loginButton.Text = "تسجيل الدخول";
                _loginButton.BackColor = DesignTokens.Colors.Primary;
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                break;

            case LoginState.Loading:
                HideError();
                _loadingOverlay.Visible = true;
                _loadingOverlay.BringToFront();
                _loginButton.Enabled = false;
                _loginButton.Text = "جاري التسجيل...";
                _usernameTextBox.Enabled = false;
                _passwordTextBox.Enabled = false;
                // Start animated spinner
                _spinnerFrame = 0;
                _spinnerLabel.Text = SpinnerChars[0].ToString();
                _spinnerTimer.Start();
                break;

            case LoginState.InvalidCredentials:
                SetError("اسم المستخدم أو كلمة المرور غير صحيحة", FontAwesomeIcons.Error, DesignTokens.Colors.Error);
                _loadingOverlay.Visible = false;
                _loginButton.Enabled = true;
                _loginButton.Text = "تسجيل الدخول";
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                _passwordTextBox.Focus();
                _passwordTextBox.SelectAll();
                break;

            case LoginState.LockedUser:
                SetError("تم قفل هذا الحساب. يرجى التواصل مع المسؤول.", FontAwesomeIcons.Warning, DesignTokens.Colors.Warning);
                _loadingOverlay.Visible = false;
                _loginButton.Enabled = true;
                _loginButton.Text = "تسجيل الدخول";
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                break;

            case LoginState.DisabledUser:
                SetError("هذا الحساب معطل. يرجى التواصل مع المسؤول.", FontAwesomeIcons.Warning, DesignTokens.Colors.Warning);
                _loadingOverlay.Visible = false;
                _loginButton.Enabled = true;
                _loginButton.Text = "تسجيل الدخول";
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                break;

            case LoginState.DatabaseUnavailable:
                SetError("لا يمكن الاتصال بقاعدة البيانات. تأكد من تشغيل الخادم.", FontAwesomeIcons.Error, DesignTokens.Colors.Error);
                _loadingOverlay.Visible = false;
                _loginButton.Enabled = false;
                _loginButton.Text = "غير متاح";
                _loginButton.BackColor = DesignTokens.Colors.Disabled;
                SetDbStatus("غير متصل", DesignTokens.Colors.Error);
                break;
        }
    }

    private async Task AttemptLoginAsync()
    {
        var username = _usernameTextBox.Text.Trim();
        var password = _passwordTextBox.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            SetError("يرجى إدخال اسم المستخدم", FontAwesomeIcons.Warning, DesignTokens.Colors.Warning);
            _usernameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            SetError("يرجى إدخال كلمة المرور", FontAwesomeIcons.Warning, DesignTokens.Colors.Warning);
            _passwordTextBox.Focus();
            return;
        }

        SetState(LoginState.Loading);

        try
        {
            IAuthService? authService = _authService;
            if (authService == null && AppServiceProvider.Provider != null)
            {
                using var scope = AppServiceProvider.Provider.CreateScope();
                authService = scope.ServiceProvider.GetService(typeof(IAuthService)) as IAuthService;
            }

            if (authService == null)
            {
                await Task.Delay(1500);
                PlaySound(SoundEvent.LoginSuccess);
                LoginSuccessful?.Invoke(this, new LoginResponse(
                    Guid.NewGuid(), "المدير", "Admin", false,
                    new System.Collections.Generic.List<string> { "Sell", "ViewDashboard", "ManageProducts" }));
                Close();
                return;
            }

            var result = await authService.LoginAsync(username, password);

            if (result.Success && result.User != null)
            {
                PlaySound(SoundEvent.LoginSuccess);
                var response = new LoginResponse(
                    result.User.Id, result.User.FullName, result.User.Role.ToString(),
                    result.User.MustChangePassword, new System.Collections.Generic.List<string>());
                LoginSuccessful?.Invoke(this, response);
                Close();
            }
            else
            {
                PlaySound(SoundEvent.LoginFailure);
                var errorMsg = result.Message ?? "";

                if (errorMsg.Contains("قفل") || errorMsg.Contains("lock", StringComparison.OrdinalIgnoreCase))
                    SetState(LoginState.LockedUser);
                else if (errorMsg.Contains("معطل") || errorMsg.Contains("disable", StringComparison.OrdinalIgnoreCase))
                    SetState(LoginState.DisabledUser);
                else
                    SetState(LoginState.InvalidCredentials);
            }
        }
        catch (Exception)
        {
            SetState(LoginState.DatabaseUnavailable);
        }
    }

    /// <summary>
    /// Sets the business name displayed on the login form.
    /// </summary>
    public void SetBusinessName(string name)
    {
        _businessNameLabel.Text = name;
    }

    /// <summary>
    /// Sets the application version displayed on the login form.
    /// </summary>
    public void SetAppVersion(string version)
    {
        _versionLabel.Text = $"الإصدار {version}";
    }

    /// <summary>
    /// Updates the database connection status indicator.
    /// </summary>
    public void SetDatabaseConnected(bool connected)
    {
        if (connected)
        {
            SetDbStatus("متصل بقاعدة البيانات", DesignTokens.Colors.Success);
            if (_currentState == LoginState.DatabaseUnavailable)
                SetState(LoginState.Initial);
        }
        else
        {
            SetDbStatus("غير متصل بقاعدة البيانات", DesignTokens.Colors.Error);
            SetState(LoginState.DatabaseUnavailable);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _usernameTextBox.Focus();
    }

    private static POS.Domain.Interfaces.ISoundService? _soundService;

    private static void PlaySound(SoundEvent soundEvent)
    {
        _soundService ??= AppServiceProvider.Provider?.GetService<POS.Domain.Interfaces.ISoundService>();
        _soundService?.Play(soundEvent);
    }
}
