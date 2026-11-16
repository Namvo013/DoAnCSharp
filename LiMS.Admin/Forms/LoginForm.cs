using LiMS.Admin.Services;

namespace LiMS.Admin.Forms;

public sealed class LoginForm : Form
{
    private readonly TextBox _apiAddress = new() { Text = "http://localhost:5107", Dock = DockStyle.Fill };
    private readonly TextBox _username = new() { Dock = DockStyle.Fill };
    private readonly TextBox _password = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly Button _signIn = new() { Text = "Đăng nhập", AutoSize = true };
    private readonly Label _error = new() { ForeColor = Color.Firebrick, AutoSize = true };

    public LoginForm()
    {
        Text = "LiMS - Đăng nhập quản trị";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 420);
        ClientSize = new Size(640, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        AdminTheme.StyleForm(this);

        AdminTheme.StyleTextBox(_apiAddress);
        AdminTheme.StyleTextBox(_username);
        AdminTheme.StyleTextBox(_password);

        var banner = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(36, 16, 36, 12),
            BackColor = AdminTheme.Surface
        };
        var brand = new Label
        {
            Text = "LiMS",
            Dock = DockStyle.Top,
            Height = 42,
            ForeColor = AdminTheme.Accent,
            Font = new Font("Segoe UI", 25F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        var subtitle = new Label
        {
            Text = "LIBRARY MANAGEMENT SYSTEM  /  ADMIN CONSOLE",
            Dock = DockStyle.Bottom,
            Height = 24,
            ForeColor = AdminTheme.Muted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        banner.Controls.Add(subtitle);
        banner.Controls.Add(brand);

        var fields = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(36, 20, 36, 12),
            BackColor = AdminTheme.Canvas
        };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        fields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        AddField(fields, 0, "API", _apiAddress);
        AddField(fields, 1, "Tài khoản", _username);
        AddField(fields, 2, "Mật khẩu", _password);

        _signIn.Click += async (_, _) => await SignInAsync();
        _password.KeyDown += async (_, args) =>
        {
            if (args.KeyCode != Keys.Enter)
                return;
            args.SuppressKeyPress = true;
            await SignInAsync();
        };

        var footer = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(36, 10, 36, 14),
            BackColor = AdminTheme.Canvas
        };
        _signIn.Text = "Đăng nhập vào hệ thống";
        _signIn.AutoSize = false;
        _signIn.Width = 544;
        _signIn.Height = 42;
        AdminTheme.StyleButton(_signIn, primary: true);
        _error.ForeColor = AdminTheme.Warning;
        _error.Padding = new Padding(2, 8, 0, 0);
        footer.Controls.Add(_signIn);
        footer.Controls.Add(_error);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = AdminTheme.Canvas
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
        layout.Controls.Add(banner, 0, 0);
        layout.Controls.Add(fields, 0, 1);
        layout.Controls.Add(footer, 0, 2);
        Controls.Add(layout);

        AcceptButton = _signIn;
    }

    private static void AddField(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = AdminTheme.Text,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        }, 0, row);
        layout.Controls.Add(control, 1, row);
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
