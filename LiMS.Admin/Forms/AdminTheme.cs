using System.Drawing.Drawing2D;

namespace LiMS.Admin.Forms;

internal static class AdminTheme
{
    public static readonly Color Canvas = Color.FromArgb(241, 248, 243);
    public static readonly Color Surface = Color.White;
    public static readonly Color SurfaceRaised = Color.FromArgb(247, 252, 248);
    public static readonly Color Sidebar = Color.FromArgb(23, 111, 54);
    public static readonly Color SidebarRaised = Color.FromArgb(27, 125, 61);
    public static readonly Color SidebarText = Color.FromArgb(240, 255, 243);
    public static readonly Color Border = Color.FromArgb(216, 233, 220);
    public static readonly Color Accent = Color.FromArgb(31, 125, 61);
    public static readonly Color AccentHover = Color.FromArgb(24, 105, 50);
    public static readonly Color Text = Color.FromArgb(28, 55, 36);
    public static readonly Color Muted = Color.FromArgb(94, 117, 101);
    public static readonly Color Warning = Color.FromArgb(146, 83, 0);
    public static readonly Color Danger = Color.FromArgb(180, 35, 24);
    public static readonly Color DangerSurface = Color.FromArgb(254, 226, 226);
    public static readonly Color Success = Color.FromArgb(23, 107, 59);
    public static readonly Color SuccessSurface = Color.FromArgb(228, 248, 233);
    public static readonly Color WarningSurface = Color.FromArgb(254, 243, 199);

    public static void StyleForm(Form form)
    {
        form.BackColor = Canvas;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 9F);
    }

    public static void StyleSidebarPanel(Panel panel)
    {
        panel.BackColor = Sidebar;
        panel.Paint += (_, args) =>
        {
            var bounds = panel.ClientRectangle;
            if (bounds.Width == 0 || bounds.Height == 0)
                return;

            var state = args.Graphics.Save();
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var gradient = new LinearGradientBrush(
                bounds,
                Color.FromArgb(25, 122, 61),
                Color.FromArgb(14, 94, 48),
                LinearGradientMode.ForwardDiagonal))
            {
                args.Graphics.FillRectangle(gradient, bounds);
            }

            var shineBounds = new Rectangle(0, 0, bounds.Width, Math.Min(bounds.Height, 250));
            using (var shine = new LinearGradientBrush(
                shineBounds,
                Color.FromArgb(6, Color.White),
                Color.FromArgb(0, Color.White),
                LinearGradientMode.Vertical))
            {
                args.Graphics.FillRectangle(shine, shineBounds);
            }

            PaintLibraryBotanical(
                args.Graphics,
                new Rectangle(bounds.Width - 92, 5, 82, 70),
                Color.FromArgb(48, Color.White));
            args.Graphics.Restore(state);
        };
    }

    public static void StyleSurfaceCard(Panel panel, Color accent)
    {
        panel.Paint += (_, args) =>
        {
            var bounds = panel.ClientRectangle;
            if (bounds.Width == 0 || bounds.Height == 0)
                return;

            var state = args.Graphics.Save();
            args.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var background = new LinearGradientBrush(
                bounds,
                Surface,
                Color.FromArgb(246, 252, 247),
                LinearGradientMode.ForwardDiagonal))
            {
                args.Graphics.FillRectangle(background, bounds);
            }

            using (var glow = new SolidBrush(Color.FromArgb(15, accent)))
            {
                args.Graphics.FillEllipse(
                    glow,
                    bounds.Width - 150,
                    -55,
                    190,
                    150);
            }

            var accentBounds = new Rectangle(1, 1, Math.Max(0, bounds.Width - 2), 3);
            if (accentBounds.Width > 0)
            {
                using var accentBrush = new LinearGradientBrush(
                    accentBounds,
                    Color.FromArgb(95, accent),
                    Color.FromArgb(12, accent),
                    LinearGradientMode.Horizontal);
                args.Graphics.FillRectangle(accentBrush, accentBounds);
            }

            using (var border = new Pen(Border))
            {
                args.Graphics.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
            }

            args.Graphics.Restore(state);
        };
    }

    public static void PaintLibraryBotanical(Graphics graphics, Rectangle bounds, Color lineColor)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var state = graphics.Save();
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var leafColor = Color.FromArgb(Math.Max(10, lineColor.A / 2), lineColor);
        var pageColor = Color.FromArgb(Math.Max(8, lineColor.A / 3), Color.White);
        using var leafBrush = new SolidBrush(leafColor);
        using var pageBrush = new SolidBrush(pageColor);
        using var linePen = new Pen(lineColor, Math.Max(1F, bounds.Width / 90F));

        var stemStart = new PointF(bounds.Left + bounds.Width * .22F, bounds.Bottom - bounds.Height * .2F);
        var stemEnd = new PointF(bounds.Left + bounds.Width * .62F, bounds.Top + bounds.Height * .08F);
        graphics.DrawBezier(
            linePen,
            stemStart,
            new PointF(bounds.Left + bounds.Width * .42F, bounds.Top + bounds.Height * .6F),
            new PointF(bounds.Left + bounds.Width * .48F, bounds.Top + bounds.Height * .32F),
            stemEnd);

        DrawLeaf(graphics, leafBrush, linePen, new PointF(bounds.Left + bounds.Width * .35F, bounds.Top + bounds.Height * .58F), bounds.Width * .38F, bounds.Height * .15F, -28F);
        DrawLeaf(graphics, leafBrush, linePen, new PointF(bounds.Left + bounds.Width * .59F, bounds.Top + bounds.Height * .43F), bounds.Width * .38F, bounds.Height * .15F, 30F);
        DrawLeaf(graphics, leafBrush, linePen, new PointF(bounds.Left + bounds.Width * .49F, bounds.Top + bounds.Height * .27F), bounds.Width * .33F, bounds.Height * .13F, -32F);

        var bookWidth = bounds.Width * .72F;
        var bookHeight = bounds.Height * .13F;
        var bookLeft = bounds.Left + bounds.Width * .12F;
        for (var index = 0; index < 3; index++)
        {
            var top = bounds.Bottom - (index + 1) * bookHeight * .92F;
            var points = new[]
            {
                new PointF(bookLeft + index * 3, top),
                new PointF(bookLeft + bookWidth, top + bookHeight * .08F),
                new PointF(bookLeft + bookWidth - index * 2, top + bookHeight * .82F),
                new PointF(bookLeft - index * 2, top + bookHeight * .72F)
            };
            graphics.FillPolygon(pageBrush, points);
            graphics.DrawPolygon(linePen, points);
            graphics.DrawLine(
                linePen,
                bookLeft + bookWidth * .16F,
                top + bookHeight * .32F,
                bookLeft + bookWidth * .78F,
                top + bookHeight * .38F);
        }

        graphics.Restore(state);
    }

    private static void DrawLeaf(
        Graphics graphics,
        Brush brush,
        Pen pen,
        PointF center,
        float width,
        float height,
        float angle)
    {
        var state = graphics.Save();
        graphics.TranslateTransform(center.X, center.Y);
        graphics.RotateTransform(angle);
        graphics.FillEllipse(brush, -width / 2, -height / 2, width, height);
        graphics.DrawEllipse(pen, -width / 2, -height / 2, width, height);
        graphics.DrawLine(pen, -width * .38F, 0, width * .38F, 0);
        graphics.Restore(state);
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
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        grid.ColumnHeadersDefaultCellStyle.BackColor = SurfaceRaised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 7, 6, 7);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceRaised;
        grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Accent;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(224, 240, 227);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 248);
        grid.RowTemplate.Height = 40;
    }
}
