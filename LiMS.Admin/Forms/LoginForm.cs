using System.Drawing.Drawing2D;
using LiMS.Admin.Services;

namespace LiMS.Admin.Forms;

public sealed class LoginForm : Form
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LiMS");
    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "admin-settings.json");

    private readonly TextBox _apiAddress = new() { Text = LoadSavedApiAddress() };
    private readonly TextBox _username = new();
    private readonly TextBox _password = new() { UseSystemPasswordChar = true };
    private readonly Button _signIn = new() { Text = "Đăng nhập", AutoSize = true };
    private readonly Label _error = new() { ForeColor = Color.Firebrick, AutoSize = true };

    public LoginForm()
    {
        Text = "LiMS - Đăng nhập quản trị";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 520);
        MinimumSize = new Size(820, 520);
        MaximumSize = new Size(820, 520);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        BackColor = AdminTheme.Surface;
        DoubleBuffered = true;

        var brandPanel = new Panel
        {
            Dock = DockStyle.Left,
            Width = 310,
            BackColor = AdminTheme.Sidebar
        };
        brandPanel.Paint += PaintBrandPanel;

        brandPanel.Controls.AddRange([
            new Label
            {
                Text = "LiMS",
                Location = new Point(42, 92),
                Size = new Size(226, 74),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 34F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft
            },
            new Panel
            {
                Location = new Point(44, 166),
                Size = new Size(54, 4),
                BackColor = Color.FromArgb(125, 188, 143)
            },
            new Label
            {
                Text = "Thư viện thông minh",
                Location = new Point(42, 192),
                Size = new Size(230, 38),
                ForeColor = Color.FromArgb(232, 241, 235),
                Font = new Font("Segoe UI", 15F, FontStyle.Bold)
            },
            new Label
            {
                Text = "HỆ THỐNG QUẢN TRỊ",
                Location = new Point(44, 233),
                Size = new Size(220, 24),
                ForeColor = Color.FromArgb(160, 194, 171),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Bold)
            },
            new Label
            {
                Text = "Library Management System",
                Location = new Point(42, 436),
                Size = new Size(230, 28),
                ForeColor = Color.FromArgb(150, 180, 160),
                Font = new Font("Segoe UI", 8F)
            }
        ]);

        var formPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = AdminTheme.Surface,
            Padding = new Padding(50, 54, 50, 30)
        };

        var heading = new Label
        {
            Text = "Đăng nhập",
            Dock = DockStyle.Top,
            Height = 52,
            ForeColor = AdminTheme.Text,
            Font = new Font("Segoe UI", 24F, FontStyle.Bold),
            TextAlign = ContentAlignment.TopLeft
        };
        var description = new Label
        {
            Text = "Dành cho quản trị viên và thủ thư",
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = AdminTheme.Muted,
            Font = new Font("Segoe UI", 9.5F)
        };

        var fields = new Panel
        {
            Location = new Point(50, 146),
            Size = new Size(410, 220),
            BackColor = AdminTheme.Surface
        };
        fields.Controls.Add(CreateField("Địa chỉ máy chủ API", _apiAddress, 0));
        fields.Controls.Add(CreateField("Tài khoản", _username, 72));
        fields.Controls.Add(CreateField("Mật khẩu", _password, 144));

        var showPassword = new CheckBox
        {
            Text = "Hiện",
            Location = new Point(330, 144),
            Size = new Size(80, 22),
            ForeColor = AdminTheme.Muted,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };
        showPassword.CheckedChanged += (_, _) =>
            _password.UseSystemPasswordChar = !showPassword.Checked;
        fields.Controls.Add(showPassword);

        _signIn.Location = new Point(50, 380);
        _signIn.Size = new Size(410, 48);
        _signIn.Text = "Đăng nhập vào hệ thống";
        AdminTheme.StyleButton(_signIn, primary: true);
        _signIn.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _signIn.Click += async (_, _) => await SignInAsync();

        _error.Location = new Point(50, 432);
        _error.Size = new Size(410, 36);
        _error.AutoSize = false;
        _error.TextAlign = ContentAlignment.MiddleCenter;
        _error.ForeColor = AdminTheme.Danger;

        _password.KeyDown += async (_, args) =>
        {
            if (args.KeyCode != Keys.Enter)
                return;
            args.SuppressKeyPress = true;
            await SignInAsync();
        };

        formPanel.Controls.Add(_error);
        formPanel.Controls.Add(_signIn);
        formPanel.Controls.Add(fields);
        formPanel.Controls.Add(description);
        formPanel.Controls.Add(heading);

        Controls.Add(formPanel);
        Controls.Add(brandPanel);

        AcceptButton = _signIn;
        ActiveControl = _username;
    }

    private static void PaintBrandPanel(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel panel)
            return;

        using var brush = new LinearGradientBrush(
            panel.ClientRectangle,
            Color.FromArgb(22, 43, 32),
            Color.FromArgb(35, 82, 55),
            LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(brush, panel.ClientRectangle);

        using var circleBrush = new SolidBrush(Color.FromArgb(18, Color.White));
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillEllipse(circleBrush, 220, 350, 150, 150);
        e.Graphics.FillEllipse(circleBrush, -72, 405, 180, 180);
    }

    private static Panel CreateField(string caption, TextBox textBox, int top)
    {
        textBox.Dock = DockStyle.None;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.BackColor = Color.FromArgb(248, 250, 248);
        textBox.ForeColor = AdminTheme.Text;
        textBox.Font = new Font("Segoe UI", 10.5F);
        textBox.Location = new Point(0, 24);
        textBox.Size = new Size(410, 38);

        var field = new Panel
        {
            Location = new Point(0, top),
            Size = new Size(410, 62),
            BackColor = Color.Transparent
        };
        field.Controls.Add(new Label
        {
            Text = caption,
            Location = new Point(0, 0),
            Size = new Size(410, 20),
            ForeColor = AdminTheme.Text,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        });
        field.Controls.Add(textBox);
        return field;
    }

    private static string LoadSavedApiAddress()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var saved = File.ReadAllText(SettingsPath).Trim();
                if (!string.IsNullOrWhiteSpace(saved))
                    return saved;
            }
        }
        catch
        {
            // Bỏ qua lỗi đọc cấu hình, dùng địa chỉ mặc định.
        }

        return "http://localhost:5107";
    }

    private static void SaveApiAddress(string address)
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            File.WriteAllText(SettingsPath, address);
        }
        catch
        {
            // Không lưu được địa chỉ API cũng không ảnh hưởng việc đăng nhập.
        }
    }

    private async Task SignInAsync()
    {
        _error.Text = "";
        _signIn.Enabled = false;
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

            SaveApiAddress(address.ToString());
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
                _signIn.Enabled = true;
        }
    }
}
