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
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TabType
{
    FileExplorer,
    Services,
    HybridServices
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
