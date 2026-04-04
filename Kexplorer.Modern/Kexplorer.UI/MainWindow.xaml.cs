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

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Load state
        _sessionState = await SessionStateManager.LoadAsync();

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
                        AddExplorerTab(tabState.TabName, tabState.CurrentFolder,
                            tabState.Drives.Count > 0 ? tabState.Drives : null,
                            tabState.IsSelected);
                        break;
                    case TabType.Services:
                        AddServicesTab(tabState.TabName, tabState.VisibleServices,
                            tabState.MachineName, tabState.SearchPattern,
                            tabState.IsSelected);
                        break;
                    case TabType.HybridServices:
                        AddHybridServicesTab(tabState.TabName, tabState.VisibleServices,
                            tabState.MachineName, tabState.SearchPattern,
                            tabState.IsSelected);
                        break;
                }
            }
        }
    }

    public void AddExplorerTab(string name, string? currentFolder, List<string>? drives, bool isSelected)
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

        // Initialize the explorer panel after it's been added to the visual tree
        panel.Loaded += async (s, e) =>
        {
            await panel.InitializeAsync(currentFolder, drives, _pluginManager, _launcherService);
        };
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
        string? machineName, string? searchPattern, bool isSelected)
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
            await panel.InitializeAsync(visibleServices, machineName, searchPattern, _pluginManager);
        };
    }

    private TabItem? _lastSelectedTab;

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MainTabControl.SelectedItem == AddTabButton)
        {
            // Don't stay on the "+" tab — show the new-tab menu and revert selection
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
            else if (tab.Content is ServicesPanel)
            {
                StatusText.Text = "Services";
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

        var servicesItem = new MenuItem { Header = "New Services Tab" };
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

        menu.IsOpen = true;
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        // Save state
        _sessionState.WindowWidth = Width;
        _sessionState.WindowHeight = Height;
        _sessionState.WindowLeft = Left;
        _sessionState.WindowTop = Top;
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
                    IsSelected = MainTabControl.SelectedItem == tab
                });
            }
            else if (tab.Content is HybridServicesPanel hybrid)
            {
                _sessionState.Tabs.Add(new TabState
                {
                    TabName = tab.Header?.ToString() ?? "Hybrid Services",
                    TabType = TabType.HybridServices,
                    VisibleServices = hybrid.GetVisibleServiceNames(),
                    MachineName = hybrid.MachineName,
                    SearchPattern = hybrid.SearchPattern,
                    IsSelected = MainTabControl.SelectedItem == tab
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
            else if (tab.Content is ServicesPanel services)
            {
                await services.ShutdownAsync();
            }
        }
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
        else if (tabItem.Content is ServicesPanel services)
            await services.ShutdownAsync();

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
}

