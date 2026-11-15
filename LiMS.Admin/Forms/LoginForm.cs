using LiMS.Admin.Services;

namespace LiMS.Admin.Forms;

public sealed class LoginForm : Form
{
    private static readonly Color Ink = Color.FromArgb(31, 41, 55);
    private static readonly Color Muted = Color.FromArgb(107, 114, 128);
    private static readonly Color Accent = Color.FromArgb(16, 145, 105);

    private readonly TextBox _apiAddress = new() { Text = "http://localhost:5107", Dock = DockStyle.Fill };
    private readonly TextBox _username = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly Button _signIn = new() { Text = "Đăng nhập hệ thống", Dock = DockStyle.Fill };
    private readonly Label _error = new()
    {
        ForeColor = Color.FromArgb(185, 55, 55),
        AutoSize = true,
        MaximumSize = new Size(380, 0)
    };

    public LoginForm()
    {
        Text = "LiMS - Đăng nhập quản trị";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(900, 600);
        ClientSize = new Size(1080, 700);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.Controls.Add(CreateWelcomePanel(), 0, 0);
        layout.Controls.Add(CreateLoginPanel(), 1, 0);
        Controls.Add(layout);

        _signIn.FlatStyle = FlatStyle.Flat;
        _signIn.FlatAppearance.BorderSize = 0;
        _signIn.BackColor = Accent;
        _signIn.ForeColor = Color.White;
        _signIn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _signIn.Cursor = Cursors.Hand;
        _signIn.FlatAppearance.MouseOverBackColor = Color.FromArgb(12, 122, 88);
        _signIn.Click += async (_, _) => await SignInAsync();
        _password.KeyDown += async (_, args) =>
        {
            if (args.KeyCode != Keys.Enter)
                return;
            args.SuppressKeyPress = true;
            await SignInAsync();
        };
        AcceptButton = _signIn;
    }

    private Control CreateWelcomePanel()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(20, 35, 54),
            Padding = new Padding(40)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            BackColor = panel.BackColor
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 14));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 8));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 29));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 17));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 12));
        layout.Controls.Add(CreateWelcomeLabel("LiMS", 30, Color.White, FontStyle.Bold), 0, 0);
        layout.Controls.Add(CreateWelcomeLabel("LIBRARY MANAGEMENT SYSTEM", 8.5F, Color.FromArgb(125, 220, 184), FontStyle.Bold), 0, 1);
        layout.Controls.Add(CreateWelcomeLabel("Không gian quản trị\r\nthư viện hiện đại", 25, Color.White, FontStyle.Bold), 0, 2);
        layout.Controls.Add(CreateWelcomeLabel("Quản lý kho sách, độc giả và hoạt động\r\nmượn trả trên cùng một hệ thống.", 10.5F, Color.FromArgb(194, 205, 218)), 0, 3);
        layout.Controls.Add(new Panel { Dock = DockStyle.Fill, BackColor = panel.BackColor }, 0, 4);
        layout.Controls.Add(CreateWelcomeLabel("BẢO MẬT  •  ĐỒNG BỘ  •  DỄ SỬ DỤNG", 8, Color.FromArgb(153, 170, 190), FontStyle.Bold), 0, 5);
        panel.Controls.Add(layout);
        return panel;
    }

    private static Label CreateWelcomeLabel(string text, float fontSize, Color color, FontStyle style = FontStyle.Regular) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = color,
        Font = new Font("Segoe UI", fontSize, style),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(0),
        Margin = new Padding(0)
    };

    private Control CreateLoginPanel()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 10,
            Padding = new Padding(54, 45, 54, 34),
            BackColor = Color.White
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        var heading = new Panel { Dock = DockStyle.Fill };
        heading.Controls.Add(new Label
        {
            Text = "Đăng nhập",
            AutoSize = true,
            ForeColor = Ink,
            Font = new Font("Segoe UI", 24, FontStyle.Bold),
            Location = new Point(0, 8)
        });
        heading.Controls.Add(new Label
        {
            Text = "Đăng nhập bằng tài khoản quản trị hoặc thủ thư.",
            AutoSize = true,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(3, 54)
        });
        layout.Controls.Add(heading, 0, 0);
        layout.Controls.Add(CreateFieldLabel("TÀI KHOẢN"), 0, 1);
        layout.Controls.Add(StyleInput(_username), 0, 2);
        layout.Controls.Add(CreateFieldLabel("MẬT KHẨU"), 0, 3);
        layout.Controls.Add(StyleInput(_password), 0, 4);
        layout.Controls.Add(CreateFieldLabel("ĐỊA CHỈ API"), 0, 5);
        layout.Controls.Add(StyleInput(_apiAddress), 0, 6);
        layout.Controls.Add(_error, 0, 7);
        layout.Controls.Add(_signIn, 0, 8);
        layout.Controls.Add(new Label
        {
            Text = "LiMS  •  Hệ thống quản lý thư viện",
            Dock = DockStyle.Fill,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.BottomLeft
        }, 0, 9);
        return layout;
    }

    private static Label CreateFieldLabel(string text) => new()
    {
        Text = text,
        Dock = DockStyle.Fill,
        ForeColor = Color.FromArgb(75, 85, 99),
        Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
        TextAlign = ContentAlignment.BottomLeft,
        Padding = new Padding(0, 0, 0, 5)
    };

    private static TextBox StyleInput(TextBox input)
    {
        input.Margin = new Padding(0, 2, 0, 8);
        input.BorderStyle = BorderStyle.FixedSingle;
        input.Font = new Font("Segoe UI", 10F);
        input.BackColor = Color.FromArgb(249, 250, 251);
        return input;
    }

    private async Task SignInAsync()
    {
        _error.Text = "";
        if (string.IsNullOrWhiteSpace(_username.Text) || string.IsNullOrWhiteSpace(_password.Text))
        {
            _error.Text = "Vui lòng nhập đầy đủ tài khoản và mật khẩu.";
            return;
        }

        _signIn.Enabled = false;
        _signIn.Text = "Đang xác thực...";
        try
        {
            if (!Uri.TryCreate(_apiAddress.Text.Trim(), UriKind.Absolute, out var address)
                || address.Scheme is not ("http" or "https"))
            {
                _error.Text = "Địa chỉ API không hợp lệ.";
                return;
            }

            var (client, user) = await AdminApiClient.SignInAsync(
                address,
                _username.Text.Trim(),
                _password.Text,
                CancellationToken.None);
            if (user.Role is not ("Admin" or "Librarian"))
            {
                client.Dispose();
                _error.Text = "Tài khoản không có quyền thủ thư/quản trị.";
                return;
            }

            Hide();
            using (var mainForm = new AdminMainForm(client, user))
                mainForm.ShowDialog(this);
            Close();
        }
        catch (AdminApiClient.AdminApiException exception)
        {
            _error.Text = exception.Message;
        }
        catch (HttpRequestException)
        {
            _error.Text = "Không kết nối được API. Hãy kiểm tra API và địa chỉ máy chủ.";
        }
        finally
        {
            if (!IsDisposed)
            {
                _signIn.Enabled = true;
                _signIn.Text = "Đăng nhập hệ thống";
            }
        }
    }
}
