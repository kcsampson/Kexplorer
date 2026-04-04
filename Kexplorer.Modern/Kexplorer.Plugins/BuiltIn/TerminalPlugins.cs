using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Open a terminal/command prompt at the current folder. Port of legacy CommandPromptScript.
/// </summary>
[FolderContext]
public sealed class OpenTerminalPlugin : IFolderPlugin
{
    public string Name => "Open Terminal Here";
    public string Description => "Open a terminal window at the current folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        // Try Windows Terminal first, fall back to cmd
        try
        {
            context.RunProgram("wt", $"-d \"{folderPath}\"", folderPath);
        }
        catch
        {
            context.RunProgram("cmd", "/k", folderPath);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Open files/folders in the configured project editor.
/// Reads the editor command from launchers.json "projectEditor" field.
/// Defaults to "code" (VS Code) if not configured.
/// Examples: "code", "zed", "notepad++", "subl", "cursor"
/// </summary>
[FolderContext]
[FileContext]
public sealed class OpenInProjectEditorPlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Open in Project Editor";
    public string Description => "Open file or folder in the configured project editor";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var editor = context.Launcher.ProjectEditor;
        context.RunProgram(editor, $"\"{folderPath}\"", folderPath);
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var editor = context.Launcher.ProjectEditor;
        foreach (var file in selectedFiles)
        {
            context.RunProgram(editor, $"\"{file.FullPath}\"", folderPath);
        }
        return Task.CompletedTask;
    }
}
