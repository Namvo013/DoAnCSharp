using System.Globalization;
using System.Text;
using LiMS.Admin.Models;
using LiMS.Admin.Services;

namespace LiMS.Admin.Forms;

public sealed class AdminMainForm : Form
{
    private static readonly Color CanvasColor = AdminTheme.Canvas;
    private static readonly Color SurfaceColor = AdminTheme.Surface;
    private static readonly Color SidebarColor = AdminTheme.Sidebar;
    private static readonly Color PrimaryColor = AdminTheme.Accent;
    private static readonly Color BorrowColor = Color.FromArgb(95, 137, 104);
    private static readonly Color ReturnColor = AdminTheme.Accent;
    private static readonly Color TextColor = AdminTheme.Text;
    private static readonly Color MutedColor = AdminTheme.Muted;

    private readonly AdminApiClient _api;
    private readonly LoginResponse _user;
    private readonly TabControl _tabs = new()
    {
        Dock = DockStyle.Fill,
        Appearance = TabAppearance.FlatButtons,
        ItemSize = new Size(0, 1),
        SizeMode = TabSizeMode.Fixed,
        Padding = new Point(0, 0)
    };
    private readonly DataGridView _booksGrid = CreateGrid();
    private readonly DataGridView _categoriesGrid = CreateGrid();
    private readonly DataGridView _copiesGrid = CreateGrid();
    private readonly DataGridView _readersGrid = CreateGrid();
    private readonly DataGridView _loansGrid = CreateGrid();
    private readonly DataGridView _finesGrid = CreateGrid();
    private readonly DataGridView _proposalsGrid = CreateGrid();
    private readonly DataGridView _settingsGrid = CreateGrid();
    private readonly DataGridView _logsGrid = CreateGrid();
    private readonly DataGridView _databaseBackupsGrid = CreateGrid();
    private string _bookSearch = "";
    private string _readerSearch = "";
    private readonly ComboBox _readerStatusFilter = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 165,
        Height = 34,
        Font = new Font("Segoe UI", 9.5F),
        Margin = new Padding(12, 3, 0, 3)
    };
    private readonly Dictionary<string, Label> _dashboardValues = [];
    private readonly Label _dashboardSummary = new();
    private readonly Label _popularBooks = new();
    private readonly Panel _monthlyReportChart = new() { BackColor = SurfaceColor };
    private readonly Label _backupSummary = new();
    private readonly Label _currentPageTitle = new();
    private readonly List<Button> _navigationButtons = [];
    private DashboardResponse? _latestDashboard;
    private DatabaseBackupStatusResponse? _databaseBackupStatus;
    private bool _isUpdatingReaderActivity;

    public AdminMainForm(AdminApiClient api, LoginResponse user)
    {
        _api = api;
        _user = user;
        FormClosed += (_, _) => _api.Dispose();
        Text = "LiMS - Quản trị thư viện";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1180, 740);
        WindowState = FormWindowState.Maximized;
        AdminTheme.StyleForm(this);
        _readerStatusFilter.BackColor = AdminTheme.SurfaceRaised;
        _readerStatusFilter.ForeColor = TextColor;
        _readerStatusFilter.FlatStyle = FlatStyle.Flat;

        BuildTabs();
        Controls.Add(CreateApplicationShell());
        _tabs.SelectedIndexChanged += async (_, _) =>
        {
            UpdateNavigation();
            if (_tabs.SelectedTab?.Tag is Func<Task> refresh)
                await RunAsync(refresh);
        };
        UpdateNavigation();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        await RunAsync(RefreshDashboardAsync);
    }

    private void BuildTabs()
    {
        AddDashboardPage();
        AddBooksPage();
        AddCategoriesPage();
        AddReadersPage();
        AddLoansPage();
        AddFinesPage();
        AddProposalsPage();
        if (_user.Role == "Admin")
        {
            AddSettingsPage();
            AddActivityLogsPage();
            AddDatabaseBackupsPage();
        }
    }

    private void AddDashboardPage()
    {
        var page = new TabPage("Tổng quan")
        {
            Tag = (Func<Task>)RefreshDashboardAsync,
            BackColor = CanvasColor
        };
        var layout = CreatePageLayout();
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        layout.Controls.Add(CreatePageHeader(
            "Tổng quan",
            "Tình hình hoạt động và các chỉ số chính của thư viện.",
            ("Xuất CSV", ExportDashboardReportAsync),
            ("Làm mới", RefreshDashboardAsync)), 0, 0);

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            Padding = new Padding(0, 8, 0, 12),
            BackColor = CanvasColor
        };
        for (var index = 0; index < 3; index++)
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        for (var index = 0; index < 2; index++)
            metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        metrics.Controls.Add(CreateMetricCard("TotalBooks", "Đầu sách", "Đầu sách trong thư viện"), 0, 0);
        metrics.Controls.Add(CreateMetricCard("AvailableCopies", "Sẵn sàng", "Bản sao có thể cho mượn"), 1, 0);
        metrics.Controls.Add(CreateMetricCard("ActiveReaders", "Độc giả hoạt động", "Thẻ độc giả còn hiệu lực"), 2, 0);
        metrics.Controls.Add(CreateMetricCard("ActiveLoans", "Đang mượn", "Phiếu mượn chưa trả"), 0, 1);
        metrics.Controls.Add(CreateMetricCard("OverdueLoans", "Quá hạn", "Cần được theo dõi"), 1, 1);
        metrics.Controls.Add(CreateMetricCard("UnpaidFines", "Phạt chưa thu", "Tổng số tiền còn phải thu"), 2, 1);
        page.Controls.Add(layout);

        var lower = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = CanvasColor,
            Padding = new Padding(0, 4, 0, 0)
        };
        for (var index = 0; index < 3; index++)
            lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333F));
        lower.Controls.Add(CreateInfoCard("TÓM TẮT THƯ VIỆN", _dashboardSummary), 0, 0);
        lower.Controls.Add(CreateInfoCard("ĐẦU SÁCH ĐƯỢC MƯỢN NHIỀU", _popularBooks), 1, 0);
        lower.Controls.Add(CreateInfoCard("BÁO CÁO 6 THÁNG GẦN NHẤT", _monthlyReportChart), 2, 0);
        _monthlyReportChart.Paint += PaintMonthlyReportChart;

        layout.Controls.Add(metrics, 0, 1);
        layout.Controls.Add(lower, 0, 2);
        _tabs.TabPages.Add(page);
    }

    private void AddBooksPage()
    {
        var page = CreateGridPage(
            "Kho sách",
            _booksGrid,
            LoadBooksAsync,
            ("Tìm đầu sách", SearchBooksAsync),
            ("Thêm đầu sách", CreateBookAsync),
            ("Sửa đầu sách", EditBookAsync),
            ("Thêm/cập nhật ảnh bìa", UploadBookCoverAsync),
            ("Ngừng kinh doanh", DeactivateBookAsync),
            ("Quản lý bản sao", ManageCopiesAsync),
            ("Thêm bản sao", AddCopiesAsync));
        _tabs.TabPages.Add(page);
    }

    private void AddCategoriesPage()
    {
        var page = CreateGridPage(
            "Thể loại sách",
            _categoriesGrid,
            LoadCategoriesAsync,
            ("Thêm thể loại", CreateCategoryAsync),
            ("Đổi tên", RenameCategoryAsync),
            ("Xóa thể loại trống", DeleteCategoryAsync));
        _tabs.TabPages.Add(page);
    }

    private void AddReadersPage()
    {
        _readersGrid.CellClick += ToggleReaderActivityAsync;

        var actions = new List<(string Title, Func<Task> Action)>
    {
        ("Tìm độc giả", SearchReadersAsync),
        ("Thêm độc giả", CreateReaderAsync),
        ("Cập nhật thẻ", UpdateReaderAsync),
        ("Khóa thẻ/tài khoản", DeactivateReaderAsync)
    };

        if (_user.Role == "Admin")
            actions.Add(("Đặt lại mật khẩu", ResetReaderPasswordAsync));

        _readerStatusFilter.Items.Clear();
        _readerStatusFilter.Items.AddRange(
            ["Tất cả trạng thái", "Đang hoạt động", "Ngừng hoạt động"]);

        _readerStatusFilter.SelectedIndex = 0;

        var page = CreateGridPage(
            "Độc giả",
            _readersGrid,
            LoadReadersAsync,
            actions.ToArray());

        var layout = page.Controls.OfType<TableLayoutPanel>().First();
        var toolbar = layout.GetControlFromPosition(0, 1) as FlowLayoutPanel;

        if (toolbar != null)
        {
            var filterLabel = new Label
            {
                Text = "Trạng thái thẻ:",
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F),
                ForeColor = TextColor,
                Margin = new Padding(12, 10, 5, 0)
            };

            toolbar.Controls.Add(filterLabel);
            toolbar.Controls.Add(_readerStatusFilter);
        }

        _readerStatusFilter.SelectedIndexChanged += async (_, _) =>
            await RunAsync(LoadReadersAsync);

        _tabs.TabPages.Add(page);
    }

    private void AddLoansPage()
    {
        var page = CreateGridPage(
            "Mượn - trả",
            _loansGrid,
            LoadLoansAsync,
            ("Cho mượn / quét barcode", CheckoutAsync),
            ("Nhận trả / quét barcode", ReturnAsync),
            ("Gia hạn phiếu chọn", RenewAsync));
        _tabs.TabPages.Add(page);
    }

    private void AddFinesPage()
    {
        var page = CreateGridPage(
            "Tiền phạt",
            _finesGrid,
            LoadFinesAsync,
            ("Ghi nhận thanh toán", CollectFineAsync));
        _tabs.TabPages.Add(page);
    }

    private void AddProposalsPage()
    {
        var page = CreateGridPage(
            "Đề xuất mua sách",
            _proposalsGrid,
            LoadProposalsAsync,
            ("Duyệt đề xuất", () => ReviewProposalAsync("Approved")),
            ("Từ chối đề xuất", () => ReviewProposalAsync("Rejected")));
        _tabs.TabPages.Add(page);
    }

    private void AddSettingsPage()
    {
        _tabs.TabPages.Add(CreateGridPage(
            "Cấu hình",
            _settingsGrid,
            LoadSettingsAsync,
            ("Sửa cấu hình", UpdateSettingAsync)));
    }

    private void AddActivityLogsPage() =>
        _tabs.TabPages.Add(CreateGridPage("Nhật ký hoạt động", _logsGrid, LoadActivityLogsAsync));

    private void AddDatabaseBackupsPage()
    {
        var page = new TabPage("Sao lưu & phục hồi")
        {
            Tag = (Func<Task>)LoadDatabaseBackupsAsync,
            BackColor = CanvasColor
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 16, 24, 22),
            BackColor = CanvasColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(CreatePageHeader("Sao lưu & phục hồi", GetPageDescription("Sao lưu & phục hồi")), 0, 0);
        layout.Controls.Add(CreateToolbar(
            ("Làm mới", LoadDatabaseBackupsAsync),
            ("Tạo bản sao lưu", CreateDatabaseBackupAsync),
            ("Phục hồi bản sao lưu", RestoreDatabaseBackupAsync)), 0, 1);

        _backupSummary.Dock = DockStyle.Fill;
        _backupSummary.AutoEllipsis = true;
        _backupSummary.ForeColor = MutedColor;
        _backupSummary.TextAlign = ContentAlignment.MiddleLeft;
        layout.Controls.Add(_backupSummary, 0, 2);

        var gridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = SurfaceColor
        };
        _databaseBackupsGrid.Dock = DockStyle.Fill;
        gridContainer.Controls.Add(_databaseBackupsGrid);
        layout.Controls.Add(gridContainer, 0, 3);
        page.Controls.Add(layout);
        _tabs.TabPages.Add(page);
    }

    private TabPage CreateGridPage(
        string title,
        DataGridView grid,
        Func<Task> refresh,
        params (string Title, Func<Task> Action)[] actions)
    {
        var page = new TabPage(title)
        {
            Tag = refresh,
            BackColor = CanvasColor
        };
        var layout = CreatePageLayout();
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(CreatePageHeader(title, GetPageDescription(title)), 0, 0);

        var toolbarActions = new (string Title, Func<Task> Action)[actions.Length + 1];
        toolbarActions[0] = ("Làm mới", refresh);
        Array.Copy(actions, 0, toolbarActions, 1, actions.Length);
        layout.Controls.Add(CreateToolbar(toolbarActions), 0, 1);

        var gridContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(1),
            BackColor = SurfaceColor
        };
        grid.Dock = DockStyle.Fill;
        gridContainer.Controls.Add(grid);
        layout.Controls.Add(gridContainer, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private TableLayoutPanel CreatePageLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(24, 16, 24, 22),
            BackColor = CanvasColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return layout;
    }

    private Panel CreatePageHeader(
        string title,
        string description,
        params (string Title, Func<Task> Action)[] actions)
    {
        var header = new Panel { Dock = DockStyle.Fill, BackColor = CanvasColor };
        var titleLabel = new Label
        {
            Text = title,
            AutoSize = true,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = TextColor,
            Location = new Point(0, 0)
        };
        var descriptionLabel = new Label
        {
            Text = description,
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F),
            ForeColor = MutedColor,
            Location = new Point(2, 47)
        };
        header.Controls.Add(descriptionLabel);
        header.Controls.Add(titleLabel);

        if (actions.Length > 0)
        {
            var actionPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = CanvasColor,
                Location = new Point(0, 8)
            };
            foreach (var pageAction in actions)
            {
                var button = CreateActionButton(pageAction.Title);
                button.Margin = new Padding(8, 0, 0, 0);
                button.Click += async (_, _) =>
                {
                    if (!button.Enabled)
                        return;
                    button.Enabled = false;
                    try
                    {
                        await RunAsync(pageAction.Action);
                    }
                    finally
                    {
                        button.Enabled = true;
                    }
                };
                actionPanel.Controls.Add(button);
            }
            header.Controls.Add(actionPanel);
            void PositionActions() =>
                actionPanel.Location = new Point(header.ClientSize.Width - actionPanel.Width, 8);
            header.Resize += (_, _) => PositionActions();
            actionPanel.SizeChanged += (_, _) => PositionActions();
            PositionActions();
        }

        return header;
    }

    private Panel CreateMetricCard(string key, string title, string hint)
    {
        var metricColor = key switch
        {
            "OverdueLoans" => Color.FromArgb(196, 74, 69),
            "UnpaidFines" => Color.FromArgb(190, 132, 47),
            "AvailableCopies" => Color.FromArgb(95, 137, 104),
            _ => PrimaryColor
        };
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Margin = new Padding(0, 0, 14, 14),
            Padding = Padding.Empty
        };
        var accent = new Panel
        {
            Location = new Point(18, 18),
            Size = new Size(4, 25),
            BackColor = metricColor
        };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(32, 13, 16, 14),
            BackColor = SurfaceColor
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 4));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty
        };
        var valueLabel = new Label
        {
            Text = "—",
            Dock = DockStyle.Fill,
            ForeColor = metricColor,
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty
        };
        var hintLabel = new Label
        {
            Text = hint,
            Dock = DockStyle.Fill,
            ForeColor = MutedColor,
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = true,
            Margin = Padding.Empty
        };
        var valueUnderline = new Panel
        {
            Dock = DockStyle.Left,
            Width = 34,
            Height = 4,
            BackColor = metricColor,
            Margin = Padding.Empty
        };
        _dashboardValues[key] = valueLabel;
        content.Controls.Add(titleLabel, 0, 0);
        content.Controls.Add(valueLabel, 0, 1);
        content.Controls.Add(valueUnderline, 0, 2);
        content.Controls.Add(hintLabel, 0, 3);
        card.Controls.Add(content);
        card.Controls.Add(accent);
        return card;
    }

    private static Panel CreateInfoCard(string title, Control content)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = SurfaceColor,
            Padding = new Padding(20),
            Margin = new Padding(0, 0, 14, 0)
        };
        var heading = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 28,
            AutoEllipsis = true,
            ForeColor = PrimaryColor,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        };
        content.Dock = DockStyle.Fill;
        if (content is Label label)
        {
            label.Font = new Font("Segoe UI", 10F);
            label.ForeColor = TextColor;
            label.Padding = new Padding(0, 8, 0, 0);
        }
        card.Controls.Add(content);
        card.Controls.Add(heading);
        return card;
    }

    private Control CreateApplicationShell()
    {
        var shell = new Panel { Dock = DockStyle.Fill, BackColor = CanvasColor };
        var sidebar = CreateSidebar();
        var content = new Panel { Dock = DockStyle.Fill, BackColor = CanvasColor };
        content.Controls.Add(_tabs);
        content.Controls.Add(CreateApplicationHeader());
        shell.Controls.Add(content);
        shell.Controls.Add(sidebar);
        return shell;
    }

    private Control CreateSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 248,
            BackColor = SidebarColor,
            Padding = new Padding(0)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = SidebarColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));

        var brand = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22, 20, 12, 10),
            BackColor = SidebarColor
        };
        brand.Controls.Add(new Label
        {
            Text = "LiMS",
            Dock = DockStyle.Top,
            Height = 46,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 22, FontStyle.Bold)
        });
        brand.Controls.Add(new Label
        {
            Text = "QUẢN LÝ THƯ VIỆN",
            Dock = DockStyle.Bottom,
            Height = 24,
            ForeColor = AdminTheme.SidebarText,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
        });

        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(14, 12, 8, 8),
            BackColor = SidebarColor
        };
        foreach (TabPage page in _tabs.TabPages)
        {
            var tab = page;
            var button = new Button
            {
                Text = tab.Text,
                Width = 212,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 8, 0),
                FlatStyle = FlatStyle.Flat,
                UseVisualStyleBackColor = false,
                BackColor = SidebarColor,
                ForeColor = AdminTheme.SidebarText,
                Font = new Font("Segoe UI", 9.5F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 0, 0, 8),
                Tag = tab
            };
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.MouseOverBackColor = AdminTheme.SidebarRaised;
            button.Click += (_, _) => _tabs.SelectedTab = tab;
            _navigationButtons.Add(button);
            navigation.Controls.Add(button);
        }

        var userPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20, 14, 12, 10),
            BackColor = AdminTheme.SidebarRaised
        };
        userPanel.Controls.Add(new Label
        {
            Text = _user.Role.ToUpperInvariant(),
            Dock = DockStyle.Top,
            Height = 20,
            ForeColor = PrimaryColor,
            Font = new Font("Segoe UI", 8, FontStyle.Bold)
        });
        userPanel.Controls.Add(new Label
        {
            Text = _user.FullName,
            Dock = DockStyle.Bottom,
            Height = 28,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            AutoEllipsis = true
        });

        layout.Controls.Add(brand, 0, 0);
        layout.Controls.Add(navigation, 0, 1);
        layout.Controls.Add(userPanel, 0, 2);
        sidebar.Controls.Add(layout);
        return sidebar;
    }

    private Control CreateApplicationHeader()
    {
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 90,
            BackColor = SurfaceColor,
            Padding = new Padding(30, 18, 28, 16)
        };
        _currentPageTitle.Text = "Tổng quan";
        _currentPageTitle.Dock = DockStyle.Left;
        _currentPageTitle.AutoSize = true;
        _currentPageTitle.TextAlign = ContentAlignment.MiddleLeft;
        _currentPageTitle.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        _currentPageTitle.ForeColor = TextColor;

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            BackColor = SurfaceColor
        };
        var logout = CreateActionButton("Đăng xuất", destructive: true);
        logout.Click += (_, _) =>
        {
            if (MessageBox.Show(this, "Bạn có chắc muốn đăng xuất khỏi hệ thống?",
                    "Đăng xuất", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                Close();
        };
        var changePassword = CreateActionButton("Đổi mật khẩu");
        changePassword.Click += async (_, _) => await RunAsync(ChangePasswordAsync);
        actions.Controls.Add(logout);
        actions.Controls.Add(changePassword);
        header.Controls.Add(actions);
        header.Controls.Add(_currentPageTitle);
        return header;
    }

    private void UpdateNavigation()
    {
        if (_tabs.SelectedTab is null)
            return;

        _currentPageTitle.Text = _tabs.SelectedTab.Text;
        foreach (var button in _navigationButtons)
        {
            var selected = ReferenceEquals(button.Tag, _tabs.SelectedTab);
            button.BackColor = selected ? PrimaryColor : SidebarColor;
            button.ForeColor = selected ? Color.White : AdminTheme.SidebarText;
            button.Font = new Font("Segoe UI", 9.5F, selected ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private static string GetPageDescription(string title) => title switch
    {
        "Kho sách" => "Quản lý danh mục, đầu sách và các bản sao trong kho.",
        "Thể loại sách" => "Tạo, đổi tên và xóa thể loại chưa được gán cho đầu sách.",
        "Độc giả" => "Tra cứu tài khoản, cập nhật thẻ và trạng thái độc giả.",
        "Mượn - trả" => "Thực hiện cho mượn, nhận trả và gia hạn phiếu.",
        "Tiền phạt" => "Theo dõi khoản phạt và ghi nhận thanh toán.",
        "Đề xuất mua sách" => "Xem xét các đề xuất bổ sung tài liệu từ độc giả.",
        "Cấu hình" => "Điều chỉnh các quy tắc hoạt động của thư viện.",
        "Nhật ký hoạt động" => "Theo dõi lịch sử thao tác của tài khoản quản trị.",
        "Sao lưu & phục hồi" => "Tạo bản sao lưu SQL Server và phục hồi có bản dự phòng an toàn.",
        _ => "Quản lý hoạt động thư viện."
    };

    private FlowLayoutPanel CreateToolbar(params (string Title, Func<Task> Action)[] actions)
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 54,
            Padding = new Padding(0, 4, 0, 4),
            WrapContents = false,
            AutoScroll = true,
            BackColor = CanvasColor
        };
        foreach (var (title, action) in actions)
        {
            var button = CreateActionButton(title, IsDestructiveAction(title));
            button.Margin = new Padding(0, 0, 9, 0);
            button.Click += async (_, _) =>
            {
                if (!button.Enabled)
                    return;
                button.Enabled = false;
                try
                {
                    await RunAsync(action);
                }
                finally
                {
                    button.Enabled = true;
                }
            };
            toolbar.Controls.Add(button);
        }

        return toolbar;
    }

    private async Task RefreshDashboardAsync()
    {
        var data = await _api.GetDashboardAsync(CancellationToken.None);
        _latestDashboard = data;
        _dashboardValues["TotalBooks"].Text = data.TotalBooks.ToString("N0");
        _dashboardValues["AvailableCopies"].Text = data.AvailableCopies.ToString("N0");
        _dashboardValues["ActiveReaders"].Text = data.ActiveReaders.ToString("N0");
        _dashboardValues["ActiveLoans"].Text = data.ActiveLoans.ToString("N0");
        _dashboardValues["OverdueLoans"].Text = data.OverdueLoans.ToString("N0");
        _dashboardValues["UnpaidFines"].Text = $"{data.UnpaidFines:N0} đ";
        _dashboardSummary.Text =
            $"Tổng bản sao: {data.TotalCopies:N0}\r\n" +
            $"Đặt giữ đang chờ: {data.PendingReservations:N0}\r\n" +
            $"Đề xuất chờ duyệt: {data.PendingProposals:N0}\r\n" +
            $"Đề xuất đã duyệt: {data.ApprovedProposals:N0}\r\n" +
            $"Đề xuất bị từ chối: {data.RejectedProposals:N0}\r\n" +
            $"Tiền phạt đã thu: {data.CollectedFines:N0} đ";
        _popularBooks.Text = data.PopularBooks.Count == 0
            ? "Chưa có dữ liệu lượt mượn."
            : string.Join(Environment.NewLine, data.PopularBooks.Select((book, index) =>
                $"{index + 1}.  {book.Title}  ·  {book.LoanCount:N0} lượt"));
        _monthlyReportChart.Invalidate();
    }

    private void PaintMonthlyReportChart(object? sender, PaintEventArgs e)
    {
        e.Graphics.Clear(SurfaceColor);
        var reports = _latestDashboard?.MonthlyReports ?? [];
        if (reports.Count == 0)
        {
            TextRenderer.DrawText(e.Graphics, "Chưa có dữ liệu báo cáo.",
                Font, _monthlyReportChart.ClientRectangle, MutedColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        using var font = new Font("Segoe UI", 8F);
        using var legendFont = new Font("Segoe UI", 8F, FontStyle.Bold);
        using var borrowBrush = new SolidBrush(BorrowColor);
        using var returnBrush = new SolidBrush(ReturnColor);
        using var trackBrush = new SolidBrush(AdminTheme.Border);
        TextRenderer.DrawText(e.Graphics, "Mượn", legendFont, new Point(47, 2), BorrowColor);
        TextRenderer.DrawText(e.Graphics, "Trả", legendFont, new Point(135, 2), ReturnColor);

        var maxCount = Math.Max(1, reports.Max(report => Math.Max(report.BorrowCount, report.ReturnCount)));
        var rowHeight = Math.Max(20, (_monthlyReportChart.ClientSize.Height - 24) / reports.Count);
        for (var index = 0; index < reports.Count; index++)
        {
            var report = reports[index];
            var y = 24 + index * rowHeight;
            TextRenderer.DrawText(e.Graphics, report.Month, font, new Rectangle(2, y, 44, rowHeight),
                TextColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            DrawBar(e.Graphics, 48, y + 6, 56, report.BorrowCount, maxCount, trackBrush, borrowBrush);
            TextRenderer.DrawText(e.Graphics, report.BorrowCount.ToString("N0"), font,
                new Rectangle(105, y, 28, rowHeight), TextColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            DrawBar(e.Graphics, 136, y + 6, 56, report.ReturnCount, maxCount, trackBrush, returnBrush);
            TextRenderer.DrawText(e.Graphics, report.ReturnCount.ToString("N0"), font,
                new Rectangle(193, y, 28, rowHeight), TextColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(e.Graphics, $"{report.CollectedFines:N0} đ", font,
                new Rectangle(224, y, Math.Max(50, _monthlyReportChart.ClientSize.Width - 226), rowHeight),
                MutedColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
    }

    private static void DrawBar(
        Graphics graphics,
        int x,
        int y,
        int width,
        int count,
        int maxCount,
        Brush trackBrush,
        Brush valueBrush)
    {
        graphics.FillRectangle(trackBrush, x, y, width, 8);
        if (count > 0)
            graphics.FillRectangle(valueBrush, x, y, Math.Max(2, width * count / maxCount), 8);
    }

    private async Task ExportDashboardReportAsync()
    {
        var data = _latestDashboard ?? await _api.GetDashboardAsync(CancellationToken.None);
        using var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = "csv",
            FileName = $"LiMS-BaoCao-{DateTime.Now:yyyyMMdd}.csv",
            Filter = "CSV (*.csv)|*.csv"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        static string CsvField(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
        static string CsvRow(params string[] values) => string.Join(",", values.Select(CsvField));
        var rows = new List<string>
        {
            CsvRow("Báo cáo thống kê LiMS"),
            CsvRow("Thời điểm xuất", DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
            "",
            CsvRow("Chỉ số tổng quan", "Giá trị"),
            CsvRow("Đầu sách", data.TotalBooks.ToString(CultureInfo.InvariantCulture)),
            CsvRow("Tổng bản sao", data.TotalCopies.ToString(CultureInfo.InvariantCulture)),
            CsvRow("Bản sao sẵn sàng", data.AvailableCopies.ToString(CultureInfo.InvariantCulture)),
            CsvRow("Độc giả hoạt động", data.ActiveReaders.ToString(CultureInfo.InvariantCulture)),
            CsvRow("Phiếu đang mượn", data.ActiveLoans.ToString(CultureInfo.InvariantCulture)),
            CsvRow("Phiếu quá hạn", data.OverdueLoans.ToString(CultureInfo.InvariantCulture)),
            CsvRow("Đặt giữ đang chờ", data.PendingReservations.ToString(CultureInfo.InvariantCulture)),
            CsvRow("Tiền phạt chưa thu (VND)", data.UnpaidFines.ToString("0.##", CultureInfo.InvariantCulture)),
            CsvRow("Tiền phạt đã thu (VND)", data.CollectedFines.ToString("0.##", CultureInfo.InvariantCulture)),
            "",
            CsvRow("Trạng thái đề xuất", "Số lượng"),
            CsvRow("Chờ duyệt", data.PendingProposals.ToString(CultureInfo.InvariantCulture)),
            CsvRow("Đã duyệt", data.ApprovedProposals.ToString(CultureInfo.InvariantCulture)),
            CsvRow("Bị từ chối", data.RejectedProposals.ToString(CultureInfo.InvariantCulture)),
            "",
            CsvRow("Tháng", "Lượt mượn", "Lượt trả", "Tiền phạt đã thu (VND)")
        };
        rows.AddRange(data.MonthlyReports.Select(report => CsvRow(
            report.Month,
            report.BorrowCount.ToString(CultureInfo.InvariantCulture),
            report.ReturnCount.ToString(CultureInfo.InvariantCulture),
            report.CollectedFines.ToString("0.##", CultureInfo.InvariantCulture))));
        rows.Add("");
        rows.Add(CsvRow("Đầu sách được mượn nhiều", "Số lượt mượn"));
        rows.AddRange(data.PopularBooks.Select(book => CsvRow(
            book.Title,
            book.LoanCount.ToString(CultureInfo.InvariantCulture))));

        try
        {
            File.WriteAllText(dialog.FileName, string.Join(Environment.NewLine, rows), new UTF8Encoding(true));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Không thể lưu báo cáo: {exception.Message}",
                "Lỗi xuất báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        MessageBox.Show(this, "Đã xuất báo cáo CSV thành công.",
            "Xuất báo cáo", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ChangePasswordAsync()
    {
        var values = Prompt("Đổi mật khẩu",
            ("Mật khẩu hiện tại", "", true),
            ("Mật khẩu mới (ít nhất 8 ký tự)", "", true));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]) || values[1].Length < 8)
        {
            MessageBox.Show(this, "Nhập mật khẩu hiện tại và mật khẩu mới có ít nhất 8 ký tự.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = await _api.ChangePasswordAsync(
            new ChangePasswordRequest(values[0], values[1]),
            CancellationToken.None);
        MessageBox.Show(this, result.Message, "Tài khoản",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task LoadBooksAsync() =>
        _booksGrid.DataSource = (await _api.GetBooksAsync(_bookSearch, CancellationToken.None)).ToList();

    private async Task LoadCategoriesAsync() =>
        _categoriesGrid.DataSource = (await _api.GetCategoriesAsync(CancellationToken.None)).ToList();

    private async Task LoadReadersAsync()
    {
        var cardStatus = _readerStatusFilter.SelectedIndex switch
        {
            1 => "Active",
            2 => "Inactive",
            _ => null
        };

        _readersGrid.DataSource = (
            await _api.GetReadersAsync(
                _readerSearch,
                cardStatus,
                CancellationToken.None)
        ).ToList();
    }

    private async void ToggleReaderActivityAsync(object? sender, DataGridViewCellEventArgs e)
    {
        if (_isUpdatingReaderActivity
            || e.RowIndex < 0
            || e.ColumnIndex < 0
            || _readersGrid.Columns[e.ColumnIndex].DataPropertyName != nameof(ReaderResponse.IsActive)
            || _readersGrid.Rows[e.RowIndex].DataBoundItem is not ReaderResponse reader)
            return;

        _isUpdatingReaderActivity = true;
        try
        {
            await RunAsync(async () =>
            {
                await _api.UpdateReaderAsync(
                    reader.ReaderId,
                    new UpdateReaderRequest(
                        reader.FullName,
                        reader.Email,
                        reader.Phone,
                        reader.CardStatus,
                        reader.BorrowLimit,
                        reader.CardExpiryDate,
                        !reader.IsActive),
                    CancellationToken.None);
                await LoadReadersAsync();
            });
        }
        finally
        {
            _isUpdatingReaderActivity = false;
        }
    }

    private async Task SearchBooksAsync()
    {
        var values = Prompt("Tìm đầu sách", ("Tên, tác giả, ISBN hoặc thể loại", _bookSearch, false));
        if (values is null)
            return;
        _bookSearch = values[0];
        await LoadBooksAsync();
    }

    private async Task SearchReadersAsync()
    {
        var values = Prompt("Tìm độc giả", ("Mã thẻ, tài khoản, họ tên, email hoặc số điện thoại", _readerSearch, false));
        if (values is null)
            return;
        _readerSearch = values[0];
        await LoadReadersAsync();
    }

    private async Task LoadLoansAsync() =>
        _loansGrid.DataSource = (await _api.GetLoansAsync(null, CancellationToken.None)).ToList();

    private async Task LoadFinesAsync() =>
        _finesGrid.DataSource = (await _api.GetFinesAsync(null, CancellationToken.None)).ToList();

    private async Task LoadProposalsAsync() =>
        _proposalsGrid.DataSource = (await _api.GetProposalsAsync(null, CancellationToken.None)).ToList();

    private async Task LoadSettingsAsync() =>
        _settingsGrid.DataSource = (await _api.GetSettingsAsync(CancellationToken.None)).ToList();

    private async Task LoadActivityLogsAsync() =>
        _logsGrid.DataSource = (await _api.GetActivityLogsAsync(CancellationToken.None)).ToList();

    private async Task LoadDatabaseBackupsAsync()
    {
        _databaseBackupStatus = await _api.GetDatabaseBackupsAsync(CancellationToken.None);
        _backupSummary.Text =
            $"Cơ sở dữ liệu: {_databaseBackupStatus.DatabaseName}  |  Thư mục sao lưu SQL Server: {_databaseBackupStatus.BackupDirectory}";
        _databaseBackupsGrid.DataSource = _databaseBackupStatus.Backups.ToList();
    }

    private async Task CreateDatabaseBackupAsync()
    {
        var backup = await _api.CreateDatabaseBackupAsync(CancellationToken.None);
        MessageBox.Show(this, $"Đã tạo và kiểm tra bản sao lưu:\r\n{backup.FileName}\r\nDung lượng: {backup.SizeBytes:N0} byte",
            "Sao lưu thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await LoadDatabaseBackupsAsync();
    }

    private async Task RestoreDatabaseBackupAsync()
    {
        var backup = _databaseBackupsGrid.CurrentRow?.DataBoundItem as DatabaseBackupFileResponse;
        if (backup is null)
        {
            MessageBox.Show(this, "Hãy chọn một tệp sao lưu trước khi phục hồi.",
                "Chưa chọn bản sao lưu", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var status = _databaseBackupStatus ?? await _api.GetDatabaseBackupsAsync(CancellationToken.None);
        var decision = MessageBox.Show(
            this,
            $"CẢNH BÁO: dữ liệu hiện tại trong {status.DatabaseName} sẽ được thay thế bằng bản sao lưu {backup.FileName}.\r\n" +
            "Hệ thống sẽ tạo một bản sao lưu dự phòng trước khi phục hồi.\r\n\r\nBạn có chắc chắn muốn tiếp tục?",
            "Xác nhận phục hồi cơ sở dữ liệu",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (decision != DialogResult.Yes)
            return;

        var values = Prompt(
            "Xác nhận thao tác phá hủy dữ liệu",
            ($"Nhập chính xác tên cơ sở dữ liệu: {status.DatabaseName}", "", false));
        if (values is null)
            return;
        if (!string.Equals(values[0], status.DatabaseName, StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "Tên cơ sở dữ liệu không khớp. Không thực hiện phục hồi.",
                "Đã hủy phục hồi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = await _api.RestoreDatabaseBackupAsync(
            backup.FileName,
            new RestoreDatabaseBackupRequest(values[0]),
            CancellationToken.None);
        MessageBox.Show(this,
            $"Đã phục hồi {result.DatabaseName} từ {result.RestoredFileName}.\r\n" +
            $"Bản sao lưu dự phòng trước khi phục hồi: {result.SafetyBackupFileName}",
            "Phục hồi thành công",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        await LoadDatabaseBackupsAsync();
    }

    private async Task CreateBookAsync()
    {
        var categories = (await _api.GetCategoriesAsync(CancellationToken.None))
            .Select(category => category.Name)
            .ToArray();
        var values = Prompt("Thêm đầu sách",
            new PromptField("Danh mục", categories.FirstOrDefault() ?? "Chưa phân loại", false, categories),
            new PromptField("Tên sách", "", false),
            new PromptField("Tác giả", "", false),
            new PromptField("ISBN", "", false),
            new PromptField("Nhà xuất bản", "", false),
            new PromptField("Năm xuất bản", "", false),
            new PromptField("Vị trí kệ", "", false),
            new PromptField("Mô tả", "", false),
            new PromptField("Số bản sao ban đầu", "0", false));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]) || string.IsNullOrWhiteSpace(values[1])
            || string.IsNullOrWhiteSpace(values[2]))
        {
            MessageBox.Show(this, "Vui lòng nhập Danh mục, Tên sách và Tác giả.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!TryParseInt(values[8], out var copies) || copies < 0)
        {
            MessageBox.Show(this, "Số bản sao ban đầu phải là số nguyên không âm.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!TryParseYear(values[5], out var year))
        {
            MessageBox.Show(this, "Năm xuất bản không hợp lệ.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await _api.CreateBookAsync(
            new CreateBookRequest(values[0], values[1], values[2], NullIfBlank(values[3]),
                NullIfBlank(values[4]), year, NullIfBlank(values[6]), NullIfBlank(values[7]), copies),
            CancellationToken.None);
        await LoadBooksAsync();
        MessageBox.Show(this, "Đã thêm đầu sách.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CreateCategoryAsync()
    {
        var values = Prompt("Thêm thể loại", ("Tên thể loại", "", false));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]))
        {
            MessageBox.Show(this, "Vui lòng nhập tên thể loại.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await _api.CreateCategoryAsync(new CreateCategoryRequest(values[0]), CancellationToken.None);
        await LoadCategoriesAsync();
        MessageBox.Show(this, $"Đã thêm thể loại '{values[0]}'.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RenameCategoryAsync()
    {
        var category = Selected<CategoryResponse>(_categoriesGrid);
        if (category is null)
            return;
        var values = Prompt("Đổi tên thể loại", ("Tên thể loại", category.Name, false));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]))
        {
            MessageBox.Show(this, "Vui lòng nhập tên thể loại.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await _api.UpdateCategoryAsync(
            category.Id,
            new UpdateCategoryRequest(values[0]),
            CancellationToken.None);
        await LoadCategoriesAsync();
        MessageBox.Show(this, "Đã đổi tên thể loại.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task DeleteCategoryAsync()
    {
        var category = Selected<CategoryResponse>(_categoriesGrid);
        if (category is null)
            return;
        if (category.BookCount > 0)
        {
            MessageBox.Show(this,
                "Thể loại đang được đầu sách sử dụng. Hãy chuyển các đầu sách sang thể loại khác trước.",
                "Không thể xóa thể loại",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }
        if (MessageBox.Show(this, $"Xóa thể loại '{category.Name}'?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        await _api.DeleteCategoryAsync(category.Id, CancellationToken.None);
        await LoadCategoriesAsync();
        MessageBox.Show(this, $"Đã xóa thể loại '{category.Name}'.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task EditBookAsync()
    {
        var book = Selected<BookResponse>(_booksGrid);
        if (book is null)
            return;
        var categories = (await _api.GetCategoriesAsync(CancellationToken.None))
            .Select(category => category.Name)
            .ToArray();
        var values = Prompt("Cập nhật đầu sách",
            new PromptField("Danh mục", book.Category, false, categories),
            new PromptField("Tên sách", book.Title, false),
            new PromptField("Tác giả", book.Author, false),
            new PromptField("ISBN", book.Isbn ?? "", false),
            new PromptField("Nhà xuất bản", book.Publisher ?? "", false),
            new PromptField("Năm xuất bản", book.PublishedYear?.ToString() ?? "", false),
            new PromptField("Vị trí kệ", book.ShelfLocation ?? "", false),
            new PromptField("Mô tả", book.Description ?? "", false),
            new PromptField("Đang kinh doanh (true/false)", "true", false, ["true", "false"]));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]) || string.IsNullOrWhiteSpace(values[1]))
        {
            MessageBox.Show(this, "Vui lòng nhập Danh mục và Tên sách.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!TryParseYear(values[5], out var year))
        {
            MessageBox.Show(this, "Năm xuất bản không hợp lệ.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!bool.TryParse(values[8], out var isActive))
        {
            MessageBox.Show(this, "Trường 'Đang kinh doanh' phải là true hoặc false.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await _api.UpdateBookAsync(
            book.Id,
            new UpdateBookRequest(values[0], values[1], values[2], NullIfBlank(values[3]),
                NullIfBlank(values[4]), year, NullIfBlank(values[6]), NullIfBlank(values[7]), isActive),
            CancellationToken.None);
        await LoadBooksAsync();
        MessageBox.Show(this, "Đã cập nhật đầu sách.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task UploadBookCoverAsync()
    {
        var book = Selected<BookResponse>(_booksGrid);
        if (book is null)
        {
            MessageBox.Show(this, "Hãy chọn một đầu sách trước.", "Chưa chọn sách",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new OpenFileDialog
        {
            Title = $"Chọn ảnh bìa cho “{book.Title}”",
            Filter = "Ảnh bìa (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        await _api.UploadBookCoverAsync(book.Id, dialog.FileName, CancellationToken.None);
        await LoadBooksAsync();
        MessageBox.Show(this, "Đã lưu ảnh bìa cho đầu sách.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task DeactivateBookAsync()
    {
        var book = Selected<BookResponse>(_booksGrid);
        if (book is null)
            return;
        if (MessageBox.Show(this,
                $"Ngừng kinh doanh đầu sách “{book.Title}”? Dữ liệu mượn và bản sao vẫn được giữ.",
                "Xác nhận ngừng kinh doanh",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        await _api.UpdateBookAsync(
            book.Id,
            new UpdateBookRequest(
                book.Category,
                book.Title,
                book.Author,
                book.Isbn,
                book.Publisher,
                book.PublishedYear,
                book.ShelfLocation,
                book.Description,
                false),
            CancellationToken.None);
        await LoadBooksAsync();
    }

    private async Task ManageCopiesAsync()
    {
        var book = Selected<BookResponse>(_booksGrid);
        if (book is null)
            return;
        var copies = (await _api.GetCopiesAsync(book.Id, CancellationToken.None)).ToList();
        using var form = new CopyManagementForm(book.Title, copies);
        if (form.ShowDialog(this) != DialogResult.OK || form.SelectedCopy is null)
            return;
        var values = Prompt("Trạng thái bản sao",
            new PromptField("Barcode", form.SelectedCopy.Barcode, false),
            new PromptField("Trạng thái", form.SelectedCopy.Status, false,
                ["Available", "Maintenance", "Lost"]));
        if (values is null)
            return;
        await _api.UpdateCopyStatusAsync(
            form.SelectedCopy.Id,
            new CopyStatusRequest(values[1]),
            CancellationToken.None);
        await LoadBooksAsync();
        MessageBox.Show(this, "Đã cập nhật trạng thái bản sao.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task AddCopiesAsync()
    {
        var book = Selected<BookResponse>(_booksGrid);
        if (book is null)
            return;
        var values = Prompt("Thêm bản sao",
            ("Tiền tố barcode", $"B{book.Id:D6}", false),
            ("Số lượng", "1", false));
        if (values is null)
            return;
        if (!TryParseInt(values[1], out var quantity) || quantity <= 0)
        {
            MessageBox.Show(this, "Số lượng bản sao phải là số nguyên lớn hơn 0.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await _api.AddCopiesAsync(
            book.Id,
            new CreateCopiesRequest(quantity, values[0]),
            CancellationToken.None);
        await LoadBooksAsync();
        MessageBox.Show(this, $"Đã thêm {quantity} bản sao.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CreateReaderAsync()
    {
        var values = Prompt("Tạo thẻ độc giả",
            ("Tên đăng nhập", "", false),
            ("Mật khẩu ban đầu", "", true),
            ("Email", "", false),
            ("Họ tên", "", false),
            ("Điện thoại", "", false),
            ("Mã thẻ", "", false),
            ("Giới hạn mượn", "5", false),
            ("Thời hạn thẻ (tháng)", "48", false));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]) || string.IsNullOrWhiteSpace(values[1])
            || string.IsNullOrWhiteSpace(values[3]) || string.IsNullOrWhiteSpace(values[5]))
        {
            MessageBox.Show(this, "Vui lòng nhập Tên đăng nhập, Mật khẩu, Họ tên và Mã thẻ.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!short.TryParse(values[6], out var borrowLimit) || borrowLimit <= 0)
        {
            MessageBox.Show(this, "Giới hạn mượn phải là số nguyên lớn hơn 0.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!TryParseInt(values[7], out var validityMonths) || validityMonths <= 0)
        {
            MessageBox.Show(this, "Thời hạn thẻ (tháng) phải là số nguyên lớn hơn 0.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await _api.CreateReaderAsync(
            new CreateReaderRequest(values[0], values[1], values[2], values[3],
                NullIfBlank(values[4]), values[5], borrowLimit, validityMonths),
            CancellationToken.None);
        await LoadReadersAsync();
        MessageBox.Show(this, $"Đã tạo thẻ độc giả cho '{values[3]}'.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task UpdateReaderAsync()
    {
        var reader = Selected<ReaderResponse>(_readersGrid);
        if (reader is null)
            return;
        var values = Prompt("Cập nhật độc giả",
            new PromptField("Họ tên", reader.FullName, false),
            new PromptField("Email", reader.Email, false),
            new PromptField("Điện thoại", reader.Phone ?? "", false),
            new PromptField("Trạng thái thẻ", reader.CardStatus, false, ["Active", "Inactive"]),
            new PromptField("Giới hạn mượn", reader.BorrowLimit.ToString(), false),
            new PromptField("Ngày hết hạn (yyyy-MM-dd)", reader.CardExpiryDate.ToString("yyyy-MM-dd"), false),
            new PromptField("Tài khoản hoạt động", reader.IsActive.ToString(), false, ["true", "false"]));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]))
        {
            MessageBox.Show(this, "Vui lòng nhập Họ tên.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!short.TryParse(values[4], out var borrowLimit) || borrowLimit <= 0)
        {
            MessageBox.Show(this, "Giới hạn mượn phải là số nguyên lớn hơn 0.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!DateOnly.TryParse(values[5], CultureInfo.InvariantCulture, out var expiry))
        {
            MessageBox.Show(this, "Ngày hết hạn phải có định dạng yyyy-MM-dd.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!bool.TryParse(values[6], out var isActive))
        {
            MessageBox.Show(this, "Trường 'Tài khoản hoạt động' phải là true hoặc false.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        await _api.UpdateReaderAsync(
            reader.ReaderId,
            new UpdateReaderRequest(values[0], values[1], NullIfBlank(values[2]),
                values[3], borrowLimit, expiry, isActive),
            CancellationToken.None);
        await LoadReadersAsync();
        MessageBox.Show(this, "Đã cập nhật thông tin độc giả.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task DeactivateReaderAsync()
    {
        var reader = Selected<ReaderResponse>(_readersGrid);
        if (reader is null)
            return;
        if (MessageBox.Show(this,
                $"Khóa thẻ và tài khoản của {reader.FullName}? Lịch sử nghiệp vụ vẫn được giữ.",
                "Xác nhận khóa độc giả",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        await _api.UpdateReaderAsync(
            reader.ReaderId,
            new UpdateReaderRequest(
                reader.FullName,
                reader.Email,
                reader.Phone,
                "Inactive",
                reader.BorrowLimit,
                reader.CardExpiryDate,
                false),
            CancellationToken.None);
        await LoadReadersAsync();
        MessageBox.Show(this, $"Đã khóa thẻ và tài khoản của {reader.FullName}.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ResetReaderPasswordAsync()
    {
        var reader = Selected<ReaderResponse>(_readersGrid);
        if (reader is null)
        {
            MessageBox.Show(this, "Vui lòng chọn độc giả cần đặt lại mật khẩu.",
                "Chưa chọn độc giả", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this,
                $"Đặt mật khẩu mới cho tài khoản {reader.Username} ({reader.FullName})?",
                "Xác nhận đặt lại mật khẩu",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        var values = Prompt("Đặt lại mật khẩu độc giả",
            ("Mật khẩu mới (8-256 ký tự)", "", true),
            ("Xác nhận mật khẩu mới", "", true));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]) || values[0].Length < 8 || values[0].Length > 256)
        {
            MessageBox.Show(this, "Mật khẩu mới phải có từ 8 đến 256 ký tự.",
                "Mật khẩu chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!string.Equals(values[0], values[1], StringComparison.Ordinal))
        {
            MessageBox.Show(this, "Mật khẩu xác nhận không khớp.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var result = await _api.ResetReaderPasswordAsync(
            reader.ReaderId,
            new ResetReaderPasswordRequest(values[0]),
            CancellationToken.None);
        MessageBox.Show(this, result.Message, "Đặt lại mật khẩu",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CheckoutAsync()
    {
        var values = Prompt("Cho mượn sách",
            ("Mã thẻ độc giả", "", false),
            ("Barcode bản sách", "", false),
            ("Mã đặt giữ (nếu có)", "", false));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]) || string.IsNullOrWhiteSpace(values[1]))
        {
            MessageBox.Show(this, "Vui lòng nhập Mã thẻ độc giả và Barcode bản sách.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        long? reservationId = null;
        if (!string.IsNullOrWhiteSpace(values[2]))
        {
            if (!long.TryParse(values[2], out var parsed))
            {
                MessageBox.Show(this, "Mã đặt giữ phải là số.",
                    "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            reservationId = parsed;
        }
        await _api.CheckoutAsync(
            new CheckoutRequest(values[0], values[1], reservationId),
            CancellationToken.None);
        await LoadLoansAsync();
        MessageBox.Show(this, "Đã ghi nhận phiếu mượn.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ReturnAsync()
    {
        var values = Prompt("Nhận trả sách",
            ("Barcode bản sách", "", false),
            ("Phạt bổ sung (VND, nếu có)", "0", false),
            ("Lý do phạt bổ sung", "", false));
        if (values is null)
            return;
        if (string.IsNullOrWhiteSpace(values[0]))
        {
            MessageBox.Show(this, "Vui lòng nhập Barcode bản sách.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (!decimal.TryParse(values[1], out var fine) || fine < 0)
        {
            MessageBox.Show(this, "Phạt bổ sung phải là số không âm.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await _api.ReturnAsync(
            new ReturnRequest(values[0], NullIfBlank(values[2]), fine),
            CancellationToken.None);
        await LoadLoansAsync();
        await LoadFinesAsync();
        MessageBox.Show(this, "Đã ghi nhận trả sách.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RenewAsync()
    {
        var loan = Selected<LoanResponse>(_loansGrid);
        if (loan is null)
            return;
        await _api.RenewAsync(loan.Id, CancellationToken.None);
        await LoadLoansAsync();
        MessageBox.Show(this, $"Đã gia hạn phiếu mượn #{loan.Id}.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task CollectFineAsync()
    {
        var fine = Selected<FineResponse>(_finesGrid);
        if (fine is null)
            return;
        var values = Prompt("Thu tiền phạt",
            ("Độc giả", $"{fine.ReaderName} ({fine.ReaderCardNo})", false),
            ("Số tiền (để trống hoặc 0 = toàn bộ)", "0", false),
            ("Ghi chú thu tiền", "", false));
        if (values is null)
            return;
        if (!decimal.TryParse(values[1], out var amount) || amount < 0)
        {
            MessageBox.Show(this, "Số tiền thu phải là số không âm.",
                "Thông tin chưa hợp lệ", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        await _api.CollectFineAsync(
            fine.Id,
            new CollectFineRequest(amount, string.IsNullOrWhiteSpace(values[2]) ? "Thu tại quầy" : values[2]),
            CancellationToken.None);
        await LoadFinesAsync();
        await RefreshDashboardAsync();
        MessageBox.Show(this, "Đã ghi nhận thanh toán tiền phạt.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task ReviewProposalAsync(string decision)
    {
        var proposal = Selected<ProposalResponse>(_proposalsGrid);
        if (proposal is null)
            return;
        var values = Prompt(decision == "Approved" ? "Duyệt đề xuất" : "Từ chối đề xuất",
            ("Sách", proposal.Title, false),
            ("Ghi chú", "", false));
        if (values is null)
            return;
        await _api.ReviewProposalAsync(
            proposal.ProposalId,
            new ReviewProposalRequest(decision, NullIfBlank(values[1])),
            CancellationToken.None);
        await LoadProposalsAsync();
        await RefreshDashboardAsync();
        MessageBox.Show(this,
            decision == "Approved" ? "Đã duyệt đề xuất." : "Đã từ chối đề xuất.",
            "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task UpdateSettingAsync()
    {
        var setting = Selected<SystemConfigResponse>(_settingsGrid);
        if (setting is null)
            return;
        var values = Prompt("Cập nhật cấu hình",
            ("Khóa", setting.Key, false),
            ("Giá trị", setting.Value, false),
            ("Mô tả", setting.Description ?? "", false));
        if (values is null)
            return;
        await _api.UpdateSettingAsync(
            setting.Key,
            new UpdateSystemConfigRequest(values[1], NullIfBlank(values[2])),
            CancellationToken.None);
        await LoadSettingsAsync();
        MessageBox.Show(this, $"Đã cập nhật cấu hình '{setting.Key}'.", "Thành công",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (AdminApiClient.AdminApiException exception)
        {
            MessageBox.Show(this, exception.Message, "Không thể hoàn tất thao tác",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (HttpRequestException exception)
        {
            MessageBox.Show(this, $"Không kết nối được API: {exception.Message}", "Lỗi kết nối",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, $"Đã xảy ra lỗi không mong muốn: {exception.Message}", "Lỗi",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5F)
        };
        AdminTheme.StyleGrid(grid);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 4, 0);
        grid.DefaultCellStyle.Padding = new Padding(8, 2, 4, 2);
        grid.DefaultCellStyle.NullValue = "—";
        grid.DataBindingComplete += (_, _) => FormatGridColumns(grid);
        return grid;
    }

    private static Button CreateActionButton(string title, bool destructive = false)
    {
        var primary = !destructive && title is
            "Thêm đầu sách" or "Thêm độc giả" or "Thêm bản sao" or "Thêm/cập nhật ảnh bìa"
            or "Cho mượn / quét barcode" or "Nhận trả / quét barcode"
            or "Ghi nhận thanh toán" or "Duyệt đề xuất" or "Gia hạn phiếu chọn";
        var button = new Button
        {
            Text = title,
            AutoSize = true,
            MinimumSize = new Size(96, 42),
            Padding = new Padding(12, 7, 12, 7),
            Margin = new Padding(4, 0, 4, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = destructive
                ? AdminTheme.DangerSurface
                : primary ? PrimaryColor : AdminTheme.SurfaceRaised,
            ForeColor = destructive
                ? AdminTheme.Danger
                : primary ? Color.White : TextColor,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = destructive || !primary ? 1 : 0;
        button.FlatAppearance.BorderColor = destructive ? AdminTheme.Danger : AdminTheme.Border;
        button.FlatAppearance.MouseOverBackColor = destructive
            ? Color.FromArgb(86, 42, 56)
            : primary ? AdminTheme.AccentHover : AdminTheme.Surface;
        return button;
    }

    private static bool IsDestructiveAction(string title) =>
        title.Contains("Ngừng", StringComparison.OrdinalIgnoreCase)
        || title.Contains("Khóa", StringComparison.OrdinalIgnoreCase)
        || title.Contains("Từ chối", StringComparison.OrdinalIgnoreCase);

    private static void FormatGridColumns(DataGridView grid)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "Mã",
            ["ReaderId"] = "Mã độc giả",
            ["AccountId"] = "Mã tài khoản",
            ["BookId"] = "Mã đầu sách",
            ["CopyId"] = "Mã bản sao",
            ["LoanId"] = "Mã phiếu mượn",
            ["ReservationId"] = "Mã đặt giữ",
            ["ProposalId"] = "Mã đề xuất",
            ["Username"] = "Tài khoản",
            ["FullName"] = "Họ và tên",
            ["Email"] = "Email",
            ["Phone"] = "Điện thoại",
            ["CardNo"] = "Mã thẻ",
            ["CardIssueDate"] = "Ngày cấp thẻ",
            ["CardExpiryDate"] = "Ngày hết hạn thẻ",
            ["CardStatus"] = "Trạng thái thẻ",
            ["BorrowLimit"] = "Giới hạn mượn",
            ["IsActive"] = "Hoạt động",
            ["Title"] = "Tên sách",
            ["BookTitle"] = "Tên sách",
            ["Author"] = "Tác giả",
            ["Isbn"] = "ISBN",
            ["Category"] = "Thể loại",
            ["Publisher"] = "Nhà xuất bản",
            ["PublishedYear"] = "Năm xuất bản",
            ["TotalCopies"] = "Tổng bản",
            ["AvailableCopies"] = "Bản sẵn sàng",
            ["ShelfLocation"] = "Vị trí kệ",
            ["Description"] = "Mô tả",
            ["CoverImageUrl"] = "Ảnh bìa",
            ["Barcode"] = "Barcode",
            ["Status"] = "Trạng thái",
            ["ReaderName"] = "Độc giả",
            ["ReaderCardNo"] = "Mã thẻ độc giả",
            ["LibrarianName"] = "Thủ thư",
            ["BorrowDate"] = "Ngày mượn",
            ["DueDate"] = "Hạn trả",
            ["ReturnDate"] = "Ngày trả",
            ["RenewCount"] = "Số lần gia hạn",
            ["Amount"] = "Số tiền",
            ["Reason"] = "Lý do",
            ["PaidAt"] = "Ngày thanh toán",
            ["CreatedAt"] = "Thời gian tạo",
            ["CreatedDate"] = "Ngày tạo",
            ["ExpiryDate"] = "Ngày hết hiệu lực",
            ["ReviewerName"] = "Người duyệt",
            ["ReviewedAt"] = "Ngày duyệt",
            ["ReviewNote"] = "Ghi chú duyệt",
            ["Key"] = "Khóa cấu hình",
            ["Value"] = "Giá trị",
            ["UpdatedAt"] = "Cập nhật lúc",
            ["Action"] = "Thao tác",
            ["TargetTable"] = "Bảng dữ liệu",
            ["TargetId"] = "Mã dữ liệu",
            ["Detail"] = "Chi tiết",
            ["LoanCount"] = "Lượt mượn"
        };

        foreach (DataGridViewColumn column in grid.Columns)
        {
            if (headers.TryGetValue(column.DataPropertyName, out var header))
                column.HeaderText = header;
            column.SortMode = DataGridViewColumnSortMode.Automatic;
            column.MinimumWidth = 80;
            if (column.DataPropertyName is "Description" or "Detail" or "ReviewNote")
                column.FillWeight = 180;
            else if (column.DataPropertyName is "FullName" or "BookTitle" or "Title" or "Email")
                column.FillWeight = 140;

            if (column.DataPropertyName.EndsWith("Date", StringComparison.Ordinal)
                || column.DataPropertyName.EndsWith("At", StringComparison.Ordinal))
                column.DefaultCellStyle.Format = column.DataPropertyName.EndsWith("At", StringComparison.Ordinal)
                    ? "dd/MM/yyyy HH:mm"
                    : "dd/MM/yyyy";
            if (column.DataPropertyName is "Amount" or "UnpaidFines" or "CollectedFines")
                column.DefaultCellStyle.Format = "N0";
        }

        grid.CellFormatting -= GridCellFormatting;
        grid.CellFormatting += GridCellFormatting;
    }

    private static void GridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (sender is DataGridView grid
            && grid.Columns[e.ColumnIndex].DataPropertyName == nameof(BookResponse.CoverImageUrl))
        {
            e.Value = string.IsNullOrWhiteSpace(e.Value?.ToString()) ? "Chưa có ảnh" : "Đã có ảnh";
            e.FormattingApplied = true;
            return;
        }

        if (e.Value is not string status)
            return;

        var normalized = status.ToLowerInvariant();
        if (normalized.Contains("overdue") || normalized.Contains("quá hạn")
            || normalized.Contains("inactive") || normalized.Contains("lost")
            || normalized.Contains("rejected") || normalized.Contains("đã hủy"))
        {
            e.CellStyle.BackColor = AdminTheme.DangerSurface;
            e.CellStyle.ForeColor = AdminTheme.Danger;
        }
        else if (normalized.Contains("pending") || normalized.Contains("maintenance")
            || normalized.Contains("chờ"))
        {
            e.CellStyle.BackColor = AdminTheme.WarningSurface;
            e.CellStyle.ForeColor = AdminTheme.Warning;
        }
        else if (normalized.Contains("available") || normalized.Contains("active")
            || normalized.Contains("borrowed") || normalized.Contains("ready")
            || normalized.Contains("paid") || normalized.Contains("approved"))
        {
            e.CellStyle.BackColor = AdminTheme.SuccessSurface;
            e.CellStyle.ForeColor = AdminTheme.Success;
        }
    }

    private static T? Selected<T>(DataGridView grid) where T : class =>
        grid.CurrentRow?.DataBoundItem as T;

    private readonly record struct PromptField(
        string Label,
        string Value,
        bool IsPassword,
        string[]? Options = null);

    private static string[]? Prompt(
        string title,
        params (string Label, string Value, bool IsPassword)[] fields) =>
        Prompt(
            title,
            fields.Select(field => new PromptField(field.Label, field.Value, field.IsPassword)).ToArray());

    private static string[]? Prompt(
        string title,
        params PromptField[] fields)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(600, 96 + fields.Length * 44),
            BackColor = CanvasColor,
            Font = new Font("Segoe UI", 9.5F)
        };
        AdminTheme.StyleForm(dialog);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 2,
            RowCount = fields.Length + 1,
            BackColor = CanvasColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var inputs = new List<Control>(fields.Length);
        for (var index = 0; index < fields.Length; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.Controls.Add(new Label
            {
                Text = fields[index].Label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = TextColor
            }, 0, index);

            Control input;
            if (fields[index].Options is { Length: > 0 } options)
            {
                var combo = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    DropDownStyle = ComboBoxStyle.DropDown,
                    BackColor = AdminTheme.SurfaceRaised,
                    ForeColor = TextColor,
                    Font = new Font("Segoe UI", 10F)
                };
                combo.Items.AddRange(options);
                combo.Text = fields[index].Value;
                input = combo;
            }
            else
            {
                var textBox = new TextBox
                {
                    Text = fields[index].Value,
                    UseSystemPasswordChar = fields[index].IsPassword,
                    Dock = DockStyle.Fill
                };
                AdminTheme.StyleTextBox(textBox);
                input = textBox;
            }

            inputs.Add(input);
            layout.Controls.Add(input, 1, index);
        }

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 5, 0, 0)
        };
        var ok = CreateActionButton("Lưu");
        ok.DialogResult = DialogResult.OK;
        var cancel = CreateActionButton("Hủy");
        cancel.DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(buttons, 0, fields.Length);
        layout.SetColumnSpan(buttons, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        return dialog.ShowDialog() == DialogResult.OK
            ? inputs.Select((input, index) =>
                fields[index].IsPassword ? input.Text : input.Text.Trim()).ToArray()
            : null;
    }

    private static string? NullIfBlank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryParseInt(string value, out int result) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

    private static bool TryParseYear(string value, out short? year)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            year = null;
            return true;
        }

        if (short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            year = parsed;
            return true;
        }

        year = null;
        return false;
    }

    private sealed class CopyManagementForm : Form
    {
        private readonly DataGridView _grid = CreateGrid();
        public BookCopyResponse? SelectedCopy => Selected<BookCopyResponse>(_grid);

        public CopyManagementForm(string bookTitle, IReadOnlyList<BookCopyResponse> copies)
        {
            AdminTheme.StyleForm(this);
            Text = $"Bản sao - {bookTitle}";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(700, 500);
            _grid.Dock = DockStyle.Fill;
            _grid.DataSource = copies.ToList();
            var buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                FlowDirection = FlowDirection.RightToLeft,
                BackColor = CanvasColor,
                Padding = new Padding(8, 5, 8, 4)
            };
            var choose = CreateActionButton("Chọn bản sao");
            choose.DialogResult = DialogResult.OK;
            var close = CreateActionButton("Đóng");
            close.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(choose);
            buttons.Controls.Add(close);
            Controls.Add(_grid);
            Controls.Add(buttons);
            AcceptButton = choose;
            CancelButton = close;
        }
    }
}
