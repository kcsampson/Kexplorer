using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Shell;

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

        var children = DirectoryLoader.LoadChildren(_folderPath, recurseDepth: 2);

        cancellationToken.ThrowIfCancellationRequested();

        await shell.SetTreeChildrenAsync(_folderPath, children, cancellationToken);
    }
}
