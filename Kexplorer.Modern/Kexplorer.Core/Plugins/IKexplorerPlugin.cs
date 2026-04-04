using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Shell;

namespace Kexplorer.Core.Plugins;

/// <summary>
/// Capability attributes that declare what contexts a plugin supports.
/// Replaces the legacy IFileScript/IFolderScript/IServiceScript hierarchy.
/// A single plugin can support multiple contexts.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class FileContextAttribute : Attribute
{
    /// <summary>
    /// Optional: restrict this plugin to specific file extensions (e.g. ".xml", ".json").
    /// Null means all extensions.
    /// </summary>
    public string[]? ValidExtensions { get; set; }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class FolderContextAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public sealed class ServiceContextAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public sealed class DockerContextAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class)]
public sealed class GlobalContextAttribute : Attribute { }

/// <summary>
/// Unified plugin interface. Replaces IScript, IFileScript, IFolderScript, IServiceScript, IMixedScript.
/// Plugins declare their capabilities via attributes.
/// </summary>
public interface IKexplorerPlugin
{
    /// <summary>
    /// Display name shown in context menus.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Short description for tooltips/command palette.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Whether this plugin is currently active/enabled.
    /// </summary>
    bool IsActive { get; }

    /// <summary>
    /// Optional keyboard shortcut (e.g. Keys.F5).
    /// </summary>
    PluginShortcut? Shortcut { get; }

    /// <summary>
    /// Called once at startup to initialize the plugin.
    /// </summary>
    Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin that operates on files (one or more selected files in the grid).
/// </summary>
public interface IFilePlugin : IKexplorerPlugin
{
    /// <summary>
    /// Optional validator — return false to hide this plugin for the given file.
    /// </summary>
    bool IsValidForFile(FileEntry file) => true;

    Task ExecuteAsync(string folderPath, IReadOnlyList<FileEntry> selectedFiles, IPluginContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin that operates on a folder (right-click on tree node).
/// </summary>
public interface IFolderPlugin : IKexplorerPlugin
{
    /// <summary>
    /// Optional validator — return false to hide this plugin for the given directory.
    /// </summary>
    bool IsValidForFolder(string folderPath) => true;

    Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin that operates on Windows services.
/// </summary>
public interface IServicePlugin : IKexplorerPlugin
{
    Task ExecuteAsync(IReadOnlyList<ServiceInfo> selectedServices, IPluginContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin that operates on Docker containers.
/// </summary>
public interface IDockerPlugin : IKexplorerPlugin
{
    Task ExecuteAsync(IReadOnlyList<DockerContainerInfo> selectedContainers, IPluginContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Keyboard shortcut definition for a plugin.
/// </summary>
public sealed class PluginShortcut
{
    public PluginShortcut(PluginKey key, bool ctrl = false, bool shift = false, bool alt = false)
    {
        Key = key;
        Ctrl = ctrl;
        Shift = shift;
        Alt = alt;
    }

    public PluginKey Key { get; }
    public bool Ctrl { get; }
    public bool Shift { get; }
    public bool Alt { get; }
}

/// <summary>
/// Platform-independent key enum for plugin shortcuts.
/// </summary>
public enum PluginKey
{
    None = 0,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    Enter, Delete, Escape, Tab
}
