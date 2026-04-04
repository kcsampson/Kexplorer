namespace Kexplorer.Core.FileSystem;

/// <summary>
/// Platform-independent representation of a directory tree node.
/// Replaces legacy KExplorerNode (which inherited WinForms TreeNode).
/// </summary>
public sealed class FileSystemNode
{
    public FileSystemNode(string name, string fullPath, bool isDirectory)
    {
        Name = name;
        FullPath = fullPath;
        IsDirectory = isDirectory;
    }

    public string Name { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }

    /// <summary>
    /// When true, the node's children are out of date and should be reloaded.
    /// Setting Stale cascades to all children (same behavior as legacy KExplorerNode).
    /// </summary>
    public bool Stale { get; set; }

    /// <summary>
    /// Whether child directories have been loaded at least once.
    /// </summary>
    public bool Loaded { get; set; }

    /// <summary>
    /// Child directory nodes. Populated by drive/folder work items.
    /// </summary>
    public List<FileSystemNode> Children { get; } = new();

    /// <summary>
    /// Mark this node and all descendants as stale.
    /// </summary>
    public void MarkStale()
    {
        Stale = true;
        foreach (var child in Children)
        {
            child.MarkStale();
        }
    }

    /// <summary>
    /// Extract the drive letter from the full path (e.g. "C" from "C:\folder").
    /// Returns null if the path doesn't start with a drive letter.
    /// </summary>
    public string? DriveLetter =>
        FullPath.Length >= 2 && FullPath[1] == ':'
            ? FullPath[..1]
            : null;
}
