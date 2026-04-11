using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kexplorer.Core.State;

/// <summary>
/// Represents a single tab's persisted state.
/// Replaces legacy TreeViewPersistState XML output.
/// </summary>
public sealed class TabState
{
    [JsonPropertyName("tabName")]
    public string TabName { get; set; } = "";

    [JsonPropertyName("tabType")]
    public TabType TabType { get; set; } = TabType.FileExplorer;

    [JsonPropertyName("currentFolder")]
    public string? CurrentFolder { get; set; }

    [JsonPropertyName("drives")]
    public List<string> Drives { get; set; } = new();

    [JsonPropertyName("isSelected")]
    public bool IsSelected { get; set; }

    /// <summary>
    /// For Services tabs: the list of visible service names.
    /// Format: "ServiceDisplayName//MachineName"
    /// </summary>
    [JsonPropertyName("visibleServices")]
    public List<string>? VisibleServices { get; set; }

    /// <summary>
    /// For Services tabs: machine name and search pattern.
    /// </summary>
    [JsonPropertyName("machineName")]
    public string? MachineName { get; set; }

    [JsonPropertyName("searchPattern")]
    public string? SearchPattern { get; set; }

    /// <summary>
    /// For File Explorer tabs: full paths of all expanded folders in the tree.
    /// Used to restore the visual tree state on startup.
    /// </summary>
    [JsonPropertyName("expandedFolders")]
    public List<string>? ExpandedFolders { get; set; }

    /// <summary>
    /// For File Explorer tabs: full path of the currently selected (highlighted) folder.
    /// </summary>
    [JsonPropertyName("selectedFolder")]
    public string? SelectedFolder { get; set; }

    /// <summary>
    /// For Services tabs: ordered list of service display-name//machine pairs defining the user's custom row order.
    /// Null or empty means no custom order (use default OS order).
    /// </summary>
    [JsonPropertyName("serviceOrder")]
    public List<string>? ServiceOrder { get; set; }

    /// <summary>
    /// For Services tabs: ordered list of Docker container names defining the user's custom row order.
    /// </summary>
    [JsonPropertyName("dockerContainerOrder")]
    public List<string>? DockerContainerOrder { get; set; }

    /// <summary>
    /// When set, this tab is rooted at a specific folder rather than a drive.
    /// The tree shows only this folder as the root node (using its short name).
    /// Null means the tab uses standard drive-based roots.
    /// </summary>
    [JsonPropertyName("rootFolderPath")]
    public string? RootFolderPath { get; set; }

    /// <summary>
    /// The type of file explorer tab.
    /// "Local" (default/null) = standard Windows drives.
    /// "WSL" = WSL filesystem rooted at \\wsl.localhost\{WslDistroName}.
    /// </summary>
    [JsonPropertyName("explorerType")]
    public string? ExplorerType { get; set; }

    /// <summary>
    /// For WSL tabs, the distro name (e.g., "Ubuntu").
    /// Used to construct the UNC root: \\wsl.localhost\{WslDistroName}.
    /// </summary>
    [JsonPropertyName("wslDistroName")]
    public string? WslDistroName { get; set; }

    // ── Network tab state ──

    [JsonPropertyName("networkListeningOnly")]
    public bool? NetworkListeningOnly { get; set; }

    [JsonPropertyName("networkTcpOnly")]
    public bool? NetworkTcpOnly { get; set; }

    [JsonPropertyName("networkSearchText")]
    public string? NetworkSearchText { get; set; }

    [JsonPropertyName("networkHiddenProcesses")]
    public List<string>? NetworkHiddenProcesses { get; set; }

    [JsonPropertyName("networkSortColumn")]
    public string? NetworkSortColumn { get; set; }

    [JsonPropertyName("networkSortDirection")]
    public string? NetworkSortDirection { get; set; }

    /// <summary>
    /// Column widths keyed by header name, e.g. {"Protocol":70,"Local Address":140,...}
    /// </summary>
    [JsonPropertyName("networkColumnWidths")]
    public Dictionary<string, double>? NetworkColumnWidths { get; set; }

    // ── Terminal tab state ──

    [JsonPropertyName("terminalShellCommand")]
    public string? TerminalShellCommand { get; set; }

    [JsonPropertyName("terminalDirectory")]
    public string? TerminalDirectory { get; set; }

    // ── Text viewer tab state ──

    [JsonPropertyName("textViewerFilePath")]
    public string? TextViewerFilePath { get; set; }

    [JsonPropertyName("textViewerWordWrap")]
    public bool? TextViewerWordWrap { get; set; }

    [JsonPropertyName("textViewerIsEditing")]
    public bool? TextViewerIsEditing { get; set; }

    // ── Chat tab state ──

    [JsonPropertyName("chatModel")]
    public string? ChatModel { get; set; }

    // ── Shared layout state (splitter positions, column widths) ──

    [JsonPropertyName("splitterPosition")]
    public double? SplitterPosition { get; set; }

    [JsonPropertyName("splitterPosition2")]
    public double? SplitterPosition2 { get; set; }

    [JsonPropertyName("columnWidths")]
    public Dictionary<string, double>? ColumnWidths { get; set; }

    [JsonPropertyName("columnWidths2")]
    public Dictionary<string, double>? ColumnWidths2 { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TabType
{
    FileExplorer,
    Services,
    HybridServices,
    Network,
    Terminal,
    TextViewer,
    Chat
}

/// <summary>
/// Root state object persisted to ~/.kexplorer/state.json.
/// Replaces legacy KexplorerStateSave.xml.
/// </summary>
public sealed class SessionState
{
    [JsonPropertyName("tabs")]
    public List<TabState> Tabs { get; set; } = new();

    [JsonPropertyName("windowWidth")]
    public double WindowWidth { get; set; } = 900;

    [JsonPropertyName("windowHeight")]
    public double WindowHeight { get; set; } = 600;

    [JsonPropertyName("windowLeft")]
    public double? WindowLeft { get; set; }

    [JsonPropertyName("windowTop")]
    public double? WindowTop { get; set; }

    [JsonPropertyName("treeSplitterPosition")]
    public double TreeSplitterPosition { get; set; } = 250;

    [JsonPropertyName("themeName")]
    public string ThemeName { get; set; } = "Standard";
}

/// <summary>
/// Handles loading and saving session state to JSON.
/// </summary>
public static class SessionStateManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static string GetDefaultStatePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(appData, ".kexplorer", "state.json");
    }

    public static async Task<SessionState> LoadAsync(string? filePath = null, CancellationToken cancellationToken = default)
    {
        filePath ??= GetDefaultStatePath();

        if (!File.Exists(filePath))
        {
            return CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var state = await JsonSerializer.DeserializeAsync<SessionState>(stream, JsonOptions, cancellationToken);
            return state ?? CreateDefault();
        }
        catch (JsonException)
        {
            return CreateDefault();
        }
    }

    public static async Task SaveAsync(SessionState state, string? filePath = null, CancellationToken cancellationToken = default)
    {
        filePath ??= GetDefaultStatePath();

        var dir = Path.GetDirectoryName(filePath);
        if (dir is not null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
    }

    private static SessionState CreateDefault()
    {
        return new SessionState
        {
            Tabs = new List<TabState>
            {
                new()
                {
                    TabName = "Main",
                    TabType = TabType.FileExplorer,
                    IsSelected = true
                }
            }
        };
    }
}
