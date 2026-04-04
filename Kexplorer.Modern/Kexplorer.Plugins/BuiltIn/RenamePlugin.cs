using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Rename a file or folder. Port of legacy RenameScript.
/// </summary>
[FolderContext]
[FileContext]
public sealed class RenamePlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Rename";
    public string Description => "Rename the selected file or folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (selectedFiles.Count != 1) return;

        var file = selectedFiles[0];
        var newName = await context.PromptAsync("Rename File", "New Name:", file.Name, cancellationToken);

        if (string.IsNullOrEmpty(newName) || newName == file.Name) return;

        var destPath = Path.Combine(Path.GetDirectoryName(file.FullPath)!, newName);
        File.Move(file.FullPath, destPath);

        await context.RefreshFolderAsync(folderPath, cancellationToken);
    }

    public async Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var dirInfo = new DirectoryInfo(folderPath);
        var newName = await context.PromptAsync("Rename Folder", "New Name:", dirInfo.Name, cancellationToken);

        if (string.IsNullOrEmpty(newName) || newName == dirInfo.Name) return;

        var destPath = Path.Combine(dirInfo.Parent!.FullName, newName);
        dirInfo.MoveTo(destPath);

        await context.RefreshFolderAsync(dirInfo.Parent!.FullName, cancellationToken);
    }
}
