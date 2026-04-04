using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Launching;
using Kexplorer.Core.Shell;
using Kexplorer.Core.Work;

namespace Kexplorer.Core.Plugins;

/// <summary>
/// Context object provided to plugins during execution.
/// Replaces legacy ScriptHelper — provides file-system, clipboard, notification, and work queue APIs.
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// The shell for reporting status, errors, and refreshing the UI.
    /// </summary>
    IKexplorerShell Shell { get; }

    /// <summary>
    /// The work queue for enqueuing background work.
    /// </summary>
    IWorkQueue WorkQueue { get; }

    /// <summary>
    /// The launcher service for opening files.
    /// </summary>
    LauncherService Launcher { get; }

    /// <summary>
    /// Get clipboard text content.
    /// </summary>
    string? GetClipboardText();

    /// <summary>
    /// Set clipboard text content.
    /// </summary>
    void SetClipboardText(string text);

    /// <summary>
    /// Get file paths from the clipboard (copy/cut files).
    /// </summary>
    IReadOnlyList<string>? GetClipboardFiles();

    /// <summary>
    /// Set file paths to clipboard.
    /// </summary>
    void SetClipboardFiles(IReadOnlyList<string> filePaths, bool isCut);

    /// <summary>
    /// Show a message/prompt to the user. Returns the user's response, or null if cancelled.
    /// </summary>
    Task<string?> PromptAsync(string title, string message, string? defaultValue = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Show a confirmation dialog. Returns true if confirmed.
    /// </summary>
    Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Run an external program.
    /// </summary>
    void RunProgram(string program, string arguments, string? workingDirectory = null, bool asAdmin = false);

    /// <summary>
    /// Refresh a folder in the tree (marks stale and re-enqueues loading).
    /// </summary>
    Task RefreshFolderAsync(string folderPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Open the built-in file viewer for the given file.
    /// The UI decides the appropriate viewer based on file type (e.g., media viewer with next/prev).
    /// </summary>
    Task ShowFileViewerAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Plugin-scoped variables (replaces ScriptHelper.VARS hashtable).
    /// Shared across plugins for inter-plugin communication (e.g., COPYFILES, CUTFILES).
    /// </summary>
    IDictionary<string, object> Variables { get; }
}
