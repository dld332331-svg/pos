using Moq;
using POS.Application.DTOs;
using POS.Application.Services;
using POS.Desktop.Forms;
using Xunit;

namespace POS.Tests.UITests;

/// <summary>
/// UI tests for DashboardForm (DASH-001) covering all states:
/// Loading, Loaded, Empty, Error, and widget rendering.
/// All async UI calls are marshaled via InvokeAsync to avoid cross-thread exceptions.
/// </summary>
public sealed class DashboardFormUITests : IDisposable
{
    private readonly Mock<IDashboardService> _mockDashboard;
    private readonly FormTestHost<DashboardForm> _host;
    private static readonly Guid UserId = Guid.NewGuid();

    public DashboardFormUITests()
    {
        _mockDashboard = new Mock<IDashboardService>(MockBehavior.Strict);

        // Default: no recent transactions. Tests that call LoadDataAsync with widget
        // data must also set up GetRecentTransactionsAsync if they expect the grid to load.
        _mockDashboard
            .Setup(d => d.GetRecentTransactionsAsync(It.IsAny<int>()))
            .ReturnsAsync(new List<RecentTransactionDto>());

        _host = new FormTestHost<DashboardForm>(_mockDashboard.Object, UserId);
    }

    public void Dispose() => _host.Dispose();

    // ========================================================================
    // Initial State — Loading
    // ========================================================================

    [Fact]
    public void InitialState_ShowsArabicTitle()
    {
        _host.InvokeOnUI(() =>
        {
            var title = _host.GetField<Label>("_titleLabel");
            Assert.Equal("لوحة التحكم", title.Text);
        });
    }

    [Fact]
    public void InitialState_LoadingPanelIsVisible()
    {
        Assert.True(_host.IsVisible("_loadingPanel"));
    }

    [Fact]
    public void InitialState_EmptyPanelIsHidden()
    {
        Assert.False(_host.IsVisible("_emptyPanel"));
    }

    [Fact]
    public void InitialState_WidgetsPanelIsHidden()
    {
        Assert.False(_host.IsVisible("_widgetsPanel"));
    }

    [Fact]
    public void InitialState_ErrorPanelIsHidden()
    {
        Assert.False(_host.IsVisible("_errorPanel"));
    }

    [Fact]
    public void InitialState_HasRtlLayout()
    {
        _host.InvokeOnUI(() =>
        {
            Assert.Equal(RightToLeft.Yes, _host.Control.RightToLeft);
        });
    }

    // ========================================================================
    // Loaded State (Service returns widgets)
    // ========================================================================

    [Fact]
    public async Task LoadDataAsync_WithWidgets_ShowsLoadedState()
    {
        var widgets = new List<DashboardWidgetDto>
        {
            new("SalesTotal", "إجمالي المبيعات", "4500.000", "مبيعات اليوم", false),
            new("ActiveShift", "الوردية الحالية", "نشطة", "بدأت الساعة 08:00", false),
            new("LowStock", "تنبيهات المخزون", "5", "منتجات تحت الحد الأدنى", true)
        };

        _mockDashboard
            .Setup(d => d.GetWidgetsAsync(UserId))
            .ReturnsAsync(widgets);

        await _host.InvokeAsync(() => _host.Control.LoadDataAsync());

        Assert.True(_host.IsVisible("_widgetsPanel"));
        Assert.True(_host.IsVisible("_recentTransactionsPanel"));
        Assert.False(_host.IsVisible("_loadingPanel"));
        Assert.False(_host.IsVisible("_emptyPanel"));
        Assert.False(_host.IsVisible("_errorPanel"));
    }

