using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Open a file in the default text editor. Port of legacy TextPadFileScript.
/// Uses the launcher service's default mapping (typically Notepad++).
/// </summary>
[FileContext]
public sealed class OpenInEditorPlugin : IFilePlugin
{
    public string Name => "Open External Editor";
    public string Description => "Open the selected file in the default external text editor";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        foreach (var file in selectedFiles)
        {
            context.Launcher.Launch(file.FullPath);
        }
        return Task.CompletedTask;
    }
}
