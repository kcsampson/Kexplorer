using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Media file extensions recognized by viewer/player plugins.
/// </summary>
internal static class MediaExtensions
{
    public static readonly string[] Images = new[]
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", ".tif",
        ".webp", ".ico", ".svg", ".heic", ".heif", ".avif"
    };

    public static readonly string[] Videos = new[]
    {
        ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",
        ".mpg", ".mpeg", ".m4v", ".ts"
    };

    public static readonly string[] All = Images.Concat(Videos).ToArray();

    public static bool IsImage(string extension)
        => Images.Contains(extension, StringComparer.OrdinalIgnoreCase);

    public static bool IsVideo(string extension)
        => Videos.Contains(extension, StringComparer.OrdinalIgnoreCase);

    public static bool IsMedia(string extension)
        => All.Contains(extension, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Open image or video files in the built-in media viewer with next/prev navigation.
/// Port of legacy PictureViewerScript — modernized with a WPF viewer that
/// discovers all media files in the same directory for sequential browsing.
/// </summary>
[FileContext]
public sealed class MediaViewerPlugin : IFilePlugin
{
    public string Name => "View / Play Media";
    public string Description => "Open in the built-in media viewer with next/prev navigation";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsValidForFile(FileEntry file)
        => MediaExtensions.IsMedia(file.Extension);

    public async Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (selectedFiles.Count > 0)
        {
            await context.ShowFileViewerAsync(selectedFiles[0].FullPath, cancellationToken);
        }
    }
}
