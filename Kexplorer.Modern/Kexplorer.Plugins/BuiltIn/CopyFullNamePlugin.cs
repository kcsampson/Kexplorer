using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Copy full file/folder path to clipboard. Port of legacy CopyToClipboardFullNameScript.
/// </summary>
[FolderContext]
[FileContext]
public sealed class CopyFullNamePlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Name - Full Name to Clipboard";
    public string Description => "Copy the full path to the clipboard";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (selectedFiles.Count == 1)
        {
            context.SetClipboardText(selectedFiles[0].FullPath);
        }
        else
        {
            context.SetClipboardText(string.Join(Environment.NewLine, selectedFiles.Select(f => f.FullPath)));
        }
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        context.SetClipboardText(folderPath);
        return Task.CompletedTask;
    }
}
