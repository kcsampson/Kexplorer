namespace Kexplorer.Core.FileSystem;

/// <summary>
/// Utility methods for translating between Windows UNC paths and Linux-style paths
/// for WSL (Windows Subsystem for Linux) filesystem access.
///
/// Internal representation uses Windows UNC paths (\\wsl.localhost\Ubuntu\...).
/// Display and terminal commands use Linux-style paths (/home/user/...).
/// </summary>
public static class WslPathHelper
{
    private const string WslLocalhostPrefix = @"\\wsl.localhost\";

    /// <summary>
    /// Builds the UNC root path for a WSL distro.
    /// e.g., "Ubuntu" → "\\wsl.localhost\Ubuntu"
    /// </summary>
    public static string GetUncRoot(string distroName)
    {
        return WslLocalhostPrefix + distroName;
    }

    /// <summary>
    /// Returns true if the path is a WSL UNC path (starts with \\wsl.localhost\).
    /// </summary>
    public static bool IsWslPath(string path)
    {
        return path.StartsWith(WslLocalhostPrefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the distro name from a WSL UNC path.
    /// e.g., "\\wsl.localhost\Ubuntu\home\user" → "Ubuntu"
    /// Returns null if the path is not a WSL path.
    /// </summary>
    public static string? GetDistroName(string uncPath)
    {
        if (!IsWslPath(uncPath))
            return null;

        var afterPrefix = uncPath[WslLocalhostPrefix.Length..];
        var slashIdx = afterPrefix.IndexOf('\\');
        return slashIdx >= 0 ? afterPrefix[..slashIdx] : afterPrefix;
    }

    /// <summary>
    /// Converts a WSL UNC path to a Linux-style path for display or terminal use.
    /// e.g., "\\wsl.localhost\Ubuntu\home\user\projects" → "/home/user/projects"
    /// e.g., "\\wsl.localhost\Ubuntu" → "/"
    /// Returns the original path unchanged if it's not a WSL path.
    /// </summary>
    public static string ToLinuxPath(string uncPath)
    {
        if (!IsWslPath(uncPath))
            return uncPath;

        var afterPrefix = uncPath[WslLocalhostPrefix.Length..];
        // Strip the distro name
        var slashIdx = afterPrefix.IndexOf('\\');
        if (slashIdx < 0)
            return "/"; // root of the distro

        var remainder = afterPrefix[(slashIdx + 1)..];
        if (string.IsNullOrEmpty(remainder))
            return "/";

        return "/" + remainder.Replace('\\', '/');
    }

    /// <summary>
    /// Converts a Linux-style path to a WSL UNC path.
    /// e.g., "/home/user/projects" with distro "Ubuntu" → "\\wsl.localhost\Ubuntu\home\user\projects"
    /// </summary>
    public static string ToUncPath(string linuxPath, string distroName)
    {
        var normalized = linuxPath.TrimStart('/').Replace('/', '\\');
        if (string.IsNullOrEmpty(normalized))
            return GetUncRoot(distroName);

        return GetUncRoot(distroName) + "\\" + normalized;
    }

    /// <summary>
    /// Checks whether a WSL distro is accessible by testing if its UNC root exists.
    /// </summary>
    public static bool IsDistroAvailable(string distroName)
    {
        try
        {
            return Directory.Exists(GetUncRoot(distroName));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Decomposes a WSL UNC path into segments for tree navigation restoration.
    /// The first segment is the UNC root (e.g., "\\wsl.localhost\Ubuntu"),
    /// followed by individual directory names.
    /// e.g., "\\wsl.localhost\Ubuntu\home\user" → ["\\wsl.localhost\Ubuntu", "home", "user"]
    /// </summary>
    public static List<string> DecomposePath(string uncPath)
    {
        var result = new List<string>();
        if (!IsWslPath(uncPath))
            return result;

        var afterPrefix = uncPath[WslLocalhostPrefix.Length..];
        var slashIdx = afterPrefix.IndexOf('\\');

        if (slashIdx < 0)
        {
            // Just the root: \\wsl.localhost\Ubuntu
            result.Add(uncPath);
            return result;
        }

        var distroName = afterPrefix[..slashIdx];
        result.Add(WslLocalhostPrefix + distroName); // root segment

        var remainder = afterPrefix[(slashIdx + 1)..];
        if (!string.IsNullOrEmpty(remainder))
        {
            result.AddRange(remainder.Split('\\', StringSplitOptions.RemoveEmptyEntries));
        }

        return result;
    }
}
