using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace FileLockHotkey;

internal static class AppIcons
{
    private static readonly Lazy<Icon> LazyAppIcon = new(CreateAppIcon);

    public static Icon AppIcon => LazyAppIcon.Value;

    public static Bitmap CreateBitmap(int size)
    {
        return DrawIconBitmap(size);
    }

    private static Icon CreateAppIcon()
    {
        using var bitmap = DrawIconBitmap(64);
        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    private static Bitmap DrawIconBitmap(int size)
    {
        var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.Clear(Color.Transparent);

        var scale = size / 64F;
        var badgeBounds = ScaleRect(5, 5, 54, 54, scale);
        using (var badgeBrush = new LinearGradientBrush(
                   badgeBounds,
                   Color.FromArgb(24, 129, 124),
                   Color.FromArgb(40, 83, 148),
                   45F))
        {
            FillRoundedRectangle(graphics, badgeBrush, badgeBounds, 15F * scale);
        }

        using (var shadowBrush = new SolidBrush(Color.FromArgb(38, 0, 0, 0)))
        {
            FillRoundedRectangle(graphics, shadowBrush, ScaleRect(15, 25, 36, 24, scale), 6F * scale);
        }

        using (var tabBrush = new SolidBrush(Color.FromArgb(214, 237, 242)))
        {
            FillRoundedRectangle(graphics, tabBrush, ScaleRect(13, 19, 20, 12, scale), 5F * scale);
        }

        using (var folderBrush = new SolidBrush(Color.FromArgb(250, 252, 255)))
        {
            FillRoundedRectangle(graphics, folderBrush, ScaleRect(12, 24, 40, 24, scale), 7F * scale);
        }

        using (var shacklePen = new Pen(Color.FromArgb(255, 255, 255), 4F * scale))
        {
            shacklePen.StartCap = LineCap.Round;
            shacklePen.EndCap = LineCap.Round;
            graphics.DrawArc(shacklePen, ScaleRect(25, 21, 14, 18, scale), 190, 160);
        }

        using (var lockBrush = new SolidBrush(Color.FromArgb(255, 204, 73)))
        {
            FillRoundedRectangle(graphics, lockBrush, ScaleRect(23, 34, 18, 13, scale), 4F * scale);
        }

        using (var keyholeBrush = new SolidBrush(Color.FromArgb(93, 64, 34)))
        {
            graphics.FillEllipse(keyholeBrush, ScaleRect(30, 38, 4, 4, scale));
            graphics.FillRectangle(keyholeBrush, ScaleRect(31, 41, 2, 4, scale));
        }

        return bitmap;
    }

    private static RectangleF ScaleRect(float x, float y, float width, float height, float scale)
    {
        return new RectangleF(x * scale, y * scale, width * scale, height * scale);
    }

    private static void FillRoundedRectangle(Graphics graphics, Brush brush, RectangleF bounds, float radius)
    {
        using var path = new GraphicsPath();
        var diameter = radius * 2F;
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        graphics.FillPath(brush, path);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
