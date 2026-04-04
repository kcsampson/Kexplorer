namespace Kexplorer.Core.FileSystem;

/// <summary>
/// Represents a single file in the file grid.
/// Replaces the legacy DataRow-based approach (DataTable with Name, Size, Modified columns).
/// </summary>
public sealed class FileEntry
{
    public FileEntry(string name, string fullPath, long size, DateTime lastModified, string extension)
    {
        Name = name;
        FullPath = fullPath;
        Size = size;
        LastModified = lastModified;
        Extension = extension;
    }

    public string Name { get; }
    public string FullPath { get; }
    public long Size { get; }
    public DateTime LastModified { get; }
    public string Extension { get; }

    /// <summary>
    /// Create a FileEntry from a FileInfo.
    /// </summary>
    public static FileEntry FromFileInfo(FileInfo fi)
    {
        return new FileEntry(
            fi.Name,
            fi.FullName,
            fi.Exists ? fi.Length : 0,
            fi.Exists ? fi.LastWriteTime : DateTime.MinValue,
            fi.Extension.ToLowerInvariant()
        );
    }
}
