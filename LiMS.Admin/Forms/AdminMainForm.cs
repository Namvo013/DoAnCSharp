using System.Globalization;
using LiMS.Admin.Models;
using LiMS.Admin.Services;

namespace LiMS.Admin.Forms;

public sealed class AdminMainForm : Form
{
    private readonly AdminApiClient _api;
    private readonly LoginResponse _user;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _booksGrid = CreateGrid();
    private readonly DataGridView _copiesGrid = CreateGrid();
    private readonly DataGridView _readersGrid = CreateGrid();
    private readonly DataGridView _loansGrid = CreateGrid();
    private readonly DataGridView _finesGrid = CreateGrid();
    private readonly DataGridView _proposalsGrid = CreateGrid();
    private readonly DataGridView _settingsGrid = CreateGrid();
    private readonly DataGridView _logsGrid = CreateGrid();
    private string _bookSearch = "";
    private string _readerSearch = "";
    private readonly Label _totalBooksMetric = CreateMetricValue();
    private readonly Label _totalCopiesMetric = CreateMetricValue();
    private readonly Label _availableCopiesMetric = CreateMetricValue();
    private readonly Label _activeReadersMetric = CreateMetricValue();
    private readonly Label _activeLoansMetric = CreateMetricValue();
    private readonly Label _overdueLoansMetric = CreateMetricValue();
    private readonly Label _pendingReservationsMetric = CreateMetricValue();
    private readonly Label _pendingProposalsMetric = CreateMetricValue();
    private readonly Label _unpaidFinesMetric = CreateMetricValue();
    private readonly Label _collectedFinesMetric = CreateMetricValue();
    private readonly ListBox _popularBooks = new()
    {
        Dock = DockStyle.Fill,
        BackColor = AdminTheme.Surface,
        ForeColor = AdminTheme.Text,
        BorderStyle = BorderStyle.None,
        Font = new Font("Segoe UI", 10F),
        IntegralHeight = false
    };
    private readonly Control _dashboard;

    public AdminMainForm(AdminApiClient api, LoginResponse user)
    {
        _api = api;
        _user = user;
        _dashboard = CreateDashboardContent();
        FormClosed += (_, _) => _api.Dispose();
        AdminTheme.StyleForm(this);
        Text = "LiMS - Quản trị thư viện";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1050, 680);
        Size = new Size(1380, 850);

