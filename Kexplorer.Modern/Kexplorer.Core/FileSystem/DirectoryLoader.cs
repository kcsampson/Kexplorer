using System.Text.Json.Serialization;

namespace Kexplorer.Core.FileSystem;

/// <summary>
/// Loads directory children (subdirectories) for a node.
/// Replaces legacy FolderWorkUnit — handles both initial load and stale refresh.
/// </summary>
public static class DirectoryLoader
{
    /// <summary>
    /// Load subdirectories for the given path, up to recurseDepth levels deep.
    /// </summary>
    public static List<FileSystemNode> LoadChildren(string directoryPath, int recurseDepth = 1)
    {
        var result = new List<FileSystemNode>();
        try
        {
            var di = new DirectoryInfo(directoryPath);
            foreach (var subdir in di.GetDirectories())
            {
                var childNode = new FileSystemNode(subdir.Name, subdir.FullName, isDirectory: true);
                childNode.Loaded = true;

                if (recurseDepth > 1)
                {
                    childNode.Children.AddRange(LoadChildren(subdir.FullName, recurseDepth - 1));
                }

                result.Add(childNode);
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }

        return result;
    }

    /// <summary>
    /// Load files in a directory.
    /// </summary>
    public static List<FileEntry> LoadFiles(string directoryPath)
    {
        var result = new List<FileEntry>();
        try
        {
            var di = new DirectoryInfo(directoryPath);
            foreach (var fi in di.GetFiles())
            {
                result.Add(FileEntry.FromFileInfo(fi));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (IOException) { }

        return result;
    }

    /// <summary>
    /// Get logical drives as root-level nodes.
    /// </summary>
    public static List<FileSystemNode> GetDriveNodes(IEnumerable<string>? onlyTheseDrives = null)
    {
        var drives = onlyTheseDrives ?? Directory.GetLogicalDrives();
        var result = new List<FileSystemNode>();
        foreach (var drive in drives)
        {
            var driveLetter = drive.TrimEnd('\\', '/');
            if (!driveLetter.EndsWith(':'))
                driveLetter += ":";
            var fullPath = driveLetter + "\\";

            result.Add(new FileSystemNode(fullPath, fullPath, isDirectory: true));
        }
        return result;
    }
}
