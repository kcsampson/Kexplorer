using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Kexplorer.Core.Launching;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.State;
using Kexplorer.Core.Work;

namespace Kexplorer.UI;

public partial class MainWindow : Window
{
    private SessionState _sessionState = new();
    private readonly PluginManager _pluginManager = new();
    private readonly LauncherService _launcherService = new();
    private bool _isInitializing = true;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Load state
        _sessionState = await SessionStateManager.LoadAsync();

        // Apply saved theme
        ThemeManager.ApplyTheme(_sessionState.ThemeName);

        // Restore window dimensions
        Width = _sessionState.WindowWidth;
        Height = _sessionState.WindowHeight;
        if (_sessionState.WindowLeft.HasValue)
            Left = _sessionState.WindowLeft.Value;
        if (_sessionState.WindowTop.HasValue)
            Top = _sessionState.WindowTop.Value;

        // Load launchers
        var launcherPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".kexplorer", "launchers.json");
        await _launcherService.LoadAsync(launcherPath);

        // Scan built-in plugins
        _pluginManager.ScanAssembly(typeof(Kexplorer.Plugins.BuiltInPluginMarker).Assembly);

        // Restore tabs from state, or create a default tab
        if (_sessionState.Tabs.Count == 0)
        {
            AddExplorerTab("Main", null, null, isSelected: true);
        }
        else
        {
            foreach (var tabState in _sessionState.Tabs)
            {
                switch (tabState.TabType)
                {
                    case TabType.FileExplorer:
                        if (string.Equals(tabState.ExplorerType, "WSL", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrEmpty(tabState.WslDistroName))
                        {
                            // WSL tab — pass distro name for WSL-specific initialization
                            AddExplorerTab(tabState.TabName, tabState.CurrentFolder,
                                null, tabState.IsSelected,
                                tabState.ExpandedFolders, tabState.SelectedFolder,
                                tabState.RootFolderPath, tabState.WslDistroName);
                        }
                        else
                        {
                            AddExplorerTab(tabState.TabName, tabState.CurrentFolder,
                                tabState.Drives.Count > 0 ? tabState.Drives : null,
                                tabState.IsSelected,
                                tabState.ExpandedFolders, tabState.SelectedFolder,
                                tabState.RootFolderPath);
                        }
                        break;
                    case TabType.Services:
                        AddServicesTab(tabState.TabName, tabState.VisibleServices,
                            tabState.MachineName, tabState.SearchPattern,
                            tabState.IsSelected);
                        break;
                    case TabType.HybridServices:
                        AddHybridServicesTab(tabState.TabName, tabState.VisibleServices,
                            tabState.MachineName, tabState.SearchPattern,
                            tabState.IsSelected,
                            tabState.ServiceOrder, tabState.DockerContainerOrder,
                            tabState.SplitterPosition, tabState.SplitterPosition2,
                            tabState.ColumnWidths, tabState.ColumnWidths2);
                        break;
                    case TabType.Network:
                        AddNetworkTab(tabState.TabName, tabState.IsSelected,
                            tabState.NetworkListeningOnly, tabState.NetworkTcpOnly,
                            tabState.NetworkSearchText, tabState.NetworkHiddenProcesses,
                            tabState.NetworkSortColumn, tabState.NetworkSortDirection,
                            tabState.NetworkColumnWidths);
                        break;
                    case TabType.Terminal:
                        AddTerminalTab(tabState.TabName, tabState.IsSelected,
                            tabState.TerminalShellCommand, tabState.TerminalDirectory);
                        break;
                    case TabType.TextViewer:
                        AddTextViewerTab(tabState.TabName, tabState.IsSelected,
                            tabState.TextViewerFilePath, tabState.TextViewerWordWrap,
                            tabState.TextViewerIsEditing);
                        break;
                    case TabType.Chat:
                        AddChatTab(tabState.TabName, tabState.IsSelected, tabState.ChatModel);
                        break;
                }
            }
        }

        _isInitializing = false;
    }

    public void AddExplorerTab(string name, string? currentFolder, List<string>? drives, bool isSelected,
        List<string>? expandedFolders = null, string? selectedFolder = null,
        string? rootFolderPath = null, string? wslDistroName = null,
        double? splitterPosition = null, Dictionary<string, double>? columnWidths = null)
    {
        var panel = new ExplorerPanel();
        var tabItem = new TabItem
        {
            Header = name,
            HeaderTemplate = (DataTemplate)FindResource("ClosableTabHeader"),
            Content = panel
        };

        // Insert before the "+" tab (which is always the last item)
        var insertIndex = MainTabControl.Items.IndexOf(AddTabButton);
        if (insertIndex >= 0)
            MainTabControl.Items.Insert(insertIndex, tabItem);
        else
            MainTabControl.Items.Add(tabItem);

        if (isSelected)
        {
            MainTabControl.SelectedItem = tabItem;
        }

        // Initialize the explorer panel after the visual tree has fully settled.
        // Using DispatcherPriority.Background avoids re-entrancy issues that occur
        // when panel.Loaded fires during the layout pass of MainWindow_Loaded.
        Dispatcher.InvokeAsync(async () =>
        {
            if (splitterPosition.HasValue)
                panel.SetSplitterPosition(splitterPosition.Value);
            if (columnWidths is { Count: > 0 })
                panel.SetFileGridColumnWidths(columnWidths);
            await panel.InitializeAsync(currentFolder, drives, _pluginManager, _launcherService,
                expandedFolders, selectedFolder, rootFolderPath, wslDistroName);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>
    /// Creates a new file explorer tab rooted at a specific folder.
    /// The tab title shows the folder's short name.
    /// </summary>
    public void AddRootedExplorerTab(string folderPath)
    {
        var shortName = System.IO.Path.GetFileName(folderPath.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(shortName))
            shortName = folderPath;

        AddExplorerTab(shortName, null, null, isSelected: true, rootFolderPath: folderPath);
    }

    /// <summary>
    /// Creates a new WSL file explorer tab rooted at the specified distro.
    /// Optionally roots at a specific subfolder within the distro.
    /// </summary>
    public void AddWslExplorerTab(string distroName, string? rootSubPath = null)
    {
        if (!Kexplorer.Core.FileSystem.WslPathHelper.IsDistroAvailable(distroName))
        {
            UpdateStatus($"WSL distro '{distroName}' not available \u2014 is WSL installed and the distro running?");
            return;
        }

        if (!string.IsNullOrEmpty(rootSubPath))
        {
            // Rooted at a specific subfolder within the WSL distro
            var shortName = System.IO.Path.GetFileName(rootSubPath.TrimEnd('\\', '/'));
            if (string.IsNullOrEmpty(shortName))
                shortName = distroName;

            AddExplorerTab($"{shortName} (WSL)", null, null, isSelected: true,
                rootFolderPath: rootSubPath, wslDistroName: distroName);
        }
        else
        {
            // Rooted at the distro root
            AddExplorerTab($"{distroName} (WSL)", null, null, isSelected: true,
                wslDistroName: distroName);
        }
    }

    public void AddServicesTab(string name, List<string>? visibleServices,
        string? machineName, string? searchPattern, bool isSelected)
    {
        var panel = new ServicesPanel();
        var tabItem = new TabItem
        {
            Header = name,
            HeaderTemplate = (DataTemplate)FindResource("ClosableTabHeader"),
            Content = panel
        };

        // Insert before the "+" tab
        var insertIndex = MainTabControl.Items.IndexOf(AddTabButton);
        if (insertIndex >= 0)
            MainTabControl.Items.Insert(insertIndex, tabItem);
        else
            MainTabControl.Items.Add(tabItem);

        if (isSelected)
        {
            MainTabControl.SelectedItem = tabItem;
        }

        panel.Loaded += async (s, e) =>
        {
            await panel.InitializeAsync(visibleServices, machineName, searchPattern, _pluginManager);
        };
    }

    public void AddHybridServicesTab(string name, List<string>? visibleServices,
        string? machineName, string? searchPattern, bool isSelected,
        List<string>? serviceOrder = null, List<string>? dockerContainerOrder = null,
        double? splitterPosition = null, double? splitterPosition2 = null,
        Dictionary<string, double>? columnWidths = null, Dictionary<string, double>? columnWidths2 = null)
    {
        var panel = new HybridServicesPanel();
        var tabItem = new TabItem
        {
            Header = name,
            HeaderTemplate = (DataTemplate)FindResource("ClosableTabHeader"),
            Content = panel
        };

        // Insert before the "+" tab
        var insertIndex = MainTabControl.Items.IndexOf(AddTabButton);
        if (insertIndex >= 0)
            MainTabControl.Items.Insert(insertIndex, tabItem);
        else
            MainTabControl.Items.Add(tabItem);

        if (isSelected)
        {
            MainTabControl.SelectedItem = tabItem;
        }

        panel.Loaded += async (s, e) =>
        {
            if (splitterPosition.HasValue || splitterPosition2.HasValue)
                panel.SetSplitterPositions(splitterPosition, splitterPosition2);
            if (columnWidths is { Count: > 0 })
                panel.SetServiceColumnWidths(columnWidths);
            if (columnWidths2 is { Count: > 0 })
                panel.SetDockerColumnWidths(columnWidths2);
            await panel.InitializeAsync(visibleServices, machineName, searchPattern, _pluginManager,
                _launcherService, serviceOrder, dockerContainerOrder);
        };
    }

    public void AddNetworkTab(string name, bool isSelected,
        bool? listeningOnly = null, bool? tcpOnly = null,
        string? searchText = null, List<string>? hiddenProcesses = null,
        string? sortColumn = null, string? sortDirection = null,
        Dictionary<string, double>? columnWidths = null)
    {
        var panel = new NetworkPanel();
        var tabItem = new TabItem
        {
            Header = name,
            HeaderTemplate = (DataTemplate)FindResource("ClosableTabHeader"),
            Content = panel
        };

        var insertIndex = MainTabControl.Items.IndexOf(AddTabButton);
        if (insertIndex >= 0)
            MainTabControl.Items.Insert(insertIndex, tabItem);
        else
            MainTabControl.Items.Add(tabItem);

        if (isSelected)
        {
            MainTabControl.SelectedItem = tabItem;
        }

        panel.Loaded += async (s, e) =>
        {
            await panel.InitializeAsync(listeningOnly, tcpOnly, searchText,
                hiddenProcesses, sortColumn, sortDirection, columnWidths);
        };
    }

    public void AddTerminalTab(string name, bool isSelected,
        string? shellCommand = null, string? initialDirectory = null)
    {
        var panel = new TerminalPanel();
        var tabItem = new TabItem
        {
            Header = name,
            HeaderTemplate = (DataTemplate)FindResource("ClosableTabHeader"),
            Content = panel
        };

        var insertIndex = MainTabControl.Items.IndexOf(AddTabButton);
        if (insertIndex >= 0)
            MainTabControl.Items.Insert(insertIndex, tabItem);
        else
            MainTabControl.Items.Add(tabItem);

        if (isSelected)
        {
            MainTabControl.SelectedItem = tabItem;
        }

        // Initialize once via Dispatcher — avoids re-init on every tab switch
        // (WPF's TabControl fires Loaded each time the tab is re-selected)
        Dispatcher.InvokeAsync(async () =>
        {
            await panel.InitializeAsync(shellCommand ?? "wsl.exe", initialDirectory);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    public void AddTextViewerTab(string name, bool isSelected,
        string? filePath = null, bool? wordWrap = null, bool? isEditing = null)
    {
        var panel = new TextViewerPanel();
        var tabItem = new TabItem
        {
            Header = name,
            HeaderTemplate = (DataTemplate)FindResource("ClosableTabHeader"),
            Content = panel
        };

        var insertIndex = MainTabControl.Items.IndexOf(AddTabButton);
        if (insertIndex >= 0)
            MainTabControl.Items.Insert(insertIndex, tabItem);
        else
            MainTabControl.Items.Add(tabItem);

        if (isSelected)
        {
            MainTabControl.SelectedItem = tabItem;
        }

        Dispatcher.InvokeAsync(async () =>
        {
            await panel.InitializeAsync(filePath, wordWrap, isEditing);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    public void AddChatTab(string name, bool isSelected, string? model = null)
    {
        var panel = new ChatPanel();
        var tabItem = new TabItem
        {
            Header = name,
            HeaderTemplate = (DataTemplate)FindResource("ClosableTabHeader"),
            Content = panel
        };

        var insertIndex = MainTabControl.Items.IndexOf(AddTabButton);
        if (insertIndex >= 0)
            MainTabControl.Items.Insert(insertIndex, tabItem);
        else
            MainTabControl.Items.Add(tabItem);

        if (isSelected)
        {
            MainTabControl.SelectedItem = tabItem;
        }

        Dispatcher.InvokeAsync(async () =>
        {
            await panel.InitializeAsync(model);
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private TabItem? _lastSelectedTab;

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabControl.SelectedItem == AddTabButton)
        {
            // Only show the new-tab menu when the user actually clicks the "+" tab,
            // not when programmatic tab insertions momentarily shift selection to it.
            if (!_isInitializing && AddTabButton.IsMouseOver)
                ShowNewTabMenu();

            // Revert to the previously selected real tab
            if (_lastSelectedTab is not null && MainTabControl.Items.Contains(_lastSelectedTab))
                MainTabControl.SelectedItem = _lastSelectedTab;
            else if (MainTabControl.Items.Count > 1)
                MainTabControl.SelectedIndex = MainTabControl.Items.Count - 2;
            return;
        }

        if (MainTabControl.SelectedItem is TabItem tab)
        {
            _lastSelectedTab = tab;

            if (tab.Content is ExplorerPanel explorer)
            {
                StatusText.Text = explorer.CurrentPath ?? "Ready";
            }
            else if (tab.Content is HybridServicesPanel)
            {
                StatusText.Text = "Hybrid Services";
            }
            else if (tab.Content is NetworkPanel)
            {
                StatusText.Text = "Network Ports";
            }
            else if (tab.Content is ServicesPanel)
            {
                StatusText.Text = "Services";
            }
            else if (tab.Content is TerminalPanel)
            {
                StatusText.Text = "Terminal";
            }
            else if (tab.Content is TextViewerPanel textViewer)
            {
                StatusText.Text = textViewer.FilePath ?? "Text Viewer";
            }
            else if (tab.Content is ChatPanel)
            {
                StatusText.Text = "Copilot Chat";
            }
        }
    }

    private void ShowNewTabMenu()
    {
        var menu = new ContextMenu();

        var explorerItem = new MenuItem { Header = "New File Explorer Tab" };
        explorerItem.Click += (s, e) =>
        {
            var tabCount = MainTabControl.Items.Count; // includes the + tab
            AddExplorerTab($"Tab {tabCount}", null, null, isSelected: true);
        };
        menu.Items.Add(explorerItem);

        var servicesItem = new MenuItem { Header = "New Services Tab (deprecated)" };
        servicesItem.Click += (s, e) =>
        {
            var patternDialog = new PromptDialog(
                "Service Filter",
                "Enter a regex pattern to filter services (leave blank for all):",
                ".*");

            if (patternDialog.ShowDialog() == true)
            {
                var pattern = patternDialog.ResponseText?.Trim();
                if (string.IsNullOrEmpty(pattern))
                    pattern = null;

                var tabName = pattern is not null ? $"Services ({pattern})" : "Services";
                AddServicesTab(tabName, null, null, pattern, isSelected: true);
            }
        };
        menu.Items.Add(servicesItem);

        var hybridServicesItem = new MenuItem { Header = "New Hybrid Services Tab" };
        hybridServicesItem.Click += (s, e) =>
        {
            var patternDialog = new PromptDialog(
                "Service Filter",
                "Enter a regex pattern to filter services (leave blank for all):",
                ".*");

            if (patternDialog.ShowDialog() == true)
            {
                var pattern = patternDialog.ResponseText?.Trim();
                if (string.IsNullOrEmpty(pattern))
                    pattern = null;

                var tabName = pattern is not null ? $"Hybrid Services ({pattern})" : "Hybrid Services";
                AddHybridServicesTab(tabName, null, null, pattern, isSelected: true);
            }
        };
        menu.Items.Add(hybridServicesItem);

        var networkItem = new MenuItem { Header = "New Network Ports Tab" };
        networkItem.Click += (s, e) =>
        {
            AddNetworkTab("Network Ports", isSelected: true);
        };
        menu.Items.Add(networkItem);

        var wslItem = new MenuItem { Header = "New WSL File Explorer Tab" };
        wslItem.Click += (s, e) =>
        {
            var distroDialog = new PromptDialog(
                "WSL Distro",
                "Enter the WSL distro name:",
                "Ubuntu");

            if (distroDialog.ShowDialog() == true)
            {
                var distro = distroDialog.ResponseText?.Trim();
                if (!string.IsNullOrEmpty(distro))
                {
                    AddWslExplorerTab(distro);
                }
            }
        };
        menu.Items.Add(wslItem);

        menu.Items.Add(new Separator());

        var terminalBashItem = new MenuItem { Header = "New Terminal Tab (bash)" };
        terminalBashItem.Click += (s, e) =>
        {
            AddTerminalTab("Terminal (bash)", isSelected: true, "wsl.exe");
        };
        menu.Items.Add(terminalBashItem);

        var terminalPsItem = new MenuItem { Header = "New Terminal Tab (PowerShell)" };
        terminalPsItem.Click += (s, e) =>
        {
            AddTerminalTab("Terminal (PS)", isSelected: true, "powershell.exe");
        };
        menu.Items.Add(terminalPsItem);

        var terminalCmdItem = new MenuItem { Header = "New Terminal Tab (cmd)" };
        terminalCmdItem.Click += (s, e) =>
        {
            AddTerminalTab("Terminal (cmd)", isSelected: true, "cmd.exe");
        };
        menu.Items.Add(terminalCmdItem);

        menu.Items.Add(new Separator());

        var chatItem = new MenuItem { Header = "New Chat Tab (Copilot)" };
        chatItem.Click += (s, e) =>
        {
            AddChatTab("Chat", isSelected: true);
        };
        menu.Items.Add(chatItem);

        menu.IsOpen = true;
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save state
        _sessionState.WindowWidth = Width;
        _sessionState.WindowHeight = Height;
        _sessionState.WindowLeft = Left;
        _sessionState.WindowTop = Top;
        _sessionState.ThemeName = _sessionState.ThemeName; // already up-to-date via menu
        _sessionState.Tabs.Clear();

        foreach (TabItem tab in MainTabControl.Items)
        {
            if (tab == AddTabButton) continue; // skip the "+" tab

            if (tab.Content is ExplorerPanel explorer)
            {
                _sessionState.Tabs.Add(new TabState
                {
                    TabName = tab.Header?.ToString() ?? "Tab",
                    TabType = TabType.FileExplorer,
                    CurrentFolder = explorer.CurrentPath,
                    Drives = explorer.LoadedDrives,
                    IsSelected = MainTabControl.SelectedItem == tab,
                    ExpandedFolders = explorer.GetExpandedFolders(),
                    SelectedFolder = explorer.CurrentPath,
                    RootFolderPath = explorer.RootFolderPath,
                    ExplorerType = explorer.WslDistroName is not null ? "WSL" : null,
                    WslDistroName = explorer.WslDistroName,
                    SplitterPosition = explorer.GetSplitterPosition(),
                    ColumnWidths = explorer.GetFileGridColumnWidths()
                });
            }
            else if (tab.Content is HybridServicesPanel hybrid)
            {
                var splitters = hybrid.GetSplitterPositions();
                _sessionState.Tabs.Add(new TabState
                {
                    TabName = tab.Header?.ToString() ?? "Hybrid Services",
                    TabType = TabType.HybridServices,
                    VisibleServices = hybrid.GetVisibleServiceNames(),
                    MachineName = hybrid.MachineName,
                    SearchPattern = hybrid.SearchPattern,
                    IsSelected = MainTabControl.SelectedItem == tab,
                    ServiceOrder = hybrid.GetServiceOrder(),
                    DockerContainerOrder = hybrid.GetDockerContainerOrder(),
                    SplitterPosition = splitters.services,
                    SplitterPosition2 = splitters.docker,
                    ColumnWidths = hybrid.GetServiceColumnWidths(),
                    ColumnWidths2 = hybrid.GetDockerColumnWidths()
                });
            }
            else if (tab.Content is ServicesPanel services)
            {
                _sessionState.Tabs.Add(new TabState
                {
                    TabName = tab.Header?.ToString() ?? "Services",
                    TabType = TabType.Services,
                    VisibleServices = services.GetVisibleServiceNames(),
                    MachineName = services.MachineName,
                    SearchPattern = services.SearchPattern,
                    IsSelected = MainTabControl.SelectedItem == tab
                });
            }
            else if (tab.Content is NetworkPanel networkTab)
            {
                var sort = networkTab.GetSortState();
                _sessionState.Tabs.Add(new TabState
                {
                    TabName = tab.Header?.ToString() ?? "Network Ports",
                    TabType = TabType.Network,
                    IsSelected = MainTabControl.SelectedItem == tab,
                    NetworkListeningOnly = networkTab.IsListeningOnly,
                    NetworkTcpOnly = networkTab.IsTcpOnly,
                    NetworkSearchText = networkTab.SearchText,
                    NetworkHiddenProcesses = networkTab.HiddenProcesses,
                    NetworkSortColumn = sort.column,
                    NetworkSortDirection = sort.direction,
                    NetworkColumnWidths = networkTab.GetColumnWidths()
                });
            }
            else if (tab.Content is TerminalPanel terminalTab)
            {
                _sessionState.Tabs.Add(new TabState
                {
                    TabName = tab.Header?.ToString() ?? "Terminal",
                    TabType = TabType.Terminal,
                    IsSelected = MainTabControl.SelectedItem == tab,
                    TerminalShellCommand = terminalTab.ShellCommand,
                    TerminalDirectory = terminalTab.InitialDirectory
                });
            }
            else if (tab.Content is TextViewerPanel textViewerTab)
            {
                _sessionState.Tabs.Add(new TabState
                {
                    TabName = tab.Header?.ToString() ?? "Text Viewer",
                    TabType = TabType.TextViewer,
                    IsSelected = MainTabControl.SelectedItem == tab,
                    TextViewerFilePath = textViewerTab.FilePath,
                    TextViewerWordWrap = textViewerTab.IsWordWrap,
                    TextViewerIsEditing = textViewerTab.IsEditing
                });
            }
            else if (tab.Content is ChatPanel chatTab)
            {
                _sessionState.Tabs.Add(new TabState
                {
                    TabName = tab.Header?.ToString() ?? "Chat",
                    TabType = TabType.Chat,
                    IsSelected = MainTabControl.SelectedItem == tab,
                    ChatModel = chatTab.SelectedModel
                });
            }
        }

        await SessionStateManager.SaveAsync(_sessionState);

        // Stop all work queues
        foreach (TabItem tab in MainTabControl.Items.OfType<TabItem>())
        {
            if (tab.Content is ExplorerPanel explorer)
            {
                await explorer.ShutdownAsync();
            }
            else if (tab.Content is HybridServicesPanel hybrid)
            {
                await hybrid.ShutdownAsync();
            }
            else if (tab.Content is NetworkPanel networkPanel)
            {
                await networkPanel.ShutdownAsync();
            }
            else if (tab.Content is ServicesPanel services)
            {
                await services.ShutdownAsync();
            }
            else if (tab.Content is TerminalPanel terminal)
            {
                await terminal.ShutdownAsync();
            }
            else if (tab.Content is TextViewerPanel textViewerPanel)
            {
                await textViewerPanel.ShutdownAsync();
            }
            else if (tab.Content is ChatPanel chatPanel)
            {
                await chatPanel.ShutdownAsync();
            }
        }

        // Explicitly shut down the application — the async continuations in
        // Window_Closing can keep the process alive after the window is gone.
        Application.Current.Shutdown();
    }

    #region Tab Rename (double-click header)

    private void TabLabel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is TextBlock label)
        {
            // Find the sibling TextBox and switch to edit mode
            if (label.Parent is StackPanel sp)
            {
                var editBox = FindChild<TextBox>(sp);
                if (editBox is not null)
                {
                    label.Visibility = Visibility.Collapsed;
                    editBox.Text = label.Text;
                    editBox.Visibility = Visibility.Visible;
                    editBox.Focus();
                    editBox.SelectAll();
                }
            }
            e.Handled = true;
        }
    }

    private void TabEditBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox editBox) return;

        if (e.Key == Key.Enter)
        {
            CommitTabRename(editBox);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelTabRename(editBox);
            e.Handled = true;
        }
    }

    private void TabEditBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox editBox)
        {
            CommitTabRename(editBox);
        }
    }

    private void CommitTabRename(TextBox editBox)
    {
        if (editBox.Parent is not StackPanel sp) return;

        var label = FindChild<TextBlock>(sp);
        if (label is null) return;

        var newName = editBox.Text.Trim();
        if (string.IsNullOrEmpty(newName))
            newName = label.Text; // revert if empty

        // Update the TabItem.Header (the data binding source)
        var tabItem = FindParent<TabItem>(sp);
        if (tabItem is not null)
            tabItem.Header = newName;

        label.Text = newName;
        label.Visibility = Visibility.Visible;
        editBox.Visibility = Visibility.Collapsed;
    }

    private void CancelTabRename(TextBox editBox)
    {
        if (editBox.Parent is not StackPanel sp) return;

        var label = FindChild<TextBlock>(sp);
        if (label is null) return;

        label.Visibility = Visibility.Visible;
        editBox.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Tab Close

    private async void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        var tabItem = FindParent<TabItem>(sender as DependencyObject);
        if (tabItem is null || tabItem == AddTabButton) return;

        // Don't close the last real tab
        var realTabCount = MainTabControl.Items.Cast<object>().Count(i => i != AddTabButton);
        if (realTabCount <= 1) return;

        // Shut down the tab's work queues
        if (tabItem.Content is ExplorerPanel explorer)
            await explorer.ShutdownAsync();
        else if (tabItem.Content is HybridServicesPanel hybrid)
            await hybrid.ShutdownAsync();
        else if (tabItem.Content is NetworkPanel network)
            await network.ShutdownAsync();
        else if (tabItem.Content is ServicesPanel services)
            await services.ShutdownAsync();
        else if (tabItem.Content is TerminalPanel terminal)
            await terminal.ShutdownAsync();

        // Select a neighbor before removing
        if (MainTabControl.SelectedItem == tabItem)
        {
            var idx = MainTabControl.Items.IndexOf(tabItem);
            if (idx > 0)
                MainTabControl.SelectedIndex = idx - 1;
            else if (MainTabControl.Items.Count > 2) // at least the other tab + "+"
                MainTabControl.SelectedIndex = 1;
        }

        MainTabControl.Items.Remove(tabItem);
    }

    #endregion

    #region Visual Tree Helpers

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;
            var nested = FindChild<T>(child);
            if (nested is not null)
                return nested;
        }
        // Fallback for logical children (template hasn't been applied yet)
        if (parent is Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is T result)
                    return result;
            }
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            child = VisualTreeHelper.GetParent(child);
            if (child is T result)
                return result;
        }
        return null;
    }

    #endregion

    public void UpdateStatus(string message)
    {
        Dispatcher.InvokeAsync(() => StatusText.Text = message);
    }

    private void SettingsGear_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AboutDialog(_sessionState.ThemeName, theme =>
        {
            ThemeManager.ApplyTheme(theme);
            _sessionState.ThemeName = theme;
        });
        dialog.Owner = this;
        dialog.ShowDialog();
    }
}

