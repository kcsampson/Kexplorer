using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Work;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Refresh the current folder (F5). Port of legacy RefreshScript.
/// </summary>
[FolderContext]
[FileContext]
public sealed class RefreshPlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Refresh";
    public string Description => "Refresh the current folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => new(PluginKey.F5);

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        await context.RefreshFolderAsync(folderPath, cancellationToken);
    }

    public async Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        await context.RefreshFolderAsync(folderPath, cancellationToken);
    }
}
