using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Shell;
using Kexplorer.Core.Work;

namespace Kexplorer.UI;

/// <summary>
/// WPF user control for the Services tab.
/// Replaces legacy ServicesPanel + ServiceMgrWorkUnit controller pattern.
/// </summary>
public partial class ServicesPanel : UserControl, IServiceShell
{
    private WorkQueue? _workQueue;
    private PluginManager? _pluginManager;
    private readonly ObservableCollection<ServiceInfo> _services = new();

    public string MachineName { get; private set; } = ".";
    public string? SearchPattern { get; private set; }

    public ServicesPanel()
    {
        InitializeComponent();
        ServiceGrid.ItemsSource = _services;
    }

    public async Task InitializeAsync(List<string>? visibleServices, string? machineName,
        string? searchPattern, PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        MachineName = machineName ?? ".";
        SearchPattern = searchPattern;

        _workQueue = new WorkQueue(this, new WorkQueueOptions { WorkerCount = 1 });
        await _workQueue.StartAsync();

        // Build context menus from service plugins
        BuildContextMenu();

        // Load services
        await _workQueue.EnqueueAsync(new ServiceLoaderWorkItem(visibleServices, machineName, searchPattern));
    }

    public async Task ShutdownAsync()
    {
        if (_workQueue is not null)
            await _workQueue.StopAsync();
    }

    public List<string> GetVisibleServiceNames()
    {
        return _services.Select(s => $"{s.DisplayName}//{s.MachineName}").ToList();
    }

    public IReadOnlyList<ServiceInfo> GetSelectedServices()
    {
        return ServiceGrid.SelectedItems.OfType<ServiceInfo>().ToList();
    }

    private void BuildContextMenu()
    {
        ServiceContextMenu.Items.Clear();
        if (_pluginManager is null) return;

        foreach (var plugin in _pluginManager.ServicePlugins.OrderBy(p => p.Name))
        {
            if (!plugin.IsActive) continue;

            var menuItem = new MenuItem { Header = plugin.Name };
            var capturedPlugin = plugin;
            menuItem.Click += async (s, e) =>
            {
                var selected = GetSelectedServices();
                if (selected.Count == 0) return;

                // Create a minimal plugin context for services
                var context = new PluginContextAdapter(this, _workQueue!, new Core.Launching.LauncherService());
                await capturedPlugin.ExecuteAsync(selected, context, CancellationToken.None);
            };
            ServiceContextMenu.Items.Add(menuItem);
        }
    }

    #region IServiceShell / IKexplorerShell

    public Task SetServiceListAsync(IReadOnlyList<ServiceInfo> services, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _services.Clear();
            foreach (var svc in services)
            {
                _services.Add(svc);
            }
        });
        return Task.CompletedTask;
    }

    public Task ReportStatusAsync(string message, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.UpdateStatus(message);
        });
        return Task.CompletedTask;
    }

    public Task ReportErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.UpdateStatus($"Error: {message}");
        });
        return Task.CompletedTask;
    }

    public Task RefreshPathAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SetTreeChildrenAsync(string parentPath, IReadOnlyList<FileSystemNode> children, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task SetFileListAsync(string directoryPath, IReadOnlyList<FileEntry> files, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task NavigateToPathAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveTreeNodeAsync(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;

    #endregion
}
