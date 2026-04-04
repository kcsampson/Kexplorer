using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Remove a drive or folder node from the tree view.
/// Useful for hiding drive letters you don't use (e.g. network drives, optical drives).
/// </summary>
[FolderContext]
public sealed class HideDrivePlugin : IFolderPlugin
{
    public string Name => "Hide from View";
    public string Description => "Remove this node from the tree view";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        await context.Shell.RemoveTreeNodeAsync(folderPath, cancellationToken);
    }
}
