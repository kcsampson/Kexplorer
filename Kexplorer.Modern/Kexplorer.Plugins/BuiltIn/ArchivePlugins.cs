using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Archive file extensions recognized by the archive plugins.
/// </summary>
internal static class ArchiveExtensions
{
    public static readonly string[] All = new[]
    {
        ".zip", ".7z", ".jar", ".war", ".ear",
        ".tar", ".gz", ".gzip", ".tgz", ".bz2", ".xz", ".zst",
        ".rar", ".cab", ".iso",
        ".docx", ".xlsx", ".xlsm", ".pptx", // Office (ZIP-based)
        ".nupkg", ".whl" // Package formats (ZIP-based)
    };

    public static bool IsArchive(string extension)
        => All.Contains(extension, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// View contents of an archive with 7-Zip File Manager.
/// Port of legacy ZipOpenFileScript. Only shown for archive files.
/// </summary>
[FileContext]
public sealed class ArchiveViewPlugin : IFilePlugin
{
    public string Name => "Zip - 7z View";
    public string Description => "View contents of archive with 7-Zip";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsValidForFile(FileEntry file)
        => ArchiveExtensions.IsArchive(file.Extension);

    public Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (selectedFiles.Count > 0)
        {
            context.RunProgram("7zFM.exe", $"\"{selectedFiles[0].FullPath}\"", folderPath);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Extract archive to the current folder using 7z command line.
/// Port of legacy UnzipHereFileScript. Only shown for archive files.
/// </summary>
[FileContext]
public sealed class ArchiveExtractHerePlugin : IFilePlugin
{
    public string Name => "Zip - Unzip Here";
    public string Description => "Extract the selected archive into the current folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsValidForFile(FileEntry file)
        => ArchiveExtensions.IsArchive(file.Extension);

    public async Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        foreach (var file in selectedFiles)
        {
            context.RunProgram("7z.exe", $"x \"{file.FullPath}\" -o\"{folderPath}\"", folderPath);
        }
        await context.RefreshFolderAsync(folderPath, cancellationToken);
    }
}

/// <summary>
/// Extract archive to a previously designated "Next Unzip Location".
/// Port of legacy ZipUnzipToFileScript. Only shown for archive files
/// and only when a destination has been set via "Zip - Next Unzip Location".
/// </summary>
[FileContext]
public sealed class ArchiveExtractToPlugin : IFilePlugin
{
    public string Name => "Zip - Unzip To";
    public string Description => "Extract to the previously selected unzip location";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsValidForFile(FileEntry file)
        => ArchiveExtensions.IsArchive(file.Extension);

    public async Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (!context.Variables.TryGetValue("NEXTUNZIPLOCATION", out var destObj) || destObj is not string destPath)
        {
            await context.Shell.ReportErrorAsync(
                "No unzip destination set. Right-click a folder and choose 'Zip - Next Unzip Location' first.",
                cancellationToken: cancellationToken);
            return;
        }

        foreach (var file in selectedFiles)
        {
            context.RunProgram("7z.exe", $"x \"{file.FullPath}\" -o\"{destPath}\"", destPath);
        }
        await context.RefreshFolderAsync(destPath, cancellationToken);
    }
}

/// <summary>
/// Designate a folder as the target for "Zip - Unzip To".
/// Port of legacy ZipNextUnZipLocFolderScript.
/// </summary>
[FolderContext]
public sealed class ArchiveSetUnzipLocationPlugin : IFolderPlugin
{
    public string Name => "Zip - Next Unzip Location";
    public string Description => "Set this folder as the target for 'Unzip To'";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        context.Variables["NEXTUNZIPLOCATION"] = folderPath;
        await context.Shell.ReportStatusAsync($"Next unzip location set to: {folderPath}", cancellationToken);
    }
}

/// <summary>
/// Compress files or a folder into a .zip archive using 7z.
/// Port of legacy ZipScript. Available for all files and folders.
/// </summary>
[FolderContext]
[FileContext]
public sealed class ArchiveCreatePlugin : IFolderPlugin, IFilePlugin
{
    public string Name => "Zip with 7z";
    public string Description => "Compress selected files or folder into a .zip archive";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var defaultName = selectedFiles.Count == 1
            ? Path.GetFileNameWithoutExtension(selectedFiles[0].Name)
            : Path.GetFileName(folderPath);

        var zipName = await context.PromptAsync("Create Zip", "Zip Name", $"..\\{defaultName}", cancellationToken);
        if (string.IsNullOrEmpty(zipName)) return;

        if (!zipName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            zipName += ".zip";

        // Build a list file for multi-file zipping
        var listFile = Path.Combine(Path.GetTempPath(), "kexplorer_ziplist.lst");
        await File.WriteAllLinesAsync(listFile, selectedFiles.Select(f => f.FullPath), cancellationToken);

        context.RunProgram("7z.exe", $"a -tzip \"{zipName}\" @\"{listFile}\"", folderPath);
    }

    public async Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var dirName = Path.GetFileName(folderPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var zipName = await context.PromptAsync("Create Zip", "Zip Name", $"..\\{dirName}", cancellationToken);
        if (string.IsNullOrEmpty(zipName)) return;

        if (!zipName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            zipName += ".zip";

        context.RunProgram("7z.exe", $"a -tzip \"{zipName}\" -r \"{folderPath}\"", folderPath);
    }
}
