using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FamilyModManager;

internal static class GameSkin
{
    internal static readonly Color Ink = Color.FromArgb(12, 12, 12);
    internal static readonly Color Field = Color.FromArgb(31, 31, 31);
    internal static readonly Color Ivory = Color.FromArgb(250, 250, 248);
    internal static readonly Color Muted = Color.FromArgb(190, 190, 188);
    private static readonly PrivateFontCollection Fonts = LoadFonts();
    private static GCHandle fontMemory;
    private static IntPtr gdiFont;
    private static readonly FontFamily Cursive = Fonts.Families.Single();
    internal static readonly Font Body = new(Cursive, 12, FontStyle.Regular);
    internal static readonly Font Heading = new(Cursive, 27, FontStyle.Regular);
    internal static readonly Font Small = new(Cursive, 10, FontStyle.Regular);
    internal static readonly Image DialogFrame = LoadBorder("frame.png");
    internal static readonly Image ControlFrame = Load("control-frame.png");

    private static Image Load(string name)
    {
        using Stream source = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"FamilyModManager.Assets.{name}")
            ?? throw new InvalidOperationException($"Missing UI artwork: {name}");
        return new Bitmap(source);
    }

    private static PrivateFontCollection LoadFonts()
    {
        var fonts = new PrivateFontCollection();
        using Stream source = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("FamilyModManager.Assets.NeutonCursive-Regular.ttf")
            ?? throw new InvalidOperationException("Missing bundled font.");
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        fontMemory = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        fonts.AddMemoryFont(fontMemory.AddrOfPinnedObject(), bytes.Length);
        uint fontCount = 0;
        gdiFont = AddFontMemResourceEx(fontMemory.AddrOfPinnedObject(),
            (uint)bytes.Length, IntPtr.Zero, ref fontCount);
        if (gdiFont == IntPtr.Zero || fontCount == 0)
            throw new InvalidOperationException("Could not load the bundled UI font.");
        return fonts;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr AddFontMemResourceEx(IntPtr font, uint size,
        IntPtr reserved, ref uint count);

    private static Image LoadBorder(string name)
    {
        using var source = (Bitmap)Load(name);
        var border = new Bitmap(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
            for (int x = 0; x < source.Width; x++)
            {
                Color pixel = source.GetPixel(x, y);
                int whiteness = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
                border.SetPixel(x, y, Color.FromArgb(pixel.A * whiteness / 255,
                    pixel.R, pixel.G, pixel.B));
            }
        return border;
    }

    internal static void DrawFrame(Graphics graphics, Image image, Rectangle bounds, int edge)
    {
        if (bounds.Width < edge * 2 || bounds.Height < edge * 2) return;
        graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
        int sx = image.Width / 3, sy = image.Height / 3;
        int[] sourceX = [0, sx, image.Width - sx];
        int[] sourceY = [0, sy, image.Height - sy];
        int[] sourceW = [sx, image.Width - sx * 2, sx];
        int[] sourceH = [sy, image.Height - sy * 2, sy];
        int[] targetX = [bounds.Left, bounds.Left + edge, bounds.Right - edge];
        int[] targetY = [bounds.Top, bounds.Top + edge, bounds.Bottom - edge];
        int[] targetW = [edge, bounds.Width - edge * 2, edge];
        int[] targetH = [edge, bounds.Height - edge * 2, edge];
        for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++)
                graphics.DrawImage(image,
                    new Rectangle(targetX[x], targetY[y], targetW[x], targetH[y]),
                    new Rectangle(sourceX[x], sourceY[y], sourceW[x], sourceH[y]),
                    GraphicsUnit.Pixel);
    }
}

internal sealed class GameButton : Button
{
    internal GameButton(string text, int width)
    {
        Text = text;
        Width = width;
        Height = 44;
        Margin = new Padding(3, 3, 3, 3);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Font = GameSkin.Small;
        ForeColor = GameSkin.Ivory;
        BackColor = GameSkin.Ink;
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        bool hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
        using var fill = new LinearGradientBrush(ClientRectangle,
            !Enabled ? Color.FromArgb(38, 38, 38) : hover ? Color.FromArgb(106, 106, 106) : Color.FromArgb(70, 70, 70),
            !Enabled ? Color.FromArgb(18, 18, 18) : hover ? Color.FromArgb(38, 38, 38) : Color.FromArgb(21, 21, 21),
            LinearGradientMode.Vertical);
        e.Graphics.FillRectangle(fill, ClientRectangle);
        using var border = new Pen(Enabled ? GameSkin.Ivory : GameSkin.Muted, 2);
        e.Graphics.DrawLine(border, 9, 5, Width - 10, 5);
        e.Graphics.DrawLine(border, 9, Height - 6, Width - 10, Height - 6);
        e.Graphics.DrawLine(border, 5, 9, 5, Height - 10);
        e.Graphics.DrawLine(border, Width - 6, 9, Width - 6, Height - 10);
        using var ornament = new SolidBrush(Enabled ? GameSkin.Ivory : GameSkin.Muted);
        Diamond(e.Graphics, ornament, 6, 6);
        Diamond(e.Graphics, ornament, Width - 7, 6);
        Diamond(e.Graphics, ornament, 6, Height - 7);
        Diamond(e.Graphics, ornament, Width - 7, Height - 7);
        using var shine = new Pen(Color.FromArgb(105, 255, 255, 242));
        e.Graphics.DrawLine(shine, 13, 10, Width - 14, 10);
        TextRenderer.DrawText(e.Graphics, Text, Font,
            new Rectangle(9, 11, Width - 18, Height - 22),
            Enabled ? GameSkin.Ivory : GameSkin.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }

    private static void Diamond(Graphics graphics, Brush brush, int x, int y) =>
        graphics.FillPolygon(brush, new Point[] { new(x, y - 5), new(x + 5, y),
            new(x, y + 5), new(x - 5, y) });

    protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); Invalidate(); }
    protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); Invalidate(); }
    protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
}
