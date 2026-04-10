using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace Kexplorer.UI;

public partial class TextViewerPanel : UserControl
{
    private string? _filePath;
    private bool _isDirty;
    private bool _suppressDirtyTracking;
    private Encoding _fileEncoding = Encoding.UTF8;
    private int _lastFindIndex = -1;

    public string? FilePath => _filePath;
    public bool IsWordWrap => WordWrapCheckBox.IsChecked == true;
    public bool IsEditing => EditModeCheckBox.IsChecked == true;

    public TextViewerPanel()
    {
        InitializeComponent();

        // Wire up keyboard shortcuts
        var saveBinding = new CommandBinding(ApplicationCommands.Save, (s, e) => SaveFile());
        var openBinding = new CommandBinding(ApplicationCommands.Open, (s, e) => OpenFileDialog());
        CommandBindings.Add(saveBinding);
        CommandBindings.Add(openBinding);

        KeyDown += TextViewerPanel_KeyDown;
    }

    public async Task InitializeAsync(string? filePath = null, bool? wordWrap = null, bool? isEditing = null)
    {
        if (wordWrap.HasValue)
            WordWrapCheckBox.IsChecked = wordWrap.Value;

        if (isEditing.HasValue)
            EditModeCheckBox.IsChecked = isEditing.Value;

        if (!string.IsNullOrEmpty(filePath))
            await LoadFileAsync(filePath);
    }

    public Task ShutdownAsync() => Task.CompletedTask;

