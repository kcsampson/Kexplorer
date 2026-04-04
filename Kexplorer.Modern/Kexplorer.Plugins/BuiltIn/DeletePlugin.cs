using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Delete selected files or folder. Port of legacy DeleteScript.
/// </summary>
[FolderContext]
[FileContext]
public sealed class DeletePlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Edit - Delete";
    public string Description => "Delete selected file or folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => new(PluginKey.Delete);

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (selectedFiles.Count == 0) return;

        var names = string.Join(", ", selectedFiles.Select(f => f.Name).Take(5));
        if (selectedFiles.Count > 5)
            names += $", ... ({selectedFiles.Count} total)";

        if (!await context.ConfirmAsync("Delete Files", $"Delete {selectedFiles.Count} file(s)?\n{names}", cancellationToken))
            return;

        foreach (var file in selectedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(file.FullPath))
                File.Delete(file.FullPath);
        }

        await context.RefreshFolderAsync(folderPath, cancellationToken);
        await context.Shell.ReportStatusAsync($"Deleted {selectedFiles.Count} file(s).", cancellationToken);
    }

    public async Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (!await context.ConfirmAsync("Delete Folder", $"Delete folder?\n{folderPath}", cancellationToken))
            return;

        if (Directory.Exists(folderPath))
            Directory.Delete(folderPath, recursive: true);

        var parent = Path.GetDirectoryName(folderPath);
        if (parent is not null)
            await context.RefreshFolderAsync(parent, cancellationToken);
    }
}
