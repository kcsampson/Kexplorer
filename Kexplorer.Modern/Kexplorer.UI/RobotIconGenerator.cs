using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kexplorer.UI;

/// <summary>
/// Generates a Terminator-style endoskeleton head icon that follows the current theme colors.
/// </summary>
internal static class RobotIconGenerator
{
    public static BitmapSource CreateIcon(Application app)
    {
        // Use the primary foreground (gold in Kimbonics, dark in Standard) for the skull
        var fg = (app.TryFindResource("PrimaryForegroundBrush") as SolidColorBrush)?.Color ?? Colors.DodgerBlue;
        var bg = (app.TryFindResource("TitleBarBackgroundBrush") as SolidColorBrush)?.Color ?? Colors.White;
        // Eye glow uses accent
        var glow = (app.TryFindResource("AccentBrush") as SolidColorBrush)?.Color ?? Colors.Red;

        const int size = 64;
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var bgBrush = new SolidColorBrush(bg);
            var fgBrush = new SolidColorBrush(fg);
            var glowBrush = new SolidColorBrush(glow);
            var thinPen = new Pen(fgBrush, 1.8);
            var thickPen = new Pen(fgBrush, 2.5);

            // Background
            dc.DrawRectangle(bgBrush, null, new Rect(0, 0, size, size));

            // --- Skull outline (angular cranium) ---
            var skull = new StreamGeometry();
            using (var ctx = skull.Open())
            {
                ctx.BeginFigure(new Point(32, 4), false, true);   // top center
                ctx.LineTo(new Point(52, 14), true, true);        // upper right
                ctx.LineTo(new Point(54, 28), true, true);        // temple right
                ctx.LineTo(new Point(50, 40), true, true);        // cheekbone right
                ctx.LineTo(new Point(44, 46), true, true);        // jaw right
                ctx.LineTo(new Point(20, 46), true, true);        // jaw left
                ctx.LineTo(new Point(14, 40), true, true);        // cheekbone left
                ctx.LineTo(new Point(10, 28), true, true);        // temple left
                ctx.LineTo(new Point(12, 14), true, true);        // upper left
            }
            skull.Freeze();
            dc.DrawGeometry(null, thickPen, skull);

            // --- Brow ridge (heavy angular line) ---
            dc.DrawLine(thickPen, new Point(15, 20), new Point(32, 17));
            dc.DrawLine(thickPen, new Point(32, 17), new Point(49, 20));

            // --- Eye sockets (angular trapezoids) ---
            var leftEye = new StreamGeometry();
            using (var ctx = leftEye.Open())
            {
                ctx.BeginFigure(new Point(18, 23), true, true);
                ctx.LineTo(new Point(29, 22), true, true);
                ctx.LineTo(new Point(28, 32), true, true);
                ctx.LineTo(new Point(17, 31), true, true);
            }
            leftEye.Freeze();
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(60, glow.R, glow.G, glow.B)), thinPen, leftEye);

            var rightEye = new StreamGeometry();
            using (var ctx = rightEye.Open())
            {
                ctx.BeginFigure(new Point(35, 22), true, true);
                ctx.LineTo(new Point(46, 23), true, true);
                ctx.LineTo(new Point(47, 31), true, true);
                ctx.LineTo(new Point(36, 32), true, true);
            }
            rightEye.Freeze();
            dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(60, glow.R, glow.G, glow.B)), thinPen, rightEye);

            // --- Eye glow dots (menacing red/green pupils) ---
            dc.DrawEllipse(glowBrush, null, new Point(23, 27), 3, 3);
            dc.DrawEllipse(glowBrush, null, new Point(41, 27), 3, 3);

            // --- Nose cavity (small downward triangle) ---
            var nose = new StreamGeometry();
            using (var ctx = nose.Open())
            {
                ctx.BeginFigure(new Point(30, 33), false, true);
                ctx.LineTo(new Point(34, 33), true, true);
                ctx.LineTo(new Point(32, 38), true, true);
            }
            nose.Freeze();
            dc.DrawGeometry(null, thinPen, nose);

            // --- Teeth (metallic grill) ---
            dc.DrawLine(thinPen, new Point(23, 40), new Point(41, 40));
            dc.DrawLine(thinPen, new Point(22, 44), new Point(42, 44));
            // Vertical tooth dividers
            for (double x = 25; x <= 39; x += 3.5)
            {
                dc.DrawLine(new Pen(fgBrush, 1.2), new Point(x, 40), new Point(x, 44));
            }

            // --- Jaw hinge bolts ---
            dc.DrawEllipse(fgBrush, null, new Point(16, 42), 2, 2);
            dc.DrawEllipse(fgBrush, null, new Point(48, 42), 2, 2);

            // --- Neck / spine ---
            dc.DrawLine(thickPen, new Point(32, 46), new Point(32, 56));
            // Vertebrae notches
            dc.DrawLine(thinPen, new Point(28, 49), new Point(36, 49));
            dc.DrawLine(thinPen, new Point(29, 53), new Point(35, 53));

            // --- Shoulder plate line ---
            dc.DrawLine(thickPen, new Point(14, 58), new Point(50, 58));
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }
}
