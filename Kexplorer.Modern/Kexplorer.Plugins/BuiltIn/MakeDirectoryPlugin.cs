using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Make a new directory. Port of legacy MakeDirectoryScript.
/// </summary>
[FolderContext]
public sealed class MakeDirectoryPlugin : IFolderPlugin
{
    public string Name => "Make Directory";
    public string Description => "Create a new subfolder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var newName = await context.PromptAsync("Make Directory", "Folder Name:", "", cancellationToken);

        if (string.IsNullOrWhiteSpace(newName)) return;

        var newPath = Path.Combine(folderPath, newName);
        Directory.CreateDirectory(newPath);

        await context.RefreshFolderAsync(folderPath, cancellationToken);
    }
}
