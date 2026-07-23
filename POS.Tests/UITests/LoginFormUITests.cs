using System.Windows.Forms;
using Moq;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Forms;
using POS.Domain.Enums;
using Xunit;

namespace POS.Tests.UITests;

/// <summary>
/// End-to-end UI tests for LoginForm (AUTH-001).
/// Tests all states: Initial, Loading, InvalidCredentials, LockedUser,
/// DisabledUser, DatabaseUnavailable, and Success.
/// </summary>
public sealed class LoginFormUITests : IDisposable
{
    private readonly Mock<IAuthService> _mockAuth;
    private readonly FormTestHost<LoginForm> _host;

    public LoginFormUITests()
    {
        _mockAuth = new Mock<IAuthService>(MockBehavior.Strict);

        // Default: database is reachable. Must be set up BEFORE the host creates
        // and shows the form, because the Load event triggers the connection check.
        _mockAuth.Setup(a => a.CheckDatabaseConnectionAsync()).ReturnsAsync(true);

        _host = new FormTestHost<LoginForm>(_mockAuth.Object);
        Thread.Sleep(100);
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public void InitialState_ShowsArabicTitle()
    {
        _host.InvokeOnUI(() =>
            Assert.Equal("تسجيل الدخول - نظام نقاط البيع", _host.Control.Text));
    }

    [Fact]
    public void InitialState_LoginButtonIsEnabled() =>
        Assert.True(_host.IsEnabled("_loginButton"));

    [Fact]
    public void InitialState_LoginButtonShowsCorrectText() =>
        Assert.Contains("تسجيل الدخول", _host.GetText("_loginButton"));

    [Fact]
    public void InitialState_ErrorLabelIsHidden() =>
        Assert.False(_host.IsVisible("_errorMessagePanel"));

    [Fact]
    public void InitialState_PasswordFieldIsEnabled() =>
        Assert.True(_host.IsEnabled("_passwordTextBox"));

    [Fact]
    public void InitialState_UsernameFieldIsEnabled() =>
        Assert.True(_host.IsEnabled("_usernameTextBox"));

    [Fact]
    public void InitialState_FormHasRtlLayout()
    {
        _host.InvokeOnUI(() =>
        {
            Assert.Equal(RightToLeft.Yes, _host.Control.RightToLeft);
            Assert.True(_host.Control.RightToLeftLayout);
        });
    }

    [Fact]
    public void InitialState_BusinessNameDefaultsToPlaceholder()
    {
        _host.InvokeOnUI(() =>
        {
            var lbl = _host.GetField<Label>("_businessNameLabel");
            Assert.Equal("اسم المنشأة", lbl.Text);
        });
    }

    [Fact]
    public void Load_ChecksDatabaseConnection()
    {
        Thread.Sleep(800);
        _host.InvokeOnUI(() =>
        {
            var lbl = _host.GetField<Label>("_dbStatusLabel");
            Assert.Contains("متصل", lbl.Text);
        });
    }

    [Fact]
    public void SetDatabaseConnected_UpdatesStatus()
    {
        _host.InvokeOnUI(() => _host.Control.SetDatabaseConnected(true));
        Thread.Sleep(200);
        Assert.Contains("متصل", _host.GetText("_dbStatusLabel"));
    }

    [Fact]
    public void SetDatabaseDisconnected_ShowsUnavailableState()
    {
        _host.InvokeOnUI(() => _host.Control.SetDatabaseConnected(false));
        Thread.Sleep(200);
        Assert.False(_host.IsEnabled("_loginButton"));
        Assert.Contains("غير متصل", _host.GetText("_dbStatusLabel"));
    }

    [Fact]
    public void LoginWithEmptyUsername_ShowsValidationError()
    {
        _host.ClickButton("_loginButton");
        Thread.Sleep(200);
        Assert.True(_host.IsVisible("_errorMessagePanel"));
        Assert.Contains("اسم المستخدم", _host.GetText("_errorMessageLabel"));
    }

    [Fact]
    public async Task LoginWithBadCredentials_ShowsInvalidState()
    {
        _mockAuth.Setup(a => a.LoginAsync("baduser", "badpass"))
            .ReturnsAsync(new POS.Domain.Interfaces.AuthResult { Success = false, Message = "Invalid credentials" });

        _host.SetTextBox("_usernameTextBox", "baduser");
        _host.SetTextBox("_passwordTextBox", "badpass");
        _host.ClickButton("_loginButton");
        await Task.Delay(1500);

        _host.InvokeOnUI(() =>
        {
            Assert.True(_host.IsVisible("_errorMessagePanel"));
            Assert.Contains("غير صحيحة", _host.GetText("_errorMessageLabel"));
        });
    }

    [Fact]
    public async Task LoginWithLockedUser_ShowsGenericError()
    {
        _mockAuth.Setup(a => a.LoginAsync("locked", "pass"))
            .ReturnsAsync(new POS.Domain.Interfaces.AuthResult { Success = false, Message = "الحساب مقفول" });

        _host.SetTextBox("_usernameTextBox", "locked");
        _host.SetTextBox("_passwordTextBox", "pass");
        _host.ClickButton("_loginButton");
        await Task.Delay(1500);

        _host.InvokeOnUI(() =>
        {
            Assert.True(_host.IsVisible("_errorMessagePanel"));
            var text = _host.GetText("_errorMessageLabel");
            Assert.Contains("غير صحيحة", text);
        });
    }

    [Fact]
    public async Task LoginWithDisabledUser_ShowsDisabledState()
    {
        _mockAuth.Setup(a => a.LoginAsync("disabled", "pass"))
            .ReturnsAsync(new POS.Domain.Interfaces.AuthResult { Success = false, Message = "الحساب معطل" });

        _host.SetTextBox("_usernameTextBox", "disabled");
        _host.SetTextBox("_passwordTextBox", "pass");
        _host.ClickButton("_loginButton");
        await Task.Delay(1500);

        _host.InvokeOnUI(() =>
        {
            Assert.True(_host.IsVisible("_errorMessagePanel"));
            Assert.Contains("معطل", _host.GetText("_errorMessageLabel"));
        });
    }

    [Fact]
    public async Task SuccessfulLogin_FiresLoginSuccessfulEvent()
    {
        var userEntity = new POS.Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            FullName = "المدير",
            Role = UserRole.Admin,
            MustChangePassword = false
        };

        _mockAuth.Setup(a => a.LoginAsync("admin", "password"))
            .ReturnsAsync(new POS.Domain.Interfaces.AuthResult { Success = true, Message = "OK", User = userEntity });

        _host.SetTextBox("_usernameTextBox", "admin");
        _host.SetTextBox("_passwordTextBox", "password");

        var eventTask = _host.AwaitEvent<LoginResponse>("LoginSuccessful");
        _host.ClickButton("_loginButton");

        var response = await eventTask;
        Assert.NotNull(response);
        Assert.Equal("المدير", response.DisplayName);
        Assert.Equal("Admin", response.Role);
    }

