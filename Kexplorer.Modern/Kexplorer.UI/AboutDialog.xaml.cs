using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Kexplorer.UI;

public partial class AboutDialog : Window
{
    private readonly Action<string> _onThemeChanged;
    private string _currentTheme;

    public AboutDialog(string currentTheme, Action<string> onThemeChanged)
    {
        InitializeComponent();

        _currentTheme = currentTheme;
        _onThemeChanged = onThemeChanged;

        // Load splash image from embedded resource
        var uri = new Uri("pack://application:,,,/Kimbonics;component/Abstract%20geometric%20male%20figure%20in%20stone.ico");
        SplashImage.Source = new BitmapImage(uri);

        // Populate theme list
        foreach (var theme in ThemeManager.Themes)
        {
            ThemeList.Items.Add(theme);
        }

        // Select the current theme
        for (int i = 0; i < ThemeList.Items.Count; i++)
        {
            if (string.Equals(ThemeList.Items[i] as string, currentTheme, StringComparison.OrdinalIgnoreCase))
            {
                ThemeList.SelectedIndex = i;
                break;
            }
        }
    }

    private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeList.SelectedItem is string selectedTheme
            && !string.Equals(selectedTheme, _currentTheme, StringComparison.OrdinalIgnoreCase))
        {
            _currentTheme = selectedTheme;
            _onThemeChanged(selectedTheme);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
