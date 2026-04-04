using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Shell;

namespace Kexplorer.Core.Work;

/// <summary>
/// Loads a drive root's subdirectories into the tree.
/// Replaces legacy DriveLoaderWorkUnit.
/// </summary>
public sealed class DriveLoaderWorkItem : IWorkItem
{
    private readonly string _drivePath;

    public DriveLoaderWorkItem(string drivePath)
    {
        _drivePath = drivePath;
    }

    public string Name => $"LoadDrive({_drivePath})";

    public async Task ExecuteAsync(IKexplorerShell shell, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await shell.ReportStatusAsync($"Loading drive {_drivePath}...", cancellationToken);

        var children = DirectoryLoader.LoadChildren(_drivePath, recurseDepth: 2);

        cancellationToken.ThrowIfCancellationRequested();

        await shell.SetTreeChildrenAsync(_drivePath, children, cancellationToken);
        await shell.ReportStatusAsync($"Drive {_drivePath} loaded.", cancellationToken);
    }
}
