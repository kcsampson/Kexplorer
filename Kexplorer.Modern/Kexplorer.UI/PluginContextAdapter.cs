using System.Diagnostics;
using System.Windows;
using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Launching;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Shell;
using Kexplorer.Core.Work;

namespace Kexplorer.UI;

/// <summary>
/// WPF implementation of IPluginContext.
/// Bridges Core plugin API to WPF UI concerns (clipboard, dialogs, etc.).
/// </summary>
internal sealed class PluginContextAdapter : IPluginContext
{
    private readonly Dictionary<string, object> _variables = new();

    public PluginContextAdapter(IKexplorerShell shell, IWorkQueue workQueue, LauncherService launcher)
    {
        Shell = shell;
        WorkQueue = workQueue;
        Launcher = launcher;
    }

    public IKexplorerShell Shell { get; }
    public IWorkQueue WorkQueue { get; }
    public LauncherService Launcher { get; }
    public IDictionary<string, object> Variables => _variables;

    public string? GetClipboardText()
    {
        return Application.Current.Dispatcher.Invoke(() =>
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        });
    }

    public void SetClipboardText(string text)
    {
        Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text));
    }

    public IReadOnlyList<string>? GetClipboardFiles()
    {
        return Application.Current.Dispatcher.Invoke(() =>
        {
            if (!Clipboard.ContainsFileDropList())
                return null;

            var files = Clipboard.GetFileDropList();
            var result = new List<string>(files.Count);
            foreach (string? file in files)
            {
                if (file is not null)
                    result.Add(file);
            }
            return (IReadOnlyList<string>)result;
        });
    }

    public void SetClipboardFiles(IReadOnlyList<string> filePaths, bool isCut)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var collection = new System.Collections.Specialized.StringCollection();
            foreach (var path in filePaths)
                collection.Add(path);
            Clipboard.SetFileDropList(collection);
        });

        // Track cut vs copy in variables
        _variables["CLIPBOARD_IS_CUT"] = isCut;
    }

    public Task<string?> PromptAsync(string title, string message, string? defaultValue = null,
        CancellationToken cancellationToken = default)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new PromptDialog(title, message, defaultValue);
            var result = dialog.ShowDialog();
            return result == true ? dialog.ResponseText : null;
        }).Task;
    }

    public Task<bool> ConfirmAsync(string title, string message, CancellationToken cancellationToken = default)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }).Task;
    }

    public void RunProgram(string program, string arguments, string? workingDirectory = null, bool asAdmin = false)
    {
        var psi = new ProcessStartInfo
        {
            FileName = program,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? "",
            UseShellExecute = true
        };

        if (asAdmin)
        {
            psi.Verb = "runas";
        }

        Process.Start(psi);
    }

    public async Task RefreshFolderAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        await Shell.RefreshPathAsync(folderPath, cancellationToken);
        // Re-enqueue folder loading
        await WorkQueue.EnqueueAsync(new FolderLoaderWorkItem(folderPath), cancellationToken);
    }

    public Task<int> PasteClipboardContentsToFolderAsync(string destinationFolder, CancellationToken cancellationToken = default)
    {
        return ShellClipboardHelper.PasteToFolderAsync(
            destinationFolder,
            status => Application.Current.Dispatcher.InvokeAsync(
                () => Shell.ReportStatusAsync(status, cancellationToken)),
            cancellationToken);
    }

    public Task ShowFileViewerAsync(string filePath, CancellationToken cancellationToken = default)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var viewer = new MediaViewerWindow(filePath);
            viewer.Show();
        });
        return Task.CompletedTask;
    }
}
