using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using POS.Application.DTOs;
using POS.Application.Services;

namespace POS.Desktop.Forms;

/// <summary>
/// USER-001: User management UserControl.
/// Top: add user button. Middle: DataGridView (Username, DisplayName, Role, Status, LastLogin, Actions).
/// Add/Edit user dialog with fields: Username, Password, DisplayName, Role (combo), Permissions (checked list).
/// Toggle active/lock.
/// </summary>
public class UserManagementForm : UserControl
{
    private enum UserMgmtState
    {
        Loading,
        Loaded,
        Empty,
        Error,
        PermissionDenied
    }

    private readonly IUserService? _userService;
    private UserMgmtState _currentState = UserMgmtState.Loading;
    private List<UserDto> _users = new();
    private List<string> _allPermissions = new();

    // UI Controls
    private Panel _toolbarPanel;
    private Button _addUserButton;
    private Button _refreshButton;
    private DataGridView _usersGrid;
    private Panel _loadingPanel;
    private Panel _emptyPanel;
    private Panel _errorPanel;
    private Panel _permissionPanel;

    // Events
    public event EventHandler? AddUserRequested;
    public event EventHandler<UserDto>? EditUserRequested;

    public UserManagementForm()
    {
        InitializeComponent();
        SetState(UserMgmtState.Loading);
    }

    public UserManagementForm(IUserService userService) : this()
    {
        _userService = userService;
    }

    private void InitializeComponent()
    {
        RightToLeft = RightToLeft.Yes;
        BackColor = DesignTokens.BackgroundColor;
        Font = DesignTokens.DefaultFont;
        Dock = DockStyle.Fill;

        // Toolbar
        _toolbarPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = DesignTokens.SurfaceColor,
            Padding = new Padding(DesignTokens.SpacingSM)
        };

        _addUserButton = new Button
        {
            Text = "➕ إضافة مستخدم",
            Font = DesignTokens.ButtonFont,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 32),
            Dock = DockStyle.Right,
            BackColor = DesignTokens.SuccessColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        _addUserButton.Click += (s, e) => AddUserRequested?.Invoke(this, EventArgs.Empty);

        _refreshButton = new Button
        {
            Text = "🔄 تحديث",
            Font = DesignTokens.DefaultFont,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(90, 32),
            Dock = DockStyle.Left,
            BackColor = DesignTokens.PrimaryColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };
        _refreshButton.Click += async (s, e) => await LoadDataAsync();

        _toolbarPanel.Controls.Add(_addUserButton);
        _toolbarPanel.Controls.Add(_refreshButton);

