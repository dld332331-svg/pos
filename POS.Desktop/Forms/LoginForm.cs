using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;

namespace POS.Desktop.Forms;

/// <summary>
/// AUTH-001: Login form with RTL layout, Arabic text, and multiple states.
/// States: Initial, Loading, InvalidCredentials, LockedUser, DisabledUser, DatabaseUnavailable
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
    private Panel _mainPanel;
    private Panel _logoPanel;
    private PictureBox _logoPictureBox;
    private Label _appNameLabel;
    private Label _businessNameLabel;
    private Label _usernameLabel;
    private ComboBox _usernameComboBox;
    private TextBox _usernameTextBox;
    private Label _passwordLabel;
    private Panel _passwordPanel;
    private TextBox _passwordTextBox;
    private Button _togglePasswordButton;
    private Button _loginButton;
    private Panel _dbStatusPanel;
    private Label _dbStatusLabel;
    private PictureBox _dbStatusIcon;
    private Label _versionLabel;
    private Label _errorMessageLabel;
    private Panel _loadingPanel;
    private PictureBox _loadingSpinner;

    // Events
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
        // Form setup
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Text = "تسجيل الدخول - نظام نقاط البيع";
        ClientSize = new Size(480, 640);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = DesignTokens.BackgroundColor;
        Font = DesignTokens.DefaultFont;
        AcceptButton = _loginButton;
        CancelButton = new Button { Visible = false };

        // Main centered panel
        _mainPanel = new Panel
        {
            Size = new Size(400, 580),
            Location = new Point(40, 30),
            BackColor = DesignTokens.SurfaceColor,
            BorderStyle = BorderStyle.FixedSingle
        };

        // Logo placeholder
        _logoPanel = new Panel
        {
            Size = new Size(80, 80),
            Location = new Point(160, 30),
            BackColor = DesignTokens.PrimaryColor,
            BorderStyle = BorderStyle.FixedSingle
        };

        _logoPictureBox = new PictureBox
        {
            Size = new Size(60, 60),
            Location = new Point(170, 40),
            BackColor = Color.Transparent,
            SizeMode = PictureBoxSizeMode.StretchImage
        };

        // App name
        _appNameLabel = new Label
        {
            Text = "نظام نقاط البيع",
            Font = DesignTokens.HeadingFont,
            ForeColor = DesignTokens.PrimaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(50, 125),
            Size = new Size(300, 35)
        };

        // Business name
        _businessNameLabel = new Label
        {
            Text = "اسم المنشأة",
            Font = DesignTokens.SubheadingFont,
            ForeColor = DesignTokens.TextSecondaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(50, 160),
            Size = new Size(300, 25)
        };

        // Username label
        _usernameLabel = new Label
        {
            Text = "اسم المستخدم",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(50, 210),
            Size = new Size(300, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        // Username combo
        _usernameComboBox = new ComboBox
        {
            Location = new Point(50, 235),
            Size = new Size(300, 30),
            Font = DesignTokens.DefaultFont,
            DropDownStyle = ComboBoxStyle.DropDownList,
            RightToLeft = RightToLeft.Yes,
            Visible = false
        };

        // Username textbox (alternative)
        _usernameTextBox = new TextBox
        {
            Location = new Point(50, 235),
            Size = new Size(300, 30),
            Font = DesignTokens.DefaultFont,
            RightToLeft = RightToLeft.Yes,
            PlaceholderText = "أدخل اسم المستخدم"
        };
        _usernameTextBox.Visible = true;

        // Password label
        _passwordLabel = new Label
        {
            Text = "كلمة المرور",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            Location = new Point(50, 280),
            Size = new Size(300, 22),
            TextAlign = ContentAlignment.MiddleRight
        };

        // Password panel (contains textbox + toggle button)
        _passwordPanel = new Panel
        {
            Location = new Point(50, 305),
            Size = new Size(300, 30),
            BackColor = DesignTokens.Colors.Surface,
            BorderStyle = BorderStyle.FixedSingle
        };

        _passwordTextBox = new TextBox
        {
            Location = new Point(0, 0),
            Size = new Size(260, 28),
            Font = DesignTokens.DefaultFont,
            PasswordChar = '●',
            RightToLeft = RightToLeft.Yes,
            BorderStyle = BorderStyle.None,
            PlaceholderText = "أدخل كلمة المرور"
        };

        _togglePasswordButton = new Button
        {
            Text = "👁",
            Location = new Point(262, 0),
            Size = new Size(36, 28),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Font = DesignTokens.Typography.Body,
        };

        // Login button
        _loginButton = new Button
        {
            Text = "تسجيل الدخول",
            Font = DesignTokens.ButtonFont,
            ForeColor = Color.White,
            BackColor = DesignTokens.PrimaryColor,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(50, 355),
            Size = new Size(300, 42),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter
        };

        // Error message label
        _errorMessageLabel = new Label
        {
            Text = "",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 9f),
            ForeColor = DesignTokens.ErrorColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(50, 405),
            Size = new Size(300, 40),
            Visible = false
        };

        // Database status indicator
        _dbStatusPanel = new Panel
        {
            Location = new Point(50, 450),
            Size = new Size(300, 25)
        };

        _dbStatusIcon = new PictureBox
        {
            Size = new Size(14, 14),
            Location = new Point(278, 5),
            SizeMode = PictureBoxSizeMode.StretchImage,
            BackColor = Color.Transparent
        };

        _dbStatusLabel = new Label
        {
            Text = "جاري التحقق من قاعدة البيانات...",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 8f),
            ForeColor = DesignTokens.TextSecondaryColor,
            TextAlign = ContentAlignment.MiddleRight,
            Location = new Point(0, 2),
            Size = new Size(270, 20)
        };

        // Loading overlay
        _loadingPanel = new Panel
        {
            Size = new Size(400, 580),
            Location = new Point(0, 0),
            BackColor = Color.FromArgb(200, DesignTokens.SurfaceColor),
            Visible = false,
            Dock = DockStyle.Fill
        };

        _loadingSpinner = new PictureBox
        {
            Size = new Size(48, 48),
            Location = new Point(176, 220),
            SizeMode = PictureBoxSizeMode.CenterImage
        };

        var loadingLabel = new Label
        {
            Text = "جاري تسجيل الدخول...",
            Font = DesignTokens.DefaultFont,
            ForeColor = DesignTokens.TextPrimaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(100, 275),
            Size = new Size(200, 25)
        };

        // Version label
        _versionLabel = new Label
        {
            Text = "الإصدار 1.0.0",
            Font = new Font(DesignTokens.DefaultFont.FontFamily, 8f),
            ForeColor = DesignTokens.TextSecondaryColor,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(50, 490),
            Size = new Size(300, 20)
        };

        // Assemble panels
        _passwordPanel.Controls.Add(_passwordTextBox);
        _passwordPanel.Controls.Add(_togglePasswordButton);
        _dbStatusPanel.Controls.Add(_dbStatusIcon);
        _dbStatusPanel.Controls.Add(_dbStatusLabel);
        _loadingPanel.Controls.Add(_loadingSpinner);
        _loadingPanel.Controls.Add(loadingLabel);

        _mainPanel.Controls.Add(_logoPanel);
        _mainPanel.Controls.Add(_logoPictureBox);
        _mainPanel.Controls.Add(_appNameLabel);
        _mainPanel.Controls.Add(_businessNameLabel);
        _mainPanel.Controls.Add(_usernameLabel);
        _mainPanel.Controls.Add(_usernameComboBox);
        _mainPanel.Controls.Add(_usernameTextBox);
        _mainPanel.Controls.Add(_passwordLabel);
        _mainPanel.Controls.Add(_passwordPanel);
        _mainPanel.Controls.Add(_loginButton);
        _mainPanel.Controls.Add(_errorMessageLabel);
        _mainPanel.Controls.Add(_dbStatusPanel);
        _mainPanel.Controls.Add(_versionLabel);
        _mainPanel.Controls.Add(_loadingPanel);

        Controls.Add(_mainPanel);

        // Event handlers
        _loginButton.Click += async (s, e) => await AttemptLoginAsync();
        _togglePasswordButton.Click += TogglePasswordVisibility;
        _usernameComboBox.SelectedIndexChanged += (s, e) =>
        {
            _usernameTextBox.Text = _usernameComboBox.SelectedItem?.ToString() ?? "";
        };
        _passwordTextBox.KeyPress += (s, e) =>
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                e.Handled = true;
                _ = AttemptLoginAsync();
            }
        };
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };
        Load += async (s, e) => await CheckDatabaseConnectionAsync();
    }

    private async Task CheckDatabaseConnectionAsync()
    {
        SetDbStatus("جاري التحقق من قاعدة البيانات...", DesignTokens.Colors.Warning);

        try
        {
            if (_authService != null)
            {
                var connected = await _authService.CheckDatabaseConnectionAsync();
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

    private void TogglePasswordVisibility(object? sender, EventArgs e)
    {
        if (_passwordTextBox.PasswordChar == '\0')
        {
            _passwordTextBox.PasswordChar = '●';
            _togglePasswordButton.Text = "👁";
        }
        else
        {
            _passwordTextBox.PasswordChar = '\0';
            _togglePasswordButton.Text = "🔒";
        }
    }

    private void SetState(LoginState state)
    {
        _currentState = state;

        switch (state)
        {
            case LoginState.Initial:
                _errorMessageLabel.Visible = false;
                _loadingPanel.Visible = false;
                _loginButton.Enabled = true;
                _loginButton.Text = "تسجيل الدخول";
                _loginButton.BackColor = DesignTokens.PrimaryColor;
                _usernameComboBox.Enabled = true;
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                break;

            case LoginState.Loading:
                _errorMessageLabel.Visible = false;
                _loadingPanel.Visible = true;
                _loadingPanel.BringToFront();
                _loginButton.Enabled = false;
                _loginButton.Text = "جاري التسجيل...";
                _usernameComboBox.Enabled = false;
                _usernameTextBox.Enabled = false;
                _passwordTextBox.Enabled = false;
                break;

            case LoginState.InvalidCredentials:
                _errorMessageLabel.Visible = true;
                _errorMessageLabel.Text = "اسم المستخدم أو كلمة المرور غير صحيحة";
                _errorMessageLabel.ForeColor = DesignTokens.ErrorColor;
                _loadingPanel.Visible = false;
                _loginButton.Enabled = true;
                _loginButton.Text = "تسجيل الدخول";
                _usernameComboBox.Enabled = true;
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                _passwordTextBox.Focus();
                _passwordTextBox.SelectAll();
                break;

            case LoginState.LockedUser:
                _errorMessageLabel.Visible = true;
                _errorMessageLabel.Text = "تم قفل هذا الحساب. يرجى التواصل مع المسؤول.";
                _errorMessageLabel.ForeColor = DesignTokens.WarningColor;
                _loadingPanel.Visible = false;
                _loginButton.Enabled = true;
                _loginButton.Text = "تسجيل الدخول";
                _usernameComboBox.Enabled = true;
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                break;

            case LoginState.DisabledUser:
                _errorMessageLabel.Visible = true;
                _errorMessageLabel.Text = "هذا الحساب معطل. يرجى التواصل مع المسؤول.";
                _errorMessageLabel.ForeColor = DesignTokens.WarningColor;
                _loadingPanel.Visible = false;
                _loginButton.Enabled = true;
                _loginButton.Text = "تسجيل الدخول";
                _usernameComboBox.Enabled = true;
                _usernameTextBox.Enabled = true;
                _passwordTextBox.Enabled = true;
                break;

            case LoginState.DatabaseUnavailable:
                _errorMessageLabel.Visible = true;
                _errorMessageLabel.Text = "لا يمكن الاتصال بقاعدة البيانات. تأكد من تشغيل الخادم.";
                _errorMessageLabel.ForeColor = DesignTokens.ErrorColor;
                _loadingPanel.Visible = false;
                _loginButton.Enabled = false;
                _loginButton.Text = "غير متاح";
                _loginButton.BackColor = DesignTokens.DisabledColor;
                SetDbStatus("غير متصل", DesignTokens.ErrorColor);
                break;
        }
    }

    private async Task AttemptLoginAsync()
    {
        var username = _usernameTextBox.Visible
            ? _usernameTextBox.Text.Trim()
            : _usernameComboBox.SelectedItem?.ToString()?.Trim() ?? "";

        var password = _passwordTextBox.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            _errorMessageLabel.Visible = true;
            _errorMessageLabel.Text = "يرجى إدخال اسم المستخدم";
            _usernameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            _errorMessageLabel.Visible = true;
            _errorMessageLabel.Text = "يرجى إدخال كلمة المرور";
            _passwordTextBox.Focus();
            return;
        }

        SetState(LoginState.Loading);

        try
        {
            if (_authService == null)
            {
                await Task.Delay(1500);
                PlaySound(SoundEvent.LoginSuccess);
                LoginSuccessful?.Invoke(this, new LoginResponse(
                    Guid.NewGuid(), "المدير", "Admin", false,
                    new List<string> { "Sell", "ViewDashboard", "ManageProducts" }));
                Close();
                return;
            }

            var result = await _authService.LoginAsync(username, password);

            if (result.Success && result.User != null)
            {
                PlaySound(SoundEvent.LoginSuccess);
                var response = new LoginResponse(
                    result.User.Id, result.User.FullName, result.User.Role.ToString(),
                    result.User.MustChangePassword, new List<string>());
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

    public void LoadUsernames(List<string> usernames)
    {
        _usernameComboBox.Items.Clear();
        foreach (var name in usernames)
            _usernameComboBox.Items.Add(name);

        if (usernames.Count > 0)
        {
            _usernameComboBox.Visible = true;
            _usernameTextBox.Visible = false;
        }
        else
        {
            _usernameComboBox.Visible = false;
            _usernameTextBox.Visible = true;
            _usernameTextBox.Focus();
        }
    }

    public void SetBusinessName(string name)
    {
        _businessNameLabel.Text = name;
    }

    public void SetAppVersion(string version)
    {
        _versionLabel.Text = $"الإصدار {version}";
    }

    public void SetDatabaseConnected(bool connected)
    {
        if (connected)
        {
            SetDbStatus("متصل بقاعدة البيانات", DesignTokens.SuccessColor);
            if (_currentState == LoginState.DatabaseUnavailable)
                SetState(LoginState.Initial);
        }
        else
        {
                    SetDbStatus("غير متصل بقاعدة البيانات", DesignTokens.ErrorColor);
            SetState(LoginState.DatabaseUnavailable);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_usernameTextBox.Visible)
            _usernameTextBox.Focus();
        else if (_usernameComboBox.Items.Count > 0)
            _usernameComboBox.SelectedIndex = 0;
    }

    private static void PlaySound(SoundEvent soundEvent)
    {
        var soundService = AppServiceProvider.Provider?.GetService<POS.Domain.Interfaces.ISoundService>();
        soundService?.Play(soundEvent);
    }
}