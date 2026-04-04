using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Shell;

namespace Kexplorer.Core.Work;

/// <summary>
/// Loads the file list for a directory into the file grid.
/// Replaces legacy FileListWorkUnit.
/// </summary>
public sealed class FileListWorkItem : IWorkItem
{
    private readonly string _directoryPath;

    public FileListWorkItem(string directoryPath)
    {
        _directoryPath = directoryPath;
    }

    public string Name => $"FileList({_directoryPath})";

    public async Task ExecuteAsync(IKexplorerShell shell, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var files = DirectoryLoader.LoadFiles(_directoryPath);

        cancellationToken.ThrowIfCancellationRequested();

        await shell.SetFileListAsync(_directoryPath, files, cancellationToken);
    }
}
