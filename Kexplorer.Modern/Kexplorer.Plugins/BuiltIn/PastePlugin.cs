using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Paste files from clipboard. Port of legacy PasteScript (local file copy/move).
/// </summary>
[FolderContext]
public sealed class PastePlugin : IFolderPlugin
{
    public string Name => "Edit - Paste";
    public string Description => "Paste files from clipboard into the selected folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsValidForFolder(string folderPath) => true;

    public async Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        List<string>? sourcePaths = null;
        bool isCut = false;

        // Check variables first (inter-plugin communication), then clipboard
        if (context.Variables.TryGetValue("CUTFILES", out var cutObj) && cutObj is List<string> cutFiles)
        {
            sourcePaths = cutFiles;
            isCut = true;
        }
        else if (context.Variables.TryGetValue("COPYFILES", out var copyObj) && copyObj is List<string> copyFiles)
        {
            sourcePaths = copyFiles;
        }
        else
        {
            var clipboardFiles = context.GetClipboardFiles();
            if (clipboardFiles is not null)
            {
                sourcePaths = clipboardFiles.ToList();
                isCut = context.Variables.TryGetValue("CLIPBOARD_IS_CUT", out var isCutVal) && isCutVal is true;
            }
        }

        if (sourcePaths is null || sourcePaths.Count == 0)
        {
            await context.Shell.ReportStatusAsync("Nothing to paste.", cancellationToken);
            return;
        }

        var destDir = new DirectoryInfo(folderPath);
        if (!destDir.Exists)
        {
            await context.Shell.ReportErrorAsync($"Destination folder does not exist: {folderPath}", cancellationToken: cancellationToken);
            return;
        }

        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(sourcePath))
            {
                var destPath = Path.Combine(folderPath, Path.GetFileName(sourcePath));
                if (isCut)
                    File.Move(sourcePath, destPath, overwrite: false);
                else
                    File.Copy(sourcePath, destPath, overwrite: false);
            }
            else if (Directory.Exists(sourcePath))
            {
                var destPath = Path.Combine(folderPath, new DirectoryInfo(sourcePath).Name);
                if (isCut)
                    Directory.Move(sourcePath, destPath);
                else
                    CopyDirectory(sourcePath, destPath);
            }
        }

        // Clean up cut state
        if (isCut)
        {
            context.Variables.Remove("CUTFILES");
        }

        await context.RefreshFolderAsync(folderPath, cancellationToken);
        await context.Shell.ReportStatusAsync($"Pasted {sourcePaths.Count} item(s).", cancellationToken);
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(destDir, new DirectoryInfo(dir).Name));
        }
    }
}