    [Fact]
    public void SetBusinessName_UpdatesLabel()
    {
        _host.InvokeOnUI(() => _host.Control.SetBusinessName("مطعم الأندلس"));
        _host.InvokeOnUI(() =>
        {
            var lbl = _host.GetField<Label>("_businessNameLabel");
            Assert.Equal("مطعم الأندلس", lbl.Text);
        });
    }

    [Fact]
    public void SetAppVersion_UpdatesVersionLabel()
    {
        _host.InvokeOnUI(() => _host.Control.SetAppVersion("2.0.0"));
        Assert.Contains("2.0.0", _host.GetText("_versionLabel"));
    }

    [Fact]
    public async Task LoginThrowsException_ShowsDatabaseUnavailable()
    {
        _mockAuth.Setup(a => a.LoginAsync("admin", "pass"))
            .ThrowsAsync(new InvalidOperationException("Connection failed"));

        _host.SetTextBox("_usernameTextBox", "admin");
        _host.SetTextBox("_passwordTextBox", "pass");
        _host.ClickButton("_loginButton");
        await Task.Delay(1500);

        Assert.False(_host.IsEnabled("_loginButton"));
        Assert.True(_host.IsVisible("_errorMessagePanel"));
        Assert.Contains("قاعدة البيانات", _host.GetText("_errorMessageLabel"));
    }
}
