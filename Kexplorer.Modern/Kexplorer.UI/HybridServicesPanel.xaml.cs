using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Kexplorer.Core.Docker;
using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Shell;
using Kexplorer.Core.Work;

namespace Kexplorer.UI;

/// <summary>
/// WPF user control for the Hybrid Services tab.
/// Top pane: Windows Services. Bottom pane: Docker Containers.
/// </summary>
public partial class HybridServicesPanel : UserControl, IHybridServiceShell
{
    private WorkQueue? _workQueue;
    private PluginManager? _pluginManager;
    private readonly ObservableCollection<ServiceInfo> _services = new();
    private readonly ObservableCollection<DockerContainerInfo> _containers = new();
    private readonly WslDockerService _dockerService = new();

    public string MachineName { get; private set; } = ".";
    public string? SearchPattern { get; private set; }

    public HybridServicesPanel()
    {
        InitializeComponent();
        ServiceGrid.ItemsSource = _services;
        DockerGrid.ItemsSource = _containers;
        ServiceGrid.SelectionChanged += ServiceGrid_SelectionChanged;
        DockerGrid.SelectionChanged += DockerGrid_SelectionChanged;
    }

    public async Task InitializeAsync(List<string>? visibleServices, string? machineName,
        string? searchPattern, PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        MachineName = machineName ?? ".";
        SearchPattern = searchPattern;

        _workQueue = new WorkQueue(this, new WorkQueueOptions { WorkerCount = 2 });
        await _workQueue.StartAsync();

        // Build context menus from service plugins
        BuildServiceContextMenu();
        BuildDockerContextMenu();

        // Load services (top pane) and Docker containers (bottom pane) in parallel
        await _workQueue.EnqueueAsync(new ServiceLoaderWorkItem(visibleServices, machineName, searchPattern));
        await _workQueue.EnqueueAsync(new DockerContainerLoaderWorkItem(_dockerService));
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

    public IReadOnlyList<DockerContainerInfo> GetSelectedContainers()
    {
        return DockerGrid.SelectedItems.OfType<DockerContainerInfo>().ToList();
    }

    private void BuildServiceContextMenu()
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

                var context = new PluginContextAdapter(this, _workQueue!, new Core.Launching.LauncherService());
                await capturedPlugin.ExecuteAsync(selected, context, CancellationToken.None);
            };
            ServiceContextMenu.Items.Add(menuItem);
        }
    }

    private void BuildDockerContextMenu()
    {
        DockerContextMenu.Items.Clear();
        if (_pluginManager is null) return;

        foreach (var plugin in _pluginManager.DockerPlugins.OrderBy(p => p.Name))
        {
            if (!plugin.IsActive) continue;

            var menuItem = new MenuItem { Header = plugin.Name };
            var capturedPlugin = plugin;
            menuItem.Click += async (s, e) =>
            {
                var selected = GetSelectedContainers();
                if (selected.Count == 0) return;

                var context = new PluginContextAdapter(this, _workQueue!, new Core.Launching.LauncherService());
                await capturedPlugin.ExecuteAsync(selected, context, CancellationToken.None);
            };
            DockerContextMenu.Items.Add(menuItem);
        }
    }

    private async void ServiceGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = ServiceGrid.SelectedItem as ServiceInfo;
        if (selected is null)
            return;

        // Build tabs for service selection: Info only (for now)
        DetailTabControl.Items.Clear();

        var infoTab = CreateInfoTab();
        DetailTabControl.Items.Add(infoTab);
        DetailTabControl.SelectedItem = infoTab;

        var infoText = (TextBox)((ScrollViewer)((TabItem)infoTab).Content).Content;
        infoText.Text = "Loading binPath...";

        try
        {
            var binPath = await Task.Run(() => QueryServiceBinPath(selected.ServiceName, selected.MachineName));
            infoText.Text = string.IsNullOrWhiteSpace(binPath)
                ? $"Name: {selected.DisplayName}\nSystem Name: {selected.ServiceName}\nStatus: {selected.Status}\nMachine: {selected.MachineName}\n\n(binPath not available)"
                : $"Name: {selected.DisplayName}\nSystem Name: {selected.ServiceName}\nStatus: {selected.Status}\nMachine: {selected.MachineName}\n\nbinPath:\n{binPath}";
        }
        catch
        {
            infoText.Text = $"Name: {selected.DisplayName}\nSystem Name: {selected.ServiceName}\nStatus: {selected.Status}\n\n(Could not query binPath)";
        }
    }

    private async void DockerGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selected = DockerGrid.SelectedItem as DockerContainerInfo;
        if (selected is null)
            return;

        // Build tabs for Docker selection: Info + Logs
        DetailTabControl.Items.Clear();

        var infoTab = CreateInfoTab();
        var logsTab = CreateLogsTab();
        DetailTabControl.Items.Add(infoTab);
        DetailTabControl.Items.Add(logsTab);
        DetailTabControl.SelectedItem = infoTab;

        var infoText = (TextBox)((ScrollViewer)((TabItem)infoTab).Content).Content;
        infoText.Text = "Loading inspect data...";

        // Load Info tab
        try
        {
            var inspectJson = await _dockerService.InspectContainerAsync(selected.Name);
            if (string.IsNullOrWhiteSpace(inspectJson))
            {
                infoText.Text = $"Name: {selected.Name}\nImage: {selected.Image}\nStatus: {selected.Status}\n\n(inspect not available)";
            }
            else
            {
                var runCommand = ReconstructDockerRun(inspectJson, selected);
                infoText.Text = $"Name: {selected.Name}\nImage: {selected.Image}\nStatus: {selected.Status}\nPorts: {selected.Ports}\nNetwork: {selected.Network}\nCreated: {selected.Created}\n\ndocker run equivalent:\n{runCommand}";
            }
        }
        catch
        {
            infoText.Text = $"Name: {selected.Name}\nImage: {selected.Image}\nStatus: {selected.Status}\n\n(Could not inspect container)";
        }

        // Load Logs tab in background
        var logsText = (TextBox)((ScrollViewer)((TabItem)logsTab).Content).Content;
        logsText.Text = "Loading logs...";

        try
        {
            var logs = await _dockerService.GetLogsAsync(selected.Name, 500);
            logsText.Text = string.IsNullOrWhiteSpace(logs) ? "(no logs)" : logs;
            // Scroll to end
            logsText.CaretIndex = logsText.Text.Length;
            logsText.ScrollToEnd();
        }
        catch
        {
            logsText.Text = "(Could not retrieve logs)";
        }
    }

    private static TabItem CreateInfoTab()
    {
        var textBox = new TextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12.5,
            Margin = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Top
        };

        return new TabItem
        {
            Header = "Info",
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = textBox
            }
        };
    }

    private static TabItem CreateLogsTab()
    {
        var textBox = new TextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12.5,
            Margin = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        return new TabItem
        {
            Header = "Logs",
            Content = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = textBox
            }
        };
    }

    private static string QueryServiceBinPath(string serviceName, string machineName)
    {
        var args = machineName is "." or ""
            ? $"qc \"{serviceName}\""
            : $"\\\\{machineName} qc \"{serviceName}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "sc",
            Arguments = args,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null) return "";

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        // Parse BINARY_PATH_NAME line
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("BINARY_PATH_NAME", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx >= 0)
                    return trimmed[(colonIdx + 1)..].Trim();
            }
        }

        return "";
    }

    private static string ReconstructDockerRun(string inspectJson, DockerContainerInfo container)
    {
        try
        {
            using var doc = JsonDocument.Parse(inspectJson);
            var root = doc.RootElement;

            // docker inspect returns an array
            var info = root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
                ? root[0]
                : root;

            var sb = new StringBuilder("docker run");

            // Name
            if (info.TryGetProperty("Name", out var nameProp))
            {
                var name = nameProp.GetString()?.TrimStart('/');
                if (!string.IsNullOrEmpty(name))
                    sb.Append($" --name {name}");
            }

            // Restart policy
            if (info.TryGetProperty("HostConfig", out var hostConfig))
            {
                if (hostConfig.TryGetProperty("RestartPolicy", out var restartPolicy) &&
                    restartPolicy.TryGetProperty("Name", out var rpName))
                {
                    var rp = rpName.GetString();
                    if (!string.IsNullOrEmpty(rp) && rp != "no")
                        sb.Append($" --restart {rp}");
                }

                // Port bindings
                if (hostConfig.TryGetProperty("PortBindings", out var portBindings) &&
                    portBindings.ValueKind == JsonValueKind.Object)
                {
                    foreach (var port in portBindings.EnumerateObject())
                    {
                        if (port.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var binding in port.Value.EnumerateArray())
                            {
                                var hostPort = binding.TryGetProperty("HostPort", out var hp) ? hp.GetString() : "";
                                var hostIp = binding.TryGetProperty("HostIp", out var hi) ? hi.GetString() : "";

                                if (!string.IsNullOrEmpty(hostPort))
                                {
                                    var hostPart = string.IsNullOrEmpty(hostIp) || hostIp == "0.0.0.0"
                                        ? hostPort
                                        : $"{hostIp}:{hostPort}";
                                    sb.Append($" -p {hostPart}:{port.Name}");
                                }
                            }
                        }
                    }
                }

                // Volumes / binds
                if (hostConfig.TryGetProperty("Binds", out var binds) &&
                    binds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var bind in binds.EnumerateArray())
                    {
                        var vol = bind.GetString();
                        if (!string.IsNullOrEmpty(vol))
                            sb.Append($" -v {vol}");
                    }
                }

                // Network mode
                if (hostConfig.TryGetProperty("NetworkMode", out var netMode))
                {
                    var nm = netMode.GetString();
                    if (!string.IsNullOrEmpty(nm) && nm != "default")
                        sb.Append($" --network {nm}");
                }

                // GPU / device requests
                if (hostConfig.TryGetProperty("DeviceRequests", out var deviceReqs) &&
                    deviceReqs.ValueKind == JsonValueKind.Array && deviceReqs.GetArrayLength() > 0)
                {
                    sb.Append(" --gpus all");
                }
            }

            // Environment variables
            if (info.TryGetProperty("Config", out var config))
            {
                if (config.TryGetProperty("Env", out var envVars) &&
                    envVars.ValueKind == JsonValueKind.Array)
                {
                    foreach (var envVar in envVars.EnumerateArray())
                    {
                        var val = envVar.GetString();
                        if (!string.IsNullOrEmpty(val) && !val.StartsWith("PATH=", StringComparison.OrdinalIgnoreCase))
                            sb.Append($" -e \"{val}\"");
                    }
                }

                // Image
                if (config.TryGetProperty("Image", out var image))
                {
                    sb.Append($" {image.GetString()}");
                }
                else
                {
                    sb.Append($" {container.Image}");
                }

                // Cmd
                if (config.TryGetProperty("Cmd", out var cmd) &&
                    cmd.ValueKind == JsonValueKind.Array && cmd.GetArrayLength() > 0)
                {
                    foreach (var arg in cmd.EnumerateArray())
                    {
                        var a = arg.GetString();
                        if (!string.IsNullOrEmpty(a))
                            sb.Append($" {a}");
                    }
                }
            }
            else
            {
                sb.Append($" {container.Image}");
            }

            return sb.ToString();
        }
        catch
        {
            return $"docker run {container.Image}  # (could not fully parse inspect data)";
        }
    }

    #region IHybridServiceShell / IServiceShell / IKexplorerShell

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

    public Task SetDockerContainerListAsync(List<DockerContainerInfo> containers, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _containers.Clear();
            foreach (var container in containers)
            {
                _containers.Add(container);
            }
        });
        return Task.CompletedTask;
    }

    public Task SetDockerStatusAsync(string message, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            DockerStatusText.Text = message;
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
