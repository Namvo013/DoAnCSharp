namespace LiMS.Admin.Forms;

internal static class AdminTheme
{
    public static readonly Color Canvas = Color.FromArgb(10, 16, 30);
    public static readonly Color Surface = Color.FromArgb(18, 27, 45);
    public static readonly Color SurfaceRaised = Color.FromArgb(25, 38, 60);
    public static readonly Color Sidebar = Color.FromArgb(7, 12, 24);
    public static readonly Color Border = Color.FromArgb(43, 59, 82);
    public static readonly Color Accent = Color.FromArgb(55, 214, 220);
    public static readonly Color AccentHover = Color.FromArgb(39, 177, 188);
    public static readonly Color Text = Color.FromArgb(230, 238, 249);
    public static readonly Color Muted = Color.FromArgb(143, 160, 183);
    public static readonly Color Warning = Color.FromArgb(255, 184, 107);
    public static readonly Color Danger = Color.FromArgb(255, 151, 164);
    public static readonly Color DangerSurface = Color.FromArgb(66, 34, 47);
    public static readonly Color Success = Color.FromArgb(119, 224, 193);
    public static readonly Color SuccessSurface = Color.FromArgb(28, 66, 59);
    public static readonly Color WarningSurface = Color.FromArgb(79, 63, 34);

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
        button.ForeColor = primary ? Canvas : Text;
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
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(31, 77, 93);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(21, 32, 51);
        grid.RowTemplate.Height = 36;
    }
}
