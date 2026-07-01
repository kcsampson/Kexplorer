using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Shell;
using System.Diagnostics;

namespace Kexplorer.Core.Work;

/// <summary>
/// Loads subdirectories for a folder node (on expand or stale refresh).
/// Replaces legacy FolderWorkUnit.
/// </summary>
public sealed class FolderLoaderWorkItem : IWorkItem
{
    private readonly string _folderPath;

    public FolderLoaderWorkItem(string folderPath)
    {
        _folderPath = folderPath;
    }

    public string Name => $"LoadFolder({_folderPath})";

    public async Task ExecuteAsync(IKexplorerShell shell, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await shell.ReportStatusAsync($"Loading folder tree: {_folderPath}", cancellationToken);
        var sw = Stopwatch.StartNew();

        // Load only one level per expand. Eagerly preloading grandchildren can make
        // large providers (for example OneDrive) feel hung while recursion completes.
        var children = DirectoryLoader.LoadChildren(_folderPath, recurseDepth: 1);

        sw.Stop();

        cancellationToken.ThrowIfCancellationRequested();

        await shell.SetTreeChildrenAsync(_folderPath, children, cancellationToken);
        await shell.ReportStatusAsync(
            $"Loaded {children.Count} folders from {_folderPath} in {sw.ElapsedMilliseconds} ms",
            cancellationToken);
    }
}
