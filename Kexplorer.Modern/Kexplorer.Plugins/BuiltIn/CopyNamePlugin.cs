using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Copy short file/folder name to clipboard. Port of legacy CopyShortNameToClipboard.
/// </summary>
[FolderContext]
[FileContext]
public sealed class CopyNamePlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Name - Short Name to Clipboard";
    public string Description => "Copy the short name (not full path) to the clipboard";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (selectedFiles.Count == 1)
        {
            context.SetClipboardText(selectedFiles[0].Name);
        }
        else
        {
            context.SetClipboardText(string.Join(Environment.NewLine, selectedFiles.Select(f => f.Name)));
        }
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var name = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(name))
            name = folderPath; // Drive root like C:\
        context.SetClipboardText(name);
        return Task.CompletedTask;
    }
}
