using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Kexplorer.Core.Docker;
using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Launching;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Shell;
using Kexplorer.Core.Work;
using Kexplorer.Plugins.BuiltIn;

namespace Kexplorer.UI;

/// <summary>
/// WPF user control for the Hybrid Services tab.
/// Top pane: Windows Services. Bottom pane: Docker Containers.
/// </summary>
public partial class HybridServicesPanel : UserControl, IHybridServiceShell
{
    private WorkQueue? _workQueue;
    private PluginManager? _pluginManager;
    private LauncherService? _launcherService;
    private readonly ObservableCollection<ServiceInfo> _services = new();
    private readonly ObservableCollection<DockerContainerInfo> _containers = new();
    private readonly WslDockerService _dockerService = new();

    public string MachineName { get; private set; } = ".";
    public string? SearchPattern { get; private set; }
    private List<string>? _serviceOrder;
    private List<string>? _dockerContainerOrder;

    public HybridServicesPanel()
    {
        InitializeComponent();
        ServiceGrid.ItemsSource = _services;
        DockerGrid.ItemsSource = _containers;
        ServiceGrid.SelectionChanged += ServiceGrid_SelectionChanged;
        DockerGrid.SelectionChanged += DockerGrid_SelectionChanged;
    }

    public async Task InitializeAsync(List<string>? visibleServices, string? machineName,
        string? searchPattern, PluginManager pluginManager,
        LauncherService? launcherService = null,
        List<string>? serviceOrder = null, List<string>? dockerContainerOrder = null)
    {
        _pluginManager = pluginManager;
        _launcherService = launcherService;
        MachineName = machineName ?? ".";
        SearchPattern = searchPattern;
        _serviceOrder = serviceOrder;
        _dockerContainerOrder = dockerContainerOrder;

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

    public List<string> GetServiceOrder()
    {
        return _services.Select(s => $"{s.DisplayName}//{s.MachineName}").ToList();
    }

    public List<string> GetDockerContainerOrder()
    {
        return _containers.Select(c => c.Name).ToList();
    }

    #region Toolbar Events

    private async void ServiceRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_workQueue is null) return;
        ServiceRefreshButton.IsEnabled = false;
        try
        {
            await _workQueue.EnqueueAsync(new ServiceLoaderWorkItem(null, MachineName, SearchPattern));
        }
        finally
        {
            ServiceRefreshButton.IsEnabled = true;
        }
    }

    private async void DockerRefresh_Click(object sender, RoutedEventArgs e)
    {
        if (_workQueue is null) return;
        DockerRefreshButton.IsEnabled = false;
        try
        {
            await _workQueue.EnqueueAsync(new DockerContainerLoaderWorkItem(_dockerService));
        }
        finally
        {
            DockerRefreshButton.IsEnabled = true;
        }
    }

    #endregion

    #region Reorder Events

    private void ServiceMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (ServiceGrid.SelectedItem is not ServiceInfo svc) return;
        var idx = _services.IndexOf(svc);
        if (idx > 0)
        {
            _services.Move(idx, idx - 1);
            ServiceGrid.SelectedItem = svc;
            ServiceGrid.ScrollIntoView(svc);
        }
    }

    private void ServiceMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (ServiceGrid.SelectedItem is not ServiceInfo svc) return;
        var idx = _services.IndexOf(svc);
        if (idx >= 0 && idx < _services.Count - 1)
        {
            _services.Move(idx, idx + 1);
            ServiceGrid.SelectedItem = svc;
            ServiceGrid.ScrollIntoView(svc);
        }
    }

    private void DockerMoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (DockerGrid.SelectedItem is not DockerContainerInfo container) return;
        var idx = _containers.IndexOf(container);
        if (idx > 0)
        {
            _containers.Move(idx, idx - 1);
            DockerGrid.SelectedItem = container;
            DockerGrid.ScrollIntoView(container);
        }
    }

    private void DockerMoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (DockerGrid.SelectedItem is not DockerContainerInfo container) return;
        var idx = _containers.IndexOf(container);
        if (idx >= 0 && idx < _containers.Count - 1)
        {
            _containers.Move(idx, idx + 1);
            DockerGrid.SelectedItem = container;
            DockerGrid.ScrollIntoView(container);
        }
    }

    private void UpdateServiceMoveButtons()
    {
        var selected = ServiceGrid.SelectedItem;
        if (selected is null)
        {
            ServiceMoveUpButton.IsEnabled = false;
            ServiceMoveDownButton.IsEnabled = false;
            return;
        }
        var idx = _services.IndexOf((ServiceInfo)selected);
        ServiceMoveUpButton.IsEnabled = idx > 0;
        ServiceMoveDownButton.IsEnabled = idx >= 0 && idx < _services.Count - 1;
    }

    private void UpdateDockerMoveButtons()
    {
        var selected = DockerGrid.SelectedItem;
        if (selected is null)
        {
            DockerMoveUpButton.IsEnabled = false;
            DockerMoveDownButton.IsEnabled = false;
            return;
        }
        var idx = _containers.IndexOf((DockerContainerInfo)selected);
        DockerMoveUpButton.IsEnabled = idx > 0;
        DockerMoveDownButton.IsEnabled = idx >= 0 && idx < _containers.Count - 1;
    }

    #endregion

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

    private static readonly IServiceLogResolver[] _logResolvers =
    {
        new Enable2020LogResolver(),
        new EwGraphqlMcpLogResolver()
    };

    private async void ServiceGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateServiceMoveButtons();

        var selected = ServiceGrid.SelectedItem as ServiceInfo;
        if (selected is null)
            return;

        // Build initial tabs — Info first, Logs added after log path is resolved
        DetailTabControl.Items.Clear();

        var infoTab = CreateInfoTab();
        DetailTabControl.Items.Add(infoTab);
        DetailTabControl.SelectedItem = infoTab;

        var infoText = (TextBox)((ScrollViewer)((TabItem)infoTab).Content).Content;
        infoText.Text = "Loading binPath...";

        // Placeholder logs tab (no Open button yet — we don't know the path)
        var logsTab = CreateLogsTab();
        DetailTabControl.Items.Add(logsTab);
        var logsText = GetLogsTextBox(logsTab);
        logsText.Text = "Loading logs...";

        string binPath;
        try
        {
            binPath = await Task.Run(() => QueryServiceBinPath(selected.ServiceName, selected.MachineName));
            infoText.Text = string.IsNullOrWhiteSpace(binPath)
                ? $"Name: {selected.DisplayName}\nSystem Name: {selected.ServiceName}\nStatus: {selected.Status}\nMachine: {selected.MachineName}\n\n(binPath not available)"
                : $"Name: {selected.DisplayName}\nSystem Name: {selected.ServiceName}\nStatus: {selected.Status}\nMachine: {selected.MachineName}\n\nbinPath:\n{binPath}";
        }
        catch
        {
            infoText.Text = $"Name: {selected.DisplayName}\nSystem Name: {selected.ServiceName}\nStatus: {selected.Status}\n\n(Could not query binPath)";
            logsText.Text = "(Could not query binPath to resolve log location)";
            return;
        }

        // Resolve and load log file from binPath
        if (string.IsNullOrWhiteSpace(binPath))
        {
            logsText.Text = "(No binPath available — cannot resolve log location)";
            return;
        }

        try
        {
            var (exePath, flags) = BinPathParser.Parse(binPath);
            string? resolvedLogPath = null;
            string? logContent = null;

            foreach (var resolver in _logResolvers)
            {
                if (!resolver.CanResolve(selected, exePath, flags))
                    continue;

                var logPaths = resolver.ResolveLogPaths(selected, exePath, flags);
                if (logPaths.Count > 0)
                {
                    resolvedLogPath = logPaths[0];
                    logContent = await Task.Run(() => ReadTail(resolvedLogPath, 500));
                }
                break;
            }

            if (resolvedLogPath is not null)
            {
                // Replace the placeholder logs tab with one that has the Open button
                DetailTabControl.Items.Remove(logsTab);
                logsTab = CreateLogsTab(resolvedLogPath, _launcherService);
                DetailTabControl.Items.Add(logsTab);
                logsText = GetLogsTextBox(logsTab);

                infoText.Text += $"\n\nLog file:\n{resolvedLogPath}";
                var displayContent = string.IsNullOrWhiteSpace(logContent) ? "(no logs)" : NormalizeJsonLogLines(logContent);
                logsText.Text = displayContent;
                logsText.CaretIndex = logsText.Text.Length;
                logsText.ScrollToEnd();
            }
            else
            {
                logsText.Text = "(No log location configured for this service type)";
            }
        }
        catch
        {
            logsText.Text = "(Could not resolve log location)";
        }
    }

    private async void DockerGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateDockerMoveButtons();

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
        var logsText = GetLogsTextBox(logsTab);
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
        var fgBrush = Application.Current.TryFindResource("PrimaryForegroundBrush") as System.Windows.Media.Brush;
        var textBox = new TextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = fgBrush ?? System.Windows.Media.Brushes.Black,
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

    private static TabItem CreateLogsTab(string? logFilePath = null, LauncherService? launcher = null)
    {
        var fgBrush = Application.Current.TryFindResource("PrimaryForegroundBrush") as System.Windows.Media.Brush;
        var textBox = new TextBox
        {
            IsReadOnly = true,
            BorderThickness = new Thickness(0),
            Background = System.Windows.Media.Brushes.Transparent,
            Foreground = fgBrush ?? System.Windows.Media.Brushes.Black,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 12.5,
            Margin = new Thickness(6, 4, 6, 4),
            VerticalAlignment = VerticalAlignment.Stretch,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
        };

        // If we have a log file path and launcher, wrap in a DockPanel with an Open button
        if (!string.IsNullOrEmpty(logFilePath) && launcher is not null)
        {
            var openButton = new Button
            {
                Content = "📂 Open",
                Width = 70,
                Height = 24,
                Margin = new Thickness(4, 2, 4, 2),
                ToolTip = $"Open {logFilePath} in external editor",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            openButton.Click += (s, e) =>
            {
                try { launcher.Launch(logFilePath); }
                catch { /* silently ignore launch failures */ }
            };

            var toolbarBrush = Application.Current.TryFindResource("ToolbarBackgroundBrush") as System.Windows.Media.Brush
                              ?? System.Windows.Media.Brushes.WhiteSmoke;
            var toolbar = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = toolbarBrush,
                Margin = new Thickness(0)
            };
            toolbar.Children.Add(openButton);

            var dock = new DockPanel();
            DockPanel.SetDock(toolbar, Dock.Top);
            dock.Children.Add(toolbar);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = textBox
            };
            dock.Children.Add(scrollViewer);

            return new TabItem { Header = "Logs", Content = dock };
        }

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

    /// <summary>
    /// Detect JSON-per-line log content and format it for readability.
    /// Each JSON line is pretty-printed with indentation.
    /// Non-JSON lines are passed through as-is.
    /// </summary>
    private static string NormalizeJsonLogLines(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var lines = raw.Split('\n');

        // Quick heuristic: check if the first non-empty line looks like JSON
        var firstNonEmpty = Array.Find(lines, l => !string.IsNullOrWhiteSpace(l));
        if (firstNonEmpty is null || !firstNonEmpty.TrimStart().StartsWith("{"))
            return raw;

        var sb = new StringBuilder(raw.Length * 2);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    var formatted = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
                    sb.AppendLine(formatted);
                    sb.AppendLine(); // blank line between entries for readability
                    continue;
                }
                catch (JsonException)
                {
                    // Not valid JSON — fall through to append raw
                }
            }
            sb.AppendLine(line.TrimEnd());
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Extract the TextBox from a logs tab, handling both layouts:
    /// TabItem > ScrollViewer > TextBox (simple), or TabItem > DockPanel > ScrollViewer > TextBox (with Open button).
    /// </summary>
    private static TextBox GetLogsTextBox(TabItem logsTab)
    {
        if (logsTab.Content is DockPanel dock)
        {
            var scrollViewer = dock.Children.OfType<ScrollViewer>().First();
            return (TextBox)scrollViewer.Content;
        }
        return (TextBox)((ScrollViewer)logsTab.Content).Content;
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

    private static string ReadTail(string filePath, int lineCount)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line is not null)
                    lines.Add(line);
            }

            var start = Math.Max(0, lines.Count - lineCount);
            return string.Join(Environment.NewLine, lines.Skip(start));
        }
        catch (Exception ex)
        {
            return $"(Could not read log file: {ex.Message})";
        }
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
            var ordered = ApplyCustomOrder(services, _serviceOrder,
                s => $"{s.DisplayName}//{s.MachineName}");
            foreach (var svc in ordered)
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
            var ordered = ApplyCustomOrder(containers, _dockerContainerOrder, c => c.Name);
            foreach (var container in ordered)
            {
                _containers.Add(container);
            }
        });
        return Task.CompletedTask;
    }

    private static IReadOnlyList<T> ApplyCustomOrder<T>(IReadOnlyList<T> items, List<string>? order, Func<T, string> keySelector)
    {
        if (order is null or { Count: 0 })
            return items;

        var orderMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < order.Count; i++)
            orderMap[order[i]] = i;

        var ordered = items.OrderBy(item =>
        {
            var key = keySelector(item);
            return orderMap.TryGetValue(key, out var idx) ? idx : int.MaxValue;
        }).ToList();

        return ordered;
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

    public Task RemoveServicesAsync(IReadOnlyList<ServiceInfo> services, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            foreach (var svc in services)
            {
                _services.Remove(svc);
            }
        });
        return Task.CompletedTask;
    }

    public Task ShowServiceLogsAsync(string serviceName, string logPath, string logContent, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            DetailTabControl.Items.Clear();

            var infoTab = CreateInfoTab();
            var logsTab = CreateLogsTab(logPath, _launcherService);
            DetailTabControl.Items.Add(infoTab);
            DetailTabControl.Items.Add(logsTab);

            var infoText = (TextBox)((ScrollViewer)((TabItem)infoTab).Content).Content;
            infoText.Text = $"Service: {serviceName}\nLog file: {logPath}";

            var logsText = GetLogsTextBox(logsTab);
            var displayContent = string.IsNullOrWhiteSpace(logContent) ? "(no logs)" : NormalizeJsonLogLines(logContent);
            logsText.Text = displayContent;
            logsText.CaretIndex = logsText.Text.Length;
            logsText.ScrollToEnd();

            DetailTabControl.SelectedItem = logsTab;
        });
        return Task.CompletedTask;
    }

    #endregion

    #region Layout Persistence

    public (double services, double docker) GetSplitterPositions()
    {
        var grid = (Grid)Content;
        var rows = grid.RowDefinitions;
        return (rows[0].ActualHeight, rows[2].ActualHeight);
    }

    public void SetSplitterPositions(double? servicesHeight, double? dockerHeight)
    {
        var grid = (Grid)Content;
        if (servicesHeight.HasValue)
            grid.RowDefinitions[0].Height = new GridLength(servicesHeight.Value, GridUnitType.Star);
        if (dockerHeight.HasValue)
            grid.RowDefinitions[2].Height = new GridLength(dockerHeight.Value, GridUnitType.Star);
    }

    public Dictionary<string, double> GetServiceColumnWidths()
    {
        var widths = new Dictionary<string, double>();
        foreach (var col in ServiceGrid.Columns)
        {
            if (col.Header is string header)
                widths[header] = col.ActualWidth;
        }
        return widths;
    }

    public void SetServiceColumnWidths(Dictionary<string, double> widths)
    {
        foreach (var col in ServiceGrid.Columns)
        {
            if (col.Header is string header && widths.TryGetValue(header, out var width))
                col.Width = new DataGridLength(width);
        }
    }

    public Dictionary<string, double> GetDockerColumnWidths()
    {
        var widths = new Dictionary<string, double>();
        foreach (var col in DockerGrid.Columns)
        {
            if (col.Header is string header)
                widths[header] = col.ActualWidth;
        }
        return widths;
    }

    public void SetDockerColumnWidths(Dictionary<string, double> widths)
    {
        foreach (var col in DockerGrid.Columns)
        {
            if (col.Header is string header && widths.TryGetValue(header, out var width))
                col.Width = new DataGridLength(width);
        }
    }

    #endregion
}
