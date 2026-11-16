namespace LiMS.Admin.Forms;

internal static class AdminTheme
{
    public static readonly Color Canvas = Color.FromArgb(243, 247, 252);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceRaised = Color.FromArgb(239, 244, 250);
    public static readonly Color Sidebar = Color.FromArgb(17, 34, 57);
    public static readonly Color SidebarRaised = Color.FromArgb(31, 52, 79);
    public static readonly Color SidebarText = Color.FromArgb(205, 218, 234);
    public static readonly Color Border = Color.FromArgb(216, 226, 238);
    public static readonly Color Accent = Color.FromArgb(37, 99, 235);
    public static readonly Color AccentHover = Color.FromArgb(29, 78, 216);
    public static readonly Color Text = Color.FromArgb(23, 38, 60);
    public static readonly Color Muted = Color.FromArgb(91, 109, 132);
    public static readonly Color Warning = Color.FromArgb(146, 83, 0);
    public static readonly Color Danger = Color.FromArgb(180, 35, 24);
    public static readonly Color DangerSurface = Color.FromArgb(254, 226, 226);
    public static readonly Color Success = Color.FromArgb(22, 101, 52);
    public static readonly Color SuccessSurface = Color.FromArgb(220, 252, 231);
    public static readonly Color WarningSurface = Color.FromArgb(254, 243, 199);

    public static void StyleForm(Form form)
    {
        form.BackColor = Canvas;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 9F);
    }

    public static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.BackColor = primary ? Accent : SurfaceRaised;
        button.ForeColor = primary ? Color.White : Text;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.Padding = new Padding(12, 4, 12, 4);
        button.Cursor = Cursors.Hand;
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = primary ? AccentHover : Surface;
        button.FlatAppearance.MouseDownBackColor = AccentHover;
    }

    public static void StyleTextBox(TextBox textBox)
    {
        textBox.BackColor = SurfaceRaised;
        textBox.ForeColor = Text;
        textBox.BorderStyle = BorderStyle.FixedSingle;
        textBox.Font = new Font("Segoe UI", 10F);
    }

    public static void StyleGrid(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.BorderStyle = BorderStyle.None;
        grid.BackgroundColor = Surface;
        grid.GridColor = Border;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        grid.ColumnHeadersHeight = 42;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceRaised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceRaised;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Accent;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(246, 249, 253);
        grid.RowTemplate.Height = 40;
    }
}
