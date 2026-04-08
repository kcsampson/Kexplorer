using System.Windows;

namespace Kexplorer.UI;

public partial class PromptDialog : Window
{
    public string? ResponseText => InputBox.Text;

    public PromptDialog(string title, string message, string? defaultValue = null)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyToWindow(this);
        Title = title;
        MessageText.Text = message;
        if (defaultValue is not null)
            InputBox.Text = defaultValue;
        InputBox.Focus();
        InputBox.SelectAll();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }
}
