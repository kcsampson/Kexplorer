using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Cut selected files/folder paths. Port of legacy CutScript.
/// </summary>
[FolderContext]
[FileContext]
public sealed class CutPlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Edit - Cut";
    public string Description => "Cut selected files or folders";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var paths = selectedFiles.Select(f => f.FullPath).ToList();
        context.SetClipboardFiles(paths, isCut: true);
        context.Variables["CUTFILES"] = paths;
        context.Variables.Remove("COPYFILES");
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        context.SetClipboardFiles(new[] { folderPath }, isCut: true);
        context.Variables["CUTFILES"] = new List<string> { folderPath };
        context.Variables.Remove("COPYFILES");
        return Task.CompletedTask;
    }
}
