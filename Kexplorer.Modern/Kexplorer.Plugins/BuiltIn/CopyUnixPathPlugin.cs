using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Copy full file/folder path with forward slashes to clipboard.
/// </summary>
[FolderContext]
[FileContext]
public sealed class CopyUnixPathPlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Name - Unix Path to Clipboard";
    public string Description => "Copy the full path with forward slashes to the clipboard";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (selectedFiles.Count == 1)
        {
            context.SetClipboardText(selectedFiles[0].FullPath.Replace('\\', '/'));
        }
        else
        {
            context.SetClipboardText(string.Join(Environment.NewLine, selectedFiles.Select(f => f.FullPath.Replace('\\', '/'))));
        }
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        context.SetClipboardText(folderPath.Replace('\\', '/'));
        return Task.CompletedTask;
    }
}
