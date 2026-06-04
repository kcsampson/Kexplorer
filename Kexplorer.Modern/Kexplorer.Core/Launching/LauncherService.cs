using System.Diagnostics;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kexplorer.Core.Launching;

/// <summary>
/// A single launcher mapping: extension → program + options.
/// Replaces the legacy XML-based Launcher element.
/// </summary>
public sealed class LauncherMapping
{
    [JsonPropertyName("ext")]
    public string Extension { get; set; } = "";

    [JsonPropertyName("command")]
    public string Command { get; set; } = "";

    [JsonPropertyName("preOption")]
    public string? PreOption { get; set; }

    [JsonPropertyName("options")]
    public string? Options { get; set; }
}

/// <summary>
/// Configuration root for launchers.json.
/// </summary>
public sealed class LauncherConfig
{
    [JsonPropertyName("launchers")]
    public List<LauncherMapping> Launchers { get; set; } = new();

    /// <summary>
    /// The command used by "Open in Editor" for files/folders (e.g., "code", "zed", "notepad++").
    /// Defaults to "code" if not set.
    /// </summary>
    [JsonPropertyName("projectEditor")]
    public string? ProjectEditor { get; set; }
}

/// <summary>
/// Launches files based on extension mappings from launchers.json.
/// Replaces legacy Launcher class (XML-driven, XmlDocument/XPath).
/// </summary>
public sealed class LauncherService
{
    private static readonly HashSet<string> DirectExecuteExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".cmd", ".bat", ".lnk", ".msi"
    };

    private readonly Dictionary<string, LauncherMapping> _mappings = new(StringComparer.OrdinalIgnoreCase);
    private LauncherMapping? _defaultMapping;

    // Sensible Windows defaults when the user has no launchers.json yet.
    // Any explicit mapping in launchers.json overrides these.
    private static readonly LauncherMapping[] WindowsDefaultMappings =
    {
        new() { Extension = "pdf", Command = "chrome.exe" },
        new() { Extension = "doc", Command = "winword.exe" },
        new() { Extension = "docx", Command = "winword.exe" },
        new() { Extension = "xls", Command = "excel.exe" },
        new() { Extension = "xlsx", Command = "excel.exe" },
        new() { Extension = "xlsm", Command = "excel.exe" },
        new() { Extension = "xlsb", Command = "excel.exe" },
        new() { Extension = "csv", Command = "excel.exe" },
        new() { Extension = "ppt", Command = "powerpnt.exe" },
        new() { Extension = "pptx", Command = "powerpnt.exe" }
    };

    /// <summary>
    /// The project editor command (e.g., "code", "zed", "notepad++"). Defaults to "code".
    /// </summary>
    public string ProjectEditor { get; private set; } = "code";

    /// <summary>
    /// Load launcher configuration from a JSON file path.
    /// </summary>
    public async Task LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _mappings.Clear();
        _defaultMapping = null;
        ProjectEditor = "code";

        if (OperatingSystem.IsWindows())
        {
            foreach (var mapping in WindowsDefaultMappings)
            {
                _mappings["." + mapping.Extension.TrimStart('.')] = new LauncherMapping
                {
                    Extension = mapping.Extension,
                    Command = mapping.Command,
                    PreOption = mapping.PreOption,
                    Options = mapping.Options
                };
            }
        }

        if (!File.Exists(filePath))
            return;

        await using var stream = File.OpenRead(filePath);
        var config = await JsonSerializer.DeserializeAsync<LauncherConfig>(stream, cancellationToken: cancellationToken);

        if (config?.Launchers is null)
            return;

        if (!string.IsNullOrWhiteSpace(config.ProjectEditor))
            ProjectEditor = config.ProjectEditor;

        foreach (var mapping in config.Launchers)
        {
            var ext = mapping.Extension.TrimStart('.');
            if (ext == "*")
            {
                _defaultMapping = mapping;
            }
            else
            {
                _mappings["." + ext] = mapping;
            }
        }
    }

    /// <summary>
    /// Launch the given file using the configured mapping, or the default.
    /// </summary>
    public void Launch(string filePath)
    {
        var fi = new FileInfo(filePath);
        var ext = fi.Extension;

        // Direct-execute for exe/cmd/bat/lnk/msi
        if (DirectExecuteExtensions.Contains(ext))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fi.FullName,
                WorkingDirectory = fi.DirectoryName ?? "",
                UseShellExecute = true
            });
            return;
        }

        // Look up extension mapping, fall back to default
        if (!_mappings.TryGetValue(ext, out var mapping))
        {
            mapping = _defaultMapping;
        }

        if (mapping is null || string.IsNullOrEmpty(mapping.Command))
        {
            // No mapping — try shell execute
            Process.Start(new ProcessStartInfo
            {
                FileName = fi.FullName,
                UseShellExecute = true
            });
            return;
        }

        var args = "";
        if (!string.IsNullOrEmpty(mapping.PreOption))
        {
            args = mapping.PreOption + " ";
        }
        args += $"\"{fi.FullName}\"";
        if (!string.IsNullOrEmpty(mapping.Options))
        {
            args += " " + mapping.Options;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = mapping.Command,
                Arguments = args,
                WorkingDirectory = fi.DirectoryName ?? "",
                UseShellExecute = true
            });
        }
        catch (Win32Exception)
        {
            // If the configured launcher command is unavailable, fall back to shell open.
            Process.Start(new ProcessStartInfo
            {
                FileName = fi.FullName,
                UseShellExecute = true
            });
        }
        catch (FileNotFoundException)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = fi.FullName,
                UseShellExecute = true
            });
        }
    }

    /// <summary>
    /// Save the current configuration to a JSON file.
    /// </summary>
    public async Task SaveAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var config = new LauncherConfig();

        if (_defaultMapping is not null)
        {
            config.Launchers.Add(_defaultMapping);
        }

        foreach (var mapping in _mappings.Values)
        {
            config.Launchers.Add(mapping);
        }

        var dir = Path.GetDirectoryName(filePath);
        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, config, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }
}