        _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        _tabs.SizeMode = TabSizeMode.Fixed;
        _tabs.ItemSize = new Size(136, 40);
        _tabs.DrawItem += DrawTab;
        BuildTabs();
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            Padding = new Padding(22, 12, 18, 10),
            BackColor = AdminTheme.Surface
        };
        var title = new Label
        {
            Text = $"LiMS  /  QUẢN TRỊ THƯ VIỆN     {_user.FullName}  ·  {_user.Role}",
            AutoSize = true,
            ForeColor = AdminTheme.Text,
            Font = new Font("Segoe UI", 12, FontStyle.Bold),
            Dock = DockStyle.Left
        };
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            BackColor = AdminTheme.Surface
        };
        var logout = new Button { Text = "Đăng xuất", AutoSize = true, Height = 36 };
        AdminTheme.StyleButton(logout);
        logout.Click += (_, _) => Close();
        var changePassword = new Button { Text = "Đổi mật khẩu", AutoSize = true, Height = 36 };
        AdminTheme.StyleButton(changePassword);
        changePassword.Click += async (_, _) => await RunAsync(ChangePasswordAsync);
        actions.Controls.Add(logout);
        actions.Controls.Add(changePassword);
        header.Controls.Add(actions);
        header.Controls.Add(title);
        Controls.Add(_tabs);
        Controls.Add(header);
        _tabs.SelectedIndexChanged += async (_, _) =>
        {
            if (_tabs.SelectedTab?.Tag is Func<Task> refresh)
                await RunAsync(refresh);
        };
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
        AddReadersPage();
        AddLoansPage();
        AddFinesPage();
        AddProposalsPage();
        if (_user.Role == "Admin")
        {
            AddSettingsPage();
            AddActivityLogsPage();
        }
    }

    private void AddDashboardPage()
    {
        var page = new TabPage("Tổng quan")
        {
            Tag = (Func<Task>)RefreshDashboardAsync,
            BackColor = AdminTheme.Canvas
        };
        page.Controls.Add(_dashboard);
        page.Controls.Add(CreateToolbar(("Làm mới", RefreshDashboardAsync)));
        _tabs.TabPages.Add(page);
    }

    private Control CreateDashboardContent()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = AdminTheme.Canvas,
            Padding = new Padding(18, 14, 18, 18),
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 230));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = AdminTheme.Canvas
        };
        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        heading.Controls.Add(new Label
        {
            Text = "TRUNG TÂM ĐIỀU HÀNH",
            Dock = DockStyle.Fill,
            ForeColor = AdminTheme.Text,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        heading.Controls.Add(new Label
        {
            Text = "Tình hình hoạt động thư viện theo thời gian thực",
            Dock = DockStyle.Fill,
            ForeColor = AdminTheme.Muted,
            Font = new Font("Segoe UI", 9.5F),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 1);
        layout.Controls.Add(heading, 0, 0);

        var metrics = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 2,
            BackColor = AdminTheme.Canvas
        };
        for (var column = 0; column < metrics.ColumnCount; column++)
            metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
        for (var row = 0; row < metrics.RowCount; row++)
            metrics.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        var metricCards = new (string Title, Label Value)[]
        {
            ("Đầu sách", _totalBooksMetric),
            ("Tổng bản sao", _totalCopiesMetric),
            ("Sẵn sàng", _availableCopiesMetric),
            ("Độc giả hoạt động", _activeReadersMetric),
            ("Phiếu mượn mở", _activeLoansMetric),
            ("Phiếu quá hạn", _overdueLoansMetric),
            ("Đặt giữ đang chờ", _pendingReservationsMetric),
            ("Đề xuất đang chờ", _pendingProposalsMetric),
            ("Phạt chưa thu", _unpaidFinesMetric),
            ("Đã thu", _collectedFinesMetric)
        };
        for (var index = 0; index < metricCards.Length; index++)
        {
            var (title, value) = metricCards[index];
            metrics.Controls.Add(CreateMetricCard(title, value), index % 5, index / 5);
        }
        layout.Controls.Add(metrics, 0, 1);

        var popularCard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(16, 10, 16, 12),
            BackColor = AdminTheme.Surface,
            Margin = new Padding(6, 8, 6, 4)
        };
        popularCard.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        popularCard.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        popularCard.Controls.Add(new Label
        {
            Text = "SÁCH ĐƯỢC MƯỢN NHIỀU",
            Dock = DockStyle.Fill,
            ForeColor = AdminTheme.Accent,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        popularCard.Controls.Add(_popularBooks, 0, 1);
        layout.Controls.Add(popularCard, 0, 2);

        return layout;
    }

    private static Panel CreateMetricCard(string title, Label value)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AdminTheme.Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(5)
        };
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12, 8, 8, 8),
            BackColor = AdminTheme.Surface
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 3));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var accent = new Panel { Dock = DockStyle.Fill, BackColor = AdminTheme.Accent };
        content.Controls.Add(accent, 0, 0);
        content.SetRowSpan(accent, 2);
        content.Controls.Add(new Label
        {
            Text = title.ToUpperInvariant(),
            Dock = DockStyle.Fill,
            ForeColor = AdminTheme.Muted,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        }, 1, 0);
        value.Dock = DockStyle.Fill;
        value.ForeColor = AdminTheme.Text;
        value.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
        value.TextAlign = ContentAlignment.MiddleLeft;
        content.Controls.Add(value, 1, 1);
        card.Controls.Add(content);
        return card;
    }

    private static Label CreateMetricValue() => new()
    {
        Text = "—",
        AutoEllipsis = true
    };

    private void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _tabs.TabPages.Count)
            return;

        var selected = (e.State & DrawItemState.Selected) != 0;
        using var background = new SolidBrush(selected ? AdminTheme.SurfaceRaised : AdminTheme.Canvas);
        e.Graphics.FillRectangle(background, e.Bounds);
        if (selected)
        {
            using var accent = new SolidBrush(AdminTheme.Accent);
            e.Graphics.FillRectangle(accent, e.Bounds.X, e.Bounds.Bottom - 3, e.Bounds.Width, 3);
        }

        TextRenderer.DrawText(
            e.Graphics,
            _tabs.TabPages[e.Index].Text,
            Font,
            e.Bounds,
            selected ? AdminTheme.Accent : AdminTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
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
            ("Ngừng kinh doanh", DeactivateBookAsync),
            ("Quản lý bản sao", ManageCopiesAsync),
            ("Thêm bản sao", AddCopiesAsync));
        _tabs.TabPages.Add(page);
    }

    private void AddReadersPage()
    {
        var page = CreateGridPage(
            "Độc giả",
            _readersGrid,
            LoadReadersAsync,
            ("Tìm độc giả", SearchReadersAsync),
            ("Thêm độc giả", CreateReaderAsync),
            ("Cập nhật thẻ", UpdateReaderAsync),
            ("Khóa thẻ/tài khoản", DeactivateReaderAsync));
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

    private TabPage CreateGridPage(
        string title,
        DataGridView grid,
        Func<Task> refresh,
        params (string Title, Func<Task> Action)[] actions)
    {
        var page = new TabPage(title) { Tag = refresh, BackColor = AdminTheme.Canvas };
        grid.Dock = DockStyle.Fill;
        page.Controls.Add(grid);
        var toolbarActions = new (string Title, Func<Task> Action)[actions.Length + 1];
        toolbarActions[0] = ("Làm mới", refresh);
        Array.Copy(actions, 0, toolbarActions, 1, actions.Length);
        page.Controls.Add(CreateToolbar(toolbarActions));
        return page;
    }

    private FlowLayoutPanel CreateToolbar(params (string Title, Func<Task> Action)[] actions)
    {
        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 48,
            Padding = new Padding(12, 6, 12, 4),
            WrapContents = false,
            AutoScroll = true,
            BackColor = AdminTheme.Surface
        };

        foreach (var (title, action) in actions)
        {
            var button = new Button
            {
                Text = title,
                AutoSize = true,
                Height = 34,
                Margin = new Padding(4, 0, 4, 0)
            };
            var primary = title.StartsWith("Thêm ", StringComparison.Ordinal)
                || title.StartsWith("Cho mượn", StringComparison.Ordinal)
                || title.StartsWith("Ghi nhận", StringComparison.Ordinal)
                || title.StartsWith("Duyệt", StringComparison.Ordinal);
            AdminTheme.StyleButton(button, primary);
            button.Click += async (_, _) => await RunAsync(action);
            toolbar.Controls.Add(button);
        }

        return toolbar;
    }

    private async Task RefreshDashboardAsync()
    {
        var data = await _api.GetDashboardAsync(CancellationToken.None);
        _totalBooksMetric.Text = data.TotalBooks.ToString("N0");
        _totalCopiesMetric.Text = data.TotalCopies.ToString("N0");
        _availableCopiesMetric.Text = data.AvailableCopies.ToString("N0");
        _activeReadersMetric.Text = data.ActiveReaders.ToString("N0");
        _activeLoansMetric.Text = data.ActiveLoans.ToString("N0");
        _overdueLoansMetric.Text = data.OverdueLoans.ToString("N0");
        _pendingReservationsMetric.Text = data.PendingReservations.ToString("N0");
        _pendingProposalsMetric.Text = data.PendingProposals.ToString("N0");
        _unpaidFinesMetric.Text = $"{data.UnpaidFines:N0} ₫";
        _collectedFinesMetric.Text = $"{data.CollectedFines:N0} ₫";
        _popularBooks.BeginUpdate();
        try
        {
            _popularBooks.Items.Clear();
            foreach (var (book, index) in data.PopularBooks.Select((book, index) => (book, index)))
                _popularBooks.Items.Add($"{index + 1:00}   {book.Title}    ·    {book.LoanCount:N0} lượt");
            if (_popularBooks.Items.Count == 0)
                _popularBooks.Items.Add("Chưa có dữ liệu mượn sách.");
        }
        finally
        {
            _popularBooks.EndUpdate();
        }
    }

    private async Task ChangePasswordAsync()
    {
        var values = Prompt("Đổi mật khẩu",
            ("Mật khẩu hiện tại", "", true),
            ("Mật khẩu mới (ít nhất 8 ký tự)", "", true));
        if (values is null || values[1].Length < 8)
            return;

        var result = await _api.ChangePasswordAsync(
            new ChangePasswordRequest(values[0], values[1]),
            CancellationToken.None);
        MessageBox.Show(this, result.Message, "Tài khoản",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task LoadBooksAsync() =>
        _booksGrid.DataSource = (await _api.GetBooksAsync(_bookSearch, CancellationToken.None)).ToList();

    private async Task LoadReadersAsync() =>
        _readersGrid.DataSource = (await _api.GetReadersAsync(_readerSearch, CancellationToken.None)).ToList();

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
        var values = Prompt("Tìm độc giả", ("Mã thẻ, tài khoản, họ tên hoặc email", _readerSearch, false));
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

    private async Task CreateBookAsync()
    {
        var values = Prompt("Thêm đầu sách",
            ("Danh mục", "Chưa phân loại", false),
            ("Tên sách", "", false),
            ("Tác giả", "", false),
            ("ISBN", "", false),
            ("Nhà xuất bản", "", false),
            ("Năm xuất bản", "", false),
            ("Vị trí kệ", "", false),
            ("Mô tả", "", false),
            ("Số bản sao ban đầu", "0", false));
        if (values is null || !TryParseInt(values[8], out var copies) || !TryParseYear(values[5], out var year))
            return;

        await _api.CreateBookAsync(
            new CreateBookRequest(values[0], values[1], values[2], NullIfBlank(values[3]),
                NullIfBlank(values[4]), year, NullIfBlank(values[6]), NullIfBlank(values[7]), copies),
            CancellationToken.None);
        await LoadBooksAsync();
    }

    private async Task EditBookAsync()
    {
        var book = Selected<BookResponse>(_booksGrid);
        if (book is null)
            return;
        var values = Prompt("Cập nhật đầu sách",
            ("Danh mục", book.Category, false),
            ("Tên sách", book.Title, false),
            ("Tác giả", book.Author, false),
            ("ISBN", book.Isbn ?? "", false),
            ("Nhà xuất bản", book.Publisher ?? "", false),
            ("Năm xuất bản", book.PublishedYear?.ToString() ?? "", false),
            ("Vị trí kệ", book.ShelfLocation ?? "", false),
            ("Mô tả", book.Description ?? "", false),
            ("Đang kinh doanh (true/false)", "true", false));
        if (values is null || !TryParseYear(values[5], out var year) || !bool.TryParse(values[8], out var isActive))
            return;

        await _api.UpdateBookAsync(
            book.Id,
            new UpdateBookRequest(values[0], values[1], values[2], NullIfBlank(values[3]),
                NullIfBlank(values[4]), year, NullIfBlank(values[6]), NullIfBlank(values[7]), isActive),
            CancellationToken.None);
        await LoadBooksAsync();
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
            ("Barcode", form.SelectedCopy.Barcode, false),
            ("Trạng thái (Available/Maintenance/Lost)", form.SelectedCopy.Status, false));
        if (values is null)
            return;
        await _api.UpdateCopyStatusAsync(
            form.SelectedCopy.Id,
            new CopyStatusRequest(values[1]),
            CancellationToken.None);
        await LoadBooksAsync();
    }

    private async Task AddCopiesAsync()
    {
        var book = Selected<BookResponse>(_booksGrid);
        if (book is null)
            return;
        var values = Prompt("Thêm bản sao",
            ("Tiền tố barcode", $"B{book.Id:D6}", false),
            ("Số lượng", "1", false));
        if (values is null || !TryParseInt(values[1], out var quantity))
            return;
        await _api.AddCopiesAsync(
            book.Id,
            new CreateCopiesRequest(quantity, values[0]),
            CancellationToken.None);
        await LoadBooksAsync();
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
        if (values is null
            || !short.TryParse(values[6], out var borrowLimit)
            || !TryParseInt(values[7], out var validityMonths))
            return;

        await _api.CreateReaderAsync(
            new CreateReaderRequest(values[0], values[1], values[2], values[3],
                NullIfBlank(values[4]), values[5], borrowLimit, validityMonths),
            CancellationToken.None);
        await LoadReadersAsync();
    }

    private async Task UpdateReaderAsync()
    {
        var reader = Selected<ReaderResponse>(_readersGrid);
        if (reader is null)
            return;
        var values = Prompt("Cập nhật độc giả",
            ("Họ tên", reader.FullName, false),
            ("Email", reader.Email, false),
            ("Điện thoại", reader.Phone ?? "", false),
            ("Trạng thái thẻ (Active/Inactive)", reader.CardStatus, false),
            ("Giới hạn mượn", reader.BorrowLimit.ToString(), false),
            ("Ngày hết hạn (yyyy-MM-dd)", reader.CardExpiryDate.ToString("yyyy-MM-dd"), false),
            ("Tài khoản hoạt động (true/false)", reader.IsActive.ToString(), false));
        if (values is null
            || !short.TryParse(values[4], out var borrowLimit)
            || !DateOnly.TryParse(values[5], CultureInfo.InvariantCulture, out var expiry)
            || !bool.TryParse(values[6], out var isActive))
            return;

        await _api.UpdateReaderAsync(
            reader.ReaderId,
            new UpdateReaderRequest(values[0], values[1], NullIfBlank(values[2]),
                values[3], borrowLimit, expiry, isActive),
            CancellationToken.None);
        await LoadReadersAsync();
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
    }

    private async Task CheckoutAsync()
    {
        var values = Prompt("Cho mượn sách",
            ("Mã thẻ độc giả", "", false),
            ("Barcode bản sách", "", false),
            ("Mã đặt giữ (nếu có)", "", false));
        if (values is null)
            return;
        long? reservationId = null;
        if (!string.IsNullOrWhiteSpace(values[2]))
        {
            if (!long.TryParse(values[2], out var parsed))
                return;
            reservationId = parsed;
        }
        await _api.CheckoutAsync(
            new CheckoutRequest(values[0], values[1], reservationId),
            CancellationToken.None);
        await LoadLoansAsync();
    }

    private async Task ReturnAsync()
    {
        var values = Prompt("Nhận trả sách",
            ("Barcode bản sách", "", false),
            ("Phạt bổ sung (VND, nếu có)", "0", false),
            ("Lý do phạt bổ sung", "", false));
        if (values is null || !decimal.TryParse(values[1], out var fine))
            return;
        await _api.ReturnAsync(
            new ReturnRequest(values[0], NullIfBlank(values[2]), fine),
            CancellationToken.None);
        await LoadLoansAsync();
        await LoadFinesAsync();
    }

    private async Task RenewAsync()
    {
        var loan = Selected<LoanResponse>(_loansGrid);
        if (loan is null)
            return;
        await _api.RenewAsync(loan.Id, CancellationToken.None);
        await LoadLoansAsync();
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
        if (values is null || !decimal.TryParse(values[1], out var amount))
            return;
        await _api.CollectFineAsync(
            fine.Id,
            new CollectFineRequest(amount, string.IsNullOrWhiteSpace(values[2]) ? "Thu tại quầy" : values[2]),
            CancellationToken.None);
        await LoadFinesAsync();
        await RefreshDashboardAsync();
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
    }

    private static DataGridView CreateGrid()
    {
        var grid = new DataGridView
        {
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            RowHeadersVisible = false
        };
        AdminTheme.StyleGrid(grid);
        return grid;
    }

    private static T? Selected<T>(DataGridView grid) where T : class =>
        grid.CurrentRow?.DataBoundItem as T;

    private static string[]? Prompt(
        string title,
        params (string Label, string Value, bool IsPassword)[] fields)
    {
        using var dialog = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ShowInTaskbar = false,
            ClientSize = new Size(560, 90 + fields.Length * 40)
        };
        AdminTheme.StyleForm(dialog);
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = fields.Length + 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var inputs = new List<TextBox>(fields.Length);
        for (var index = 0; index < fields.Length; index++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.Controls.Add(new Label
            {
                Text = fields[index].Label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                ForeColor = AdminTheme.Text
            }, 0, index);
            var input = new TextBox
            {
                Text = fields[index].Value,
                UseSystemPasswordChar = fields[index].IsPassword,
                Dock = DockStyle.Fill
            };
            AdminTheme.StyleTextBox(input);
            inputs.Add(input);
            layout.Controls.Add(input, 1, index);
        }

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var ok = new Button { Text = "Lưu", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, AutoSize = true };
        AdminTheme.StyleButton(ok, primary: true);
        AdminTheme.StyleButton(cancel);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.Controls.Add(buttons, 0, fields.Length);
        layout.SetColumnSpan(buttons, 2);
        dialog.Controls.Add(layout);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        return dialog.ShowDialog() == DialogResult.OK
            ? inputs.Select(input => input.Text.Trim()).ToArray()
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
                FlowDirection = FlowDirection.RightToLeft
            };
            var select = new Button { Text = "Chọn bản sao", DialogResult = DialogResult.OK, AutoSize = true };
            var close = new Button { Text = "Đóng", DialogResult = DialogResult.Cancel, AutoSize = true };
            AdminTheme.StyleButton(select, primary: true);
            AdminTheme.StyleButton(close);
            buttons.Controls.Add(select);
            buttons.Controls.Add(close);
            Controls.Add(_grid);
            Controls.Add(buttons);
            AcceptButton = (Button)buttons.Controls[0];
            CancelButton = (Button)buttons.Controls[1];
        }
    }
}
