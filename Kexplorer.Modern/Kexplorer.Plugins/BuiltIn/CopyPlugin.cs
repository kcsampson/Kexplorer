using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Copy selected files/folder paths to clipboard. Port of legacy CopyScript.
/// </summary>
[FolderContext]
[FileContext]
public sealed class CopyPlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Edit - Copy";
    public string Description => "Copy selected files or folders";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null; // Ctrl+C handled by WPF natively, but also available in context menu

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var paths = selectedFiles.Select(f => f.FullPath).ToList();
        context.SetClipboardFiles(paths, isCut: false);
        context.Variables["COPYFILES"] = paths;
        context.Variables.Remove("CUTFILES");
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        context.SetClipboardFiles(new[] { folderPath }, isCut: false);
        context.Variables["COPYFILES"] = new List<string> { folderPath };
        context.Variables.Remove("CUTFILES");
        return Task.CompletedTask;
    }
}