    public async Task LoadFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            UpdateStatus($"File not found: {filePath}");
            return;
        }

        try
        {
            _suppressDirtyTracking = true;

            // Read with encoding detection
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = await reader.ReadToEndAsync();
            _fileEncoding = reader.CurrentEncoding;

            ContentTextBox.Text = content;
            _filePath = filePath;
            _isDirty = false;

            FilePathLabel.Text = filePath;
            RefreshButton.IsEnabled = true;
            EncodingLabel.Text = _fileEncoding.EncodingName;

            var fileInfo = new FileInfo(filePath);
            FileSizeLabel.Text = FormatFileSize(fileInfo.Length);
            ModifiedLabel.Text = "";

            UpdatePosition();
            _suppressDirtyTracking = false;

            // Render line numbers after layout settles
            Dispatcher.InvokeAsync(UpdateLineNumbers, System.Windows.Threading.DispatcherPriority.Background);
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error loading file: {ex.Message}");
        }
    }

    private void OpenFileDialog()
    {
        if (_isDirty && !ConfirmDiscardChanges())
            return;

        var dialog = new OpenFileDialog
        {
            Filter = "All Files (*.*)|*.*|Text Files (*.txt)|*.txt|Log Files (*.log)|*.log|" +
                     "XML Files (*.xml)|*.xml|JSON Files (*.json)|*.json|" +
                     "C# Files (*.cs)|*.cs|Config Files (*.config;*.ini;*.yaml;*.yml)|*.config;*.ini;*.yaml;*.yml",
            Title = "Open Text File"
        };

        if (!string.IsNullOrEmpty(_filePath))
            dialog.InitialDirectory = Path.GetDirectoryName(_filePath);

        if (dialog.ShowDialog() == true)
        {
            _ = LoadFileAsync(dialog.FileName);
        }
    }

    private async void SaveFile()
    {
        if (string.IsNullOrEmpty(_filePath)) return;
        if (!IsEditing) return;

        try
        {
            await File.WriteAllTextAsync(_filePath, ContentTextBox.Text, _fileEncoding);
            _isDirty = false;
            ModifiedLabel.Text = "";
            UpdateStatus($"Saved: {_filePath}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving file:\n{ex.Message}", "Save Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveFileAs()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "All Files (*.*)|*.*|Text Files (*.txt)|*.txt|Log Files (*.log)|*.log|" +
                     "XML Files (*.xml)|*.xml|JSON Files (*.json)|*.json|" +
                     "C# Files (*.cs)|*.cs",
            Title = "Save As"
        };

        if (!string.IsNullOrEmpty(_filePath))
        {
            dialog.InitialDirectory = Path.GetDirectoryName(_filePath);
            dialog.FileName = Path.GetFileName(_filePath);
        }

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await File.WriteAllTextAsync(dialog.FileName, ContentTextBox.Text, _fileEncoding);
                _filePath = dialog.FileName;
                _isDirty = false;
                FilePathLabel.Text = _filePath;
                ModifiedLabel.Text = "";
                RefreshButton.IsEnabled = true;

                var fileInfo = new FileInfo(_filePath);
                FileSizeLabel.Text = FormatFileSize(fileInfo.Length);
                UpdateStatus($"Saved: {_filePath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving file:\n{ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private bool ConfirmDiscardChanges()
    {
        var result = MessageBox.Show(
            "You have unsaved changes. Discard them?",
            "Unsaved Changes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return result == MessageBoxResult.Yes;
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e) => OpenFileDialog();

    private void SaveButton_Click(object sender, RoutedEventArgs e) => SaveFile();

    private void SaveAsButton_Click(object sender, RoutedEventArgs e) => SaveFileAs();

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_filePath)) return;
        if (_isDirty && !ConfirmDiscardChanges()) return;
        await LoadFileAsync(_filePath);
    }

    private void WordWrapChanged(object sender, RoutedEventArgs e)
    {
        if (ContentTextBox is null) return;
        ContentTextBox.TextWrapping = WordWrapCheckBox.IsChecked == true
            ? TextWrapping.Wrap
            : TextWrapping.NoWrap;
    }

    private void EditModeChanged(object sender, RoutedEventArgs e)
    {
        if (ContentTextBox is null) return;
        var editing = EditModeCheckBox.IsChecked == true;
        ContentTextBox.IsReadOnly = !editing;
        SaveButton.IsEnabled = editing;
        SaveAsButton.IsEnabled = editing;
    }

    private void ContentTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdatePosition();
    }

    private void ContentTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressDirtyTracking) return;
        if (!ContentTextBox.IsReadOnly && _filePath is not null)
        {
            _isDirty = true;
            ModifiedLabel.Text = "Modified";
        }
        UpdateLineNumbers();
    }

    private void ContentTextBox_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateLineNumbers();
    }

    private void UpdateLineNumbers()
    {
        LineNumberCanvas.Children.Clear();

        if (ContentTextBox.Text.Length == 0)
        {
            AddLineNumber(1, 0);
            return;
        }

        var fontFamily = ContentTextBox.FontFamily;
        var fontSize = ContentTextBox.FontSize;
        var foreground = TryFindResource("SecondaryForegroundBrush") as Brush ?? Brushes.Gray;
        var typeface = new Typeface(fontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        // Get the first visible character index and work from there
        var firstCharIndex = ContentTextBox.GetCharacterIndexFromLineIndex(0);
        if (firstCharIndex < 0) firstCharIndex = 0;

        var totalLines = ContentTextBox.LineCount;
        if (totalLines <= 0) totalLines = 1;

        // Resize gutter width based on digit count
        var digitCount = Math.Max(3, totalLines.ToString().Length);
        LineNumberCanvas.Width = digitCount * 9 + 12;

        // Find first visible line by scroll offset
        var firstVisibleLine = 0;
        for (var i = 0; i < totalLines; i++)
        {
            var charIndex = ContentTextBox.GetCharacterIndexFromLineIndex(i);
            if (charIndex < 0) continue;
            var rect = ContentTextBox.GetRectFromCharacterIndex(charIndex);
            if (rect.Top >= -fontSize)
            {
                firstVisibleLine = i;
                break;
            }
        }

        // Render visible line numbers
        var canvasHeight = LineNumberCanvas.ActualHeight;
        for (var i = firstVisibleLine; i < totalLines; i++)
        {
            var charIndex = ContentTextBox.GetCharacterIndexFromLineIndex(i);
            if (charIndex < 0) continue;

            var rect = ContentTextBox.GetRectFromCharacterIndex(charIndex);
            if (rect.Top > canvasHeight) break;

            AddLineNumber(i + 1, rect.Top);
        }
    }

    private void AddLineNumber(int lineNumber, double top)
    {
        var foreground = TryFindResource("SecondaryForegroundBrush") as Brush ?? Brushes.Gray;
        var tb = new TextBlock
        {
            Text = lineNumber.ToString(),
            FontFamily = ContentTextBox.FontFamily,
            FontSize = ContentTextBox.FontSize,
            Foreground = foreground,
            TextAlignment = TextAlignment.Right,
            Width = LineNumberCanvas.Width - 8,
            Padding = new Thickness(0, 0, 4, 0)
        };
        System.Windows.Controls.Canvas.SetTop(tb, top);
        System.Windows.Controls.Canvas.SetLeft(tb, 0);
        LineNumberCanvas.Children.Add(tb);
    }

    private void TextViewerPanel_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            SaveFileAs();
            e.Handled = true;
        }
        else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            SaveFile();
            e.Handled = true;
        }
        else if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control)
        {
            OpenFileDialog();
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowFindBar(showReplace: false);
            e.Handled = true;
        }
        else if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowFindBar(showReplace: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && FindBar.Visibility == Visibility.Visible)
        {
            CloseFindBar();
            e.Handled = true;
        }
        else if (e.Key == Key.F3 && Keyboard.Modifiers == ModifierKeys.Shift)
        {
            FindPrevious();
            e.Handled = true;
        }
        else if (e.Key == Key.F3)
        {
            FindNext();
            e.Handled = true;
        }
    }

    #region Find / Replace

    private void ShowFindBar(bool showReplace)
    {
        FindBar.Visibility = Visibility.Visible;
        ReplaceRow.Visibility = showReplace ? Visibility.Visible : Visibility.Collapsed;

        // Pre-populate with selected text
        if (ContentTextBox.SelectedText.Length > 0 && !ContentTextBox.SelectedText.Contains('\n'))
            FindTextBox.Text = ContentTextBox.SelectedText;

        FindTextBox.Focus();
        FindTextBox.SelectAll();
    }

    private void CloseFindBar()
    {
        FindBar.Visibility = Visibility.Collapsed;
        FindStatusLabel.Text = "";
        ContentTextBox.Focus();
    }

    private void CloseFindBar_Click(object sender, RoutedEventArgs e) => CloseFindBar();

    private void FindNext_Click(object sender, RoutedEventArgs e) => FindNext();

    private void FindPrev_Click(object sender, RoutedEventArgs e) => FindPrevious();

    private void FindTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
                FindPrevious();
            else
                FindNext();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFindBar();
            e.Handled = true;
        }
    }

    private void ReplaceTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ReplaceOnce();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseFindBar();
            e.Handled = true;
        }
    }

    private void FindNext()
    {
        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = ContentTextBox.Text;
        var comparison = MatchCaseCheckBox.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var startIndex = _lastFindIndex >= 0
            ? Math.Min(_lastFindIndex + 1, text.Length)
            : ContentTextBox.CaretIndex;

        var index = text.IndexOf(searchText, startIndex, comparison);

        // Wrap around
        if (index < 0 && startIndex > 0)
            index = text.IndexOf(searchText, 0, comparison);

        if (index >= 0)
        {
            ContentTextBox.Select(index, searchText.Length);
            ContentTextBox.Focus();
            _lastFindIndex = index;
            FindStatusLabel.Text = "";
        }
        else
        {
            _lastFindIndex = -1;
            FindStatusLabel.Text = "No matches found";
        }
    }

    private void FindPrevious()
    {
        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = ContentTextBox.Text;
        var comparison = MatchCaseCheckBox.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var startIndex = _lastFindIndex >= 0
            ? Math.Max(_lastFindIndex - 1, 0)
            : Math.Max(ContentTextBox.CaretIndex - 1, 0);

        var index = text.LastIndexOf(searchText, startIndex, comparison);

        // Wrap around
        if (index < 0)
            index = text.LastIndexOf(searchText, text.Length - 1, comparison);

        if (index >= 0)
        {
            ContentTextBox.Select(index, searchText.Length);
            ContentTextBox.Focus();
            _lastFindIndex = index;
            FindStatusLabel.Text = "";
        }
        else
        {
            _lastFindIndex = -1;
            FindStatusLabel.Text = "No matches found";
        }
    }

    private void ReplaceOne_Click(object sender, RoutedEventArgs e) => ReplaceOnce();

    private void ReplaceAll_Click(object sender, RoutedEventArgs e) => ReplaceAll();

    private void ReplaceOnce()
    {
        if (ContentTextBox.IsReadOnly) return;

        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var comparison = MatchCaseCheckBox.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        // If current selection matches the search text, replace it
        if (ContentTextBox.SelectedText.Length > 0
            && ContentTextBox.SelectedText.Equals(searchText, comparison))
        {
            var selStart = ContentTextBox.SelectionStart;
            ContentTextBox.SelectedText = ReplaceTextBox.Text;
            ContentTextBox.Select(selStart, ReplaceTextBox.Text.Length);
        }

        // Advance to next match
        FindNext();
    }

    private void ReplaceAll()
    {
        if (ContentTextBox.IsReadOnly) return;

        var searchText = FindTextBox.Text;
        if (string.IsNullOrEmpty(searchText)) return;

        var replaceText = ReplaceTextBox.Text;
        var comparison = MatchCaseCheckBox.IsChecked == true
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        var text = ContentTextBox.Text;
        var count = 0;
        var index = 0;
        var sb = new System.Text.StringBuilder();

        while (index < text.Length)
        {
            var found = text.IndexOf(searchText, index, comparison);
            if (found < 0)
            {
                sb.Append(text, index, text.Length - index);
                break;
            }
            sb.Append(text, index, found - index);
            sb.Append(replaceText);
            index = found + searchText.Length;
            count++;
        }

        if (count > 0)
        {
            ContentTextBox.Text = sb.ToString();
            FindStatusLabel.Text = $"Replaced {count} occurrence{(count != 1 ? "s" : "")}";
        }
        else
        {
            FindStatusLabel.Text = "No matches found";
        }
    }

    #endregion

    private void UpdatePosition()
    {
        var caretIndex = ContentTextBox.CaretIndex;
        var text = ContentTextBox.Text;

        var line = 1;
        var col = 1;
        for (var i = 0; i < caretIndex && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                col = 1;
            }
            else
            {
                col++;
            }
        }

        PositionLabel.Text = $"Ln {line}, Col {col}";
    }

    private void UpdateStatus(string message)
    {
        // Find MainWindow to update its status bar
        if (Window.GetWindow(this) is MainWindow mainWindow)
        {
            mainWindow.UpdateStatus(message);
        }
    }

    private static string FormatFileSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
