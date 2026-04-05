using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Kexplorer.UI;

/// <summary>
/// Built-in media viewer with next/prev navigation through all media files
/// in the same directory. Supports images and video.
/// </summary>
public partial class MediaViewerWindow : Window
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif",
        ".webp", ".ico", ".heic", ".heif", ".avif"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
        ".mpg", ".mpeg", ".m4v", ".ts"
    };

    private readonly List<string> _mediaFiles = new();
    private int _currentIndex;

    public MediaViewerWindow(string filePath)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => ThemeManager.ApplyToWindow(this);
        BuildFileList(filePath);
        ShowCurrent();
    }

    private void BuildFileList(string selectedFile)
    {
        var dir = Path.GetDirectoryName(selectedFile);
        if (dir is null) return;

        var allMedia = Directory.EnumerateFiles(dir)
            .Where(f => IsMediaFile(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _mediaFiles.AddRange(allMedia);
        _currentIndex = _mediaFiles.FindIndex(f =>
            string.Equals(f, selectedFile, StringComparison.OrdinalIgnoreCase));

        if (_currentIndex < 0)
            _currentIndex = 0;
    }

    private void ShowCurrent()
    {
        if (_mediaFiles.Count == 0) return;

        var file = _mediaFiles[_currentIndex];
        var ext = Path.GetExtension(file);
        var fileName = Path.GetFileName(file);

        Title = $"{fileName} — Media Viewer";
        FileInfoText.Text = $"{fileName}  ({_currentIndex + 1} of {_mediaFiles.Count})";

        PrevButton.IsEnabled = _currentIndex > 0;
        NextButton.IsEnabled = _currentIndex < _mediaFiles.Count - 1;

        if (VideoExtensions.Contains(ext))
        {
            ShowVideo(file);
        }
        else
        {
            ShowImage(file);
        }
    }

    private void ShowImage(string filePath)
    {
        VideoDisplay.Stop();
        VideoDisplay.Visibility = Visibility.Collapsed;
        ImageDisplay.Visibility = Visibility.Visible;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();

        ImageDisplay.Source = bitmap;
    }

    private void ShowVideo(string filePath)
    {
        ImageDisplay.Source = null;
        ImageDisplay.Visibility = Visibility.Collapsed;
        VideoDisplay.Visibility = Visibility.Visible;

        VideoDisplay.Source = new Uri(filePath, UriKind.Absolute);
        VideoDisplay.Play();
    }

    private void Navigate(int delta)
    {
        var newIndex = _currentIndex + delta;
        if (newIndex < 0 || newIndex >= _mediaFiles.Count) return;

        _currentIndex = newIndex;
        ShowCurrent();
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e) => Navigate(-1);
    private void NextButton_Click(object sender, RoutedEventArgs e) => Navigate(1);

    private void VideoDisplay_MediaEnded(object sender, RoutedEventArgs e)
    {
        VideoDisplay.Stop();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                Navigate(-1);
                e.Handled = true;
                break;
            case Key.Right:
                Navigate(1);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Space when VideoDisplay.Visibility == Visibility.Visible:
                // Toggle play/pause (simple toggle via stop/play)
                VideoDisplay.Position = VideoDisplay.Position; // no-op to check state
                e.Handled = true;
                break;
        }
    }

    private static bool IsMediaFile(string extension)
        => ImageExtensions.Contains(extension) || VideoExtensions.Contains(extension);
}
