using Kexplorer.Core.FileSystem;

namespace Kexplorer.Core.Shell;

/// <summary>
/// The shell abstraction that work items use to communicate results back to the UI.
/// All methods are safe to call from any thread — the shell handles marshaling.
/// </summary>
public interface IKexplorerShell
{
  Task ReportStatusAsync(string message, CancellationToken cancellationToken = default);

  Task ReportErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default);

  Task RefreshPathAsync(string path, CancellationToken cancellationToken = default);

  /// <summary>
  /// Replace the child nodes of a tree node at the given path with the provided children.
  /// Called by folder/drive work items after loading directory contents.
  /// </summary>
  Task SetTreeChildrenAsync(string parentPath, IReadOnlyList<FileSystemNode> children, CancellationToken cancellationToken = default);

  /// <summary>
  /// Populate the file grid with the given file entries for a directory.
  /// Called by file-list work items after enumerating files.
  /// </summary>
  Task SetFileListAsync(string directoryPath, IReadOnlyList<FileEntry> files, CancellationToken cancellationToken = default);

  /// <summary>
  /// Select and expand a tree node at the given path (used for state restore / folder finder).
  /// </summary>
  Task NavigateToPathAsync(string path, CancellationToken cancellationToken = default);

  /// <summary>
  /// Remove a tree node from the view (e.g., hide a drive letter).
  /// </summary>
  Task RemoveTreeNodeAsync(string path, CancellationToken cancellationToken = default);
}
