using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kexplorer.UI;

public static class ThemeManager
{
    private static readonly string[] AvailableThemes = { "Standard", "Kimbonics", "Kimbo-Slice" };

    // DWM attributes
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;   // Windows 11 22H2+
    private const int DWMWA_TEXT_COLOR = 36;       // Windows 11 22H2+

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public static IReadOnlyList<string> Themes => AvailableThemes;

    public static void ApplyTheme(string themeName)
    {
        var asmName = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name;
        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/{asmName};component/Themes/{themeName}.xaml")
        };

        var app = Application.Current;
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Contains("IsThemeDictionary"));
        if (existing != null)
            app.Resources.MergedDictionaries.Remove(existing);

        app.Resources.MergedDictionaries.Add(dict);

        // Resolve title bar colors from the theme
        bool useDarkTitleBar = themeName != "Standard";
        var captionBrush = app.TryFindResource("TitleBarBackgroundBrush") as SolidColorBrush;
        var textBrush = app.TryFindResource("TitleBarForegroundBrush") as SolidColorBrush;

        foreach (Window window in app.Windows)
        {
            SetDarkTitleBar(window, useDarkTitleBar);
            if (captionBrush is not null)
                SetTitleBarColor(window, captionBrush.Color);
            if (textBrush is not null)
                SetTitleBarTextColor(window, textBrush.Color);

            // Update window icon to match theme
            window.Icon = LoadAppIcon();
        }
    }

    /// <summary>
    /// Apply title bar styling to a newly opened window using the current theme resources.
    /// </summary>
    public static void ApplyToWindow(Window window)
    {
        var app = Application.Current;
        var captionBrush = app.TryFindResource("TitleBarBackgroundBrush") as SolidColorBrush;
        var textBrush = app.TryFindResource("TitleBarForegroundBrush") as SolidColorBrush;
        bool useDark = captionBrush is not null &&
                       (captionBrush.Color.R + captionBrush.Color.G + captionBrush.Color.B) < 384;

        SetDarkTitleBar(window, useDark);
        if (captionBrush is not null) SetTitleBarColor(window, captionBrush.Color);
        if (textBrush is not null) SetTitleBarTextColor(window, textBrush.Color);

        window.Icon = LoadAppIcon();
    }

    private static BitmapSource? _cachedIcon;

    private static BitmapSource? LoadAppIcon()
    {
        if (_cachedIcon is not null) return _cachedIcon;
        var exeDir = AppContext.BaseDirectory;
        var icoPath = Path.Combine(exeDir, "app.ico");
        if (File.Exists(icoPath))
        {
            var icon = new BitmapImage();
            icon.BeginInit();
            icon.UriSource = new Uri(icoPath, UriKind.Absolute);
            icon.CacheOption = BitmapCacheOption.OnLoad;
            icon.EndInit();
            icon.Freeze();
            _cachedIcon = icon;
            return _cachedIcon;
        }
        return null;
    }

    private static void SetDarkTitleBar(Window window, bool dark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int value = dark ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int));
        }
        catch { }
    }

    private static void SetTitleBarColor(Window window, Color color)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            // COLORREF is 0x00BBGGRR
            int colorRef = color.R | (color.G << 8) | (color.B << 16);
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref colorRef, sizeof(int));
        }
        catch { }
    }

    private static void SetTitleBarTextColor(Window window, Color color)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            int colorRef = color.R | (color.G << 8) | (color.B << 16);
            DwmSetWindowAttribute(hwnd, DWMWA_TEXT_COLOR, ref colorRef, sizeof(int));
        }
        catch { }
    }
}
