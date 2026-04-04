namespace Kexplorer.Core.Plugins;

/// <summary>
/// Discovers and manages plugins. Replaces legacy ScriptMgr.
/// </summary>
public sealed class PluginManager
{
    private readonly List<IFilePlugin> _filePlugins = new();
    private readonly List<IFolderPlugin> _folderPlugins = new();
    private readonly List<IServicePlugin> _servicePlugins = new();
    private readonly List<IDockerPlugin> _dockerPlugins = new();

    public IReadOnlyList<IFilePlugin> FilePlugins => _filePlugins;
    public IReadOnlyList<IFolderPlugin> FolderPlugins => _folderPlugins;
    public IReadOnlyList<IServicePlugin> ServicePlugins => _servicePlugins;
    public IReadOnlyList<IDockerPlugin> DockerPlugins => _dockerPlugins;

    public void Register(IKexplorerPlugin plugin)
    {
        if (plugin is IFilePlugin fp)
            _filePlugins.Add(fp);
        if (plugin is IFolderPlugin folderPlugin)
            _folderPlugins.Add(folderPlugin);
        if (plugin is IServicePlugin sp)
            _servicePlugins.Add(sp);
        if (plugin is IDockerPlugin dp)
            _dockerPlugins.Add(dp);
    }

    /// <summary>
    /// Initialize all registered plugins.
    /// </summary>
    public async Task InitializeAllAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        var seen = new HashSet<IKexplorerPlugin>(ReferenceEqualityComparer.Instance);

        foreach (var p in _filePlugins)
        {
            if (seen.Add(p))
                await p.InitializeAsync(context, cancellationToken);
        }
        foreach (var p in _folderPlugins)
        {
            if (seen.Add(p))
                await p.InitializeAsync(context, cancellationToken);
        }
        foreach (var p in _servicePlugins)
        {
            if (seen.Add(p))
                await p.InitializeAsync(context, cancellationToken);
        }
        foreach (var p in _dockerPlugins)
        {
            if (seen.Add(p))
                await p.InitializeAsync(context, cancellationToken);
        }
    }

    /// <summary>
    /// Scan an assembly for plugin types and register them.
    /// </summary>
    public void ScanAssembly(System.Reflection.Assembly assembly)
    {
        foreach (var type in assembly.GetExportedTypes())
        {
            if (type.IsAbstract || type.IsInterface)
                continue;

            if (typeof(IKexplorerPlugin).IsAssignableFrom(type))
            {
                var ctor = type.GetConstructor(Type.EmptyTypes);
                if (ctor is not null)
                {
                    var plugin = (IKexplorerPlugin)ctor.Invoke(Array.Empty<object>());
                    Register(plugin);
                }
            }
        }
    }
}