    [Fact]
    public async Task LoadDataAsync_WithWidgets_PopulatesWidgetCards()
    {
        var widgets = new List<DashboardWidgetDto>
        {
            new("SalesTotal", "إجمالي المبيعات", "4500.000", "مبيعات اليوم", false),
            new("LowStock", "تنبيهات المخزون", "5", "منتجات تحت الحد الأدنى", true)
        };

        _mockDashboard
            .Setup(d => d.GetWidgetsAsync(UserId))
            .ReturnsAsync(widgets);

        await _host.InvokeAsync(() => _host.Control.LoadDataAsync());

        _host.InvokeOnUI(() =>
        {
            var widgetsPanel = _host.GetField<FlowLayoutPanel>("_widgetsPanel");
            Assert.Equal(2, widgetsPanel.Controls.Count);
        });
    }

    [Fact]
    public async Task LoadDataAsync_RefreshButtonUpdatesTimestamp()
    {
        var widgets = new List<DashboardWidgetDto>
        {
            new("SalesTotal", "إجمالي المبيعات", "4500.000", "", false),
            new("ActiveShift", "الوردية الحالية", "نشطة", "", false),
            new("LowStock", "تنبيهات المخزون", "5", "", true)
        };

        _mockDashboard
            .Setup(d => d.GetWidgetsAsync(UserId))
            .ReturnsAsync(widgets);

        await _host.InvokeAsync(() => _host.Control.LoadDataAsync());

        _host.InvokeOnUI(() =>
        {
            var refreshLabel = _host.GetField<Label>("_lastRefreshLabel");
            Assert.Contains("آخر تحديث", refreshLabel.Text);
        });
    }

    // ========================================================================
    // Empty State (Service returns empty list)
    // ========================================================================

    [Fact]
    public async Task LoadDataAsync_WithNullWidgets_ShowsEmptyState()
    {
        _mockDashboard
            .Setup(d => d.GetWidgetsAsync(UserId))
            .ReturnsAsync((List<DashboardWidgetDto>?)null!);

        await _host.InvokeAsync(() => _host.Control.LoadDataAsync());

        Assert.True(_host.IsVisible("_emptyPanel"));
        Assert.False(_host.IsVisible("_widgetsPanel"));
        Assert.False(_host.IsVisible("_loadingPanel"));
    }

    [Fact]
    public async Task LoadDataAsync_WithEmptyWidgets_ShowsEmptyState()
    {
        _mockDashboard
            .Setup(d => d.GetWidgetsAsync(UserId))
            .ReturnsAsync(new List<DashboardWidgetDto>());

        await _host.InvokeAsync(() => _host.Control.LoadDataAsync());

        Assert.True(_host.IsVisible("_emptyPanel"));
        Assert.False(_host.IsVisible("_widgetsPanel"));
    }

    // ========================================================================
    // Error State
    // ========================================================================

    [Fact]
    public async Task LoadDataAsync_OnException_ShowsErrorState()
    {
        _mockDashboard
            .Setup(d => d.GetWidgetsAsync(UserId))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        await _host.InvokeAsync(() => _host.Control.LoadDataAsync());

        Assert.True(_host.IsVisible("_errorPanel"));
        Assert.False(_host.IsVisible("_loadingPanel"));
        Assert.False(_host.IsVisible("_emptyPanel"));
    }

    // ========================================================================
    // Permission Denied
    // ========================================================================

    [Fact]
    public async Task LoadDataAsync_OnUnauthorized_ShowsPermissionDenied()
    {
        _mockDashboard
            .Setup(d => d.GetWidgetsAsync(UserId))
            .ThrowsAsync(new UnauthorizedAccessException("No permission"));

        await _host.InvokeAsync(() => _host.Control.LoadDataAsync());

        Assert.True(_host.IsVisible("_permissionPanel"));
        Assert.False(_host.IsVisible("_errorPanel"));
    }

    // ========================================================================
    // Retry Button
    // ========================================================================

    [Fact]
    public void ErrorPanel_RetryButtonExists()
    {
        _host.InvokeOnUI(() =>
        {
            var retryBtn = _host.GetField<Button>("_retryButton");
            Assert.NotNull(retryBtn);
            Assert.Contains("محاولة", retryBtn.Text);
        });
    }
}