        // Users DataGridView
        _usersGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            RowHeadersVisible = false,
            BackgroundColor = DesignTokens.SurfaceColor,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = DesignTokens.BorderColor,
            RightToLeft = RightToLeft.Yes,
            Font = DesignTokens.DataFont,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _usersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "اسم المستخدم", Name = "Username", FillWeight = 15 });
        _usersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الاسم المعروض", Name = "DisplayName", FillWeight = 18 });
        _usersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الدور", Name = "Role", FillWeight = 12 });
        _usersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "الحالة", Name = "Status", FillWeight = 10 });
        _usersGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "آخر دخول", Name = "LastLogin", FillWeight = 20 });
        _usersGrid.Columns.Add(new DataGridViewButtonColumn { HeaderText = "إجراءات", Name = "Actions", FillWeight = 15, Text = "إجراءات", UseColumnTextForButtonValue = true });

        _usersGrid.CellClick += UsersGrid_CellClick;
        _usersGrid.CellFormatting += UsersGrid_CellFormatting;

        // Overlay panels
        _loadingPanel = CreateOverlay("جاري تحميل المستخدمين...");
        _emptyPanel = CreateOverlay("لا يوجد مستخدمون");
        _emptyPanel.Visible = false;

        _errorPanel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor, Visible = false };
        var errLbl = new Label { Text = "حدث خطأ أثناء تحميل المستخدمين", Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.ErrorColor, Dock = DockStyle.Top, Height = 40, TextAlign = ContentAlignment.MiddleCenter };
        var retryBtn = new Button { Text = "إعادة المحاولة", Font = DesignTokens.ButtonFont, BackColor = DesignTokens.PrimaryColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Size = new Size(150, 40), Cursor = Cursors.Hand, Anchor = AnchorStyles.None };
        retryBtn.Click += async (s, e) => await LoadDataAsync();
        _errorPanel.Controls.Add(retryBtn);
        _errorPanel.Controls.Add(errLbl);

        _permissionPanel = CreateOverlay("ليس لديك صلاحية لإدارة المستخدمين");
        _permissionPanel.Visible = false;

        Controls.Add(_loadingPanel);
        Controls.Add(_emptyPanel);
        Controls.Add(_errorPanel);
        Controls.Add(_permissionPanel);
        Controls.Add(_usersGrid);
        Controls.Add(_toolbarPanel);
    }

    private Panel CreateOverlay(string text)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = DesignTokens.BackgroundColor };
        panel.Controls.Add(new Label { Text = text, Font = DesignTokens.SubheadingFont, ForeColor = DesignTokens.TextSecondaryColor, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill });
        return panel;
    }

    private void SetState(UserMgmtState state)
    {
        _currentState = state;
        _loadingPanel.Visible = state == UserMgmtState.Loading;
        _emptyPanel.Visible = state == UserMgmtState.Empty;
        _errorPanel.Visible = state == UserMgmtState.Error;
        _permissionPanel.Visible = state == UserMgmtState.PermissionDenied;
        _usersGrid.Visible = state == UserMgmtState.Loaded;
        _addUserButton.Enabled = state == UserMgmtState.Loaded;
    }

    public async Task LoadDataAsync()
    {
        SetState(UserMgmtState.Loading);

        try
        {
            if (_userService != null)
            {
                var usersTask = _userService.GetUsersAsync();
                var permsTask = _userService.GetAllPermissionsAsync();
                await Task.WhenAll(usersTask, permsTask);
                _users = await usersTask;
                _allPermissions = await permsTask;
            }
            else
            {
                await Task.Delay(500);
                LoadSampleUsers();
                _allPermissions = new List<string> { "Sell", "ApplyDiscount", "ViewReports", "EditProducts", "AdjustInventory", "ManageUsers", "ChangeSettings", "Backup", "ManageTables" };
            }

            PopulateGrid();
            SetState(_users.Count > 0 ? UserMgmtState.Loaded : UserMgmtState.Empty);
        }
        catch (UnauthorizedAccessException)
        {
            SetState(UserMgmtState.PermissionDenied);
        }
        catch
        {
            SetState(UserMgmtState.Error);
        }
    }

    private void LoadSampleUsers()
    {
        _users = new List<UserDto>
        {
            new UserDto(Guid.NewGuid(), "admin", "المدير العام", "Admin", true, false, DateTime.Now.AddHours(-2)),
            new UserDto(Guid.NewGuid(), "cashier1", "أحمد محمد", "Cashier", true, false, DateTime.Now.AddMinutes(-30)),
            new UserDto(Guid.NewGuid(), "cashier2", "سارة علي", "Cashier", true, false, DateTime.Now.AddDays(-1)),
            new UserDto(Guid.NewGuid(), "stock", "خالد حسن", "StockKeeper", true, false, DateTime.Now.AddHours(-5)),
            new UserDto(Guid.NewGuid(), "disabled_user", "محمد سعيد", "Cashier", false, false, null)
        };
    }

    private void PopulateGrid()
    {
        _usersGrid.Rows.Clear();
        foreach (var user in _users)
        {
            var status = !user.IsActive ? "معطل" : user.IsLocked ? "مقفل" : "نشط";
            var lastLogin = user.LastLoginAt?.ToString("yyyy/MM/dd HH:mm") ?? "لم يسجل دخول";
            var role = user.Role switch { "Admin" => "مدير", "Cashier" => "كاشير", "StockKeeper" => "أمين مخزن", _ => user.Role };
            _usersGrid.Rows.Add(user.Username, user.DisplayName, role, status, lastLogin, "إجراءات");
            _usersGrid.Rows[_usersGrid.Rows.Count - 1].Tag = user;
        }
    }

    private void UsersGrid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_usersGrid.Columns[e.ColumnIndex].Name == "Status")
        {
            var text = e.Value?.ToString() ?? "";
            e.CellStyle.ForeColor = text switch
            {
                "نشط" => DesignTokens.SuccessColor,
                "مقفل" => DesignTokens.ErrorColor,
                "معطل" => DesignTokens.DisabledColor,
                _ => DesignTokens.TextPrimaryColor
            };
        }
    }

    private void UsersGrid_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        if (_usersGrid.Columns[e.ColumnIndex].Name != "Actions") return;

        var user = _usersGrid.Rows[e.RowIndex].Tag as UserDto;
        if (user == null) return;

        var menu = new ContextMenuStrip { RightToLeft = RightToLeft.Yes };

        var editItem = new ToolStripMenuItem("✏️ تعديل");
        editItem.Click += (s, e) => EditUserRequested?.Invoke(this, user);
        menu.Items.Add(editItem);

        var toggleItem = new ToolStripMenuItem(user.IsActive ? "🚫 تعطيل" : "✅ تفعيل");
        toggleItem.Click += async (s, e) =>
        {
            if (_userService != null)
                await _userService.ToggleUserStatusAsync(user.Id, !user.IsActive);
            _ = LoadDataAsync();
        };
        menu.Items.Add(toggleItem);

        if (user.IsLocked)
        {
            var unlockItem = new ToolStripMenuItem("🔓 فتح القفل");
            unlockItem.Click += async (s, e) =>
            {
                if (_userService != null)
                    await _userService.UnlockUserAsync(user.Id);
                _ = LoadDataAsync();
            };
            menu.Items.Add(unlockItem);
        }

        var cellRect = _usersGrid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, true);
        menu.Show(_usersGrid, cellRect.Left, cellRect.Bottom);
    }
}