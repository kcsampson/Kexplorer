using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Kexplorer.Core.Network;

namespace Kexplorer.UI;

public partial class NetworkPanel : UserControl
{
    private List<NetworkConnection> _allConnections = new();
    private readonly ObservableCollection<NetworkConnection> _filteredConnections = new();
    private readonly HashSet<string> _hiddenProcessNames = new(StringComparer.OrdinalIgnoreCase);

    public NetworkPanel()
    {
        InitializeComponent();
        ConnectionGrid.ItemsSource = _filteredConnections;
    }

    public async Task InitializeAsync()
    {
        await LoadConnectionsAsync();
    }

    public Task ShutdownAsync() => Task.CompletedTask;

    private async Task LoadConnectionsAsync()
    {
        UpdateStatus("Loading network connections...");

        try
        {
            _allConnections = await NetworkInfoService.GetConnectionsAsync();
            ApplyFilters();
            UpdateStatus($"Loaded {_allConnections.Count} connections");
        }
        catch (Exception ex)
        {
            UpdateStatus($"Error: {ex.Message}");
        }
    }

    private void ApplyFilters()
    {
        var items = _allConnections.AsEnumerable();

        if (ListeningOnlyCheckBox.IsChecked == true)
            items = items.Where(c => c.State.Equals("LISTENING", StringComparison.OrdinalIgnoreCase));

        if (TcpOnlyCheckBox.IsChecked == true)
            items = items.Where(c => c.Protocol.StartsWith("TCP", StringComparison.OrdinalIgnoreCase));

        if (_hiddenProcessNames.Count > 0)
            items = items.Where(c => !_hiddenProcessNames.Contains(c.ProcessName));

        var search = SearchBox.Text?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            items = items.Where(c =>
                c.Protocol.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.LocalAddress.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.LocalPort.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.RemoteAddress.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.State.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Pid.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.ProcessName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.ProcessPath.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        var result = items.ToList();

        _filteredConnections.Clear();
        foreach (var conn in result)
            _filteredConnections.Add(conn);

        CountLabel.Text = $"{_filteredConnections.Count} of {_allConnections.Count}";
        UpdateHiddenIndicator();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadConnectionsAsync();
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (_allConnections.Count > 0)
            ApplyFilters();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_allConnections.Count > 0)
            ApplyFilters();
    }

    private void ConnectionGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        GridContextMenu.Items.Clear();

        if (ConnectionGrid.SelectedItem is NetworkConnection selected)
        {
            var processName = selected.ProcessName;
            var hideItem = new MenuItem
            {
                Header = $"Hide all ports from '{processName}'"
            };
            hideItem.Click += (s, args) =>
            {
                _hiddenProcessNames.Add(processName);
                ApplyFilters();
            };
            GridContextMenu.Items.Add(hideItem);
        }

        if (_hiddenProcessNames.Count > 0)
        {
            if (GridContextMenu.Items.Count > 0)
                GridContextMenu.Items.Add(new Separator());

            foreach (var name in _hiddenProcessNames.OrderBy(n => n))
            {
                var unhideItem = new MenuItem { Header = $"Show '{name}'" };
                var captured = name;
                unhideItem.Click += (s, args) =>
                {
                    _hiddenProcessNames.Remove(captured);
                    ApplyFilters();
                };
                GridContextMenu.Items.Add(unhideItem);
            }

            GridContextMenu.Items.Add(new Separator());

            var showAllItem = new MenuItem { Header = "Show all hidden processes" };
            showAllItem.Click += (s, args) =>
            {
                _hiddenProcessNames.Clear();
                ApplyFilters();
            };
            GridContextMenu.Items.Add(showAllItem);
        }

        if (GridContextMenu.Items.Count == 0)
            e.Handled = true; // nothing to show
    }

    private void ShowHiddenButton_Click(object sender, RoutedEventArgs e)
    {
        _hiddenProcessNames.Clear();
        ApplyFilters();
    }

    private void UpdateHiddenIndicator()
    {
        if (_hiddenProcessNames.Count > 0)
        {
            ShowHiddenButton.Visibility = Visibility.Visible;
            HiddenCountLabel.Text = $"({_hiddenProcessNames.Count} process{(_hiddenProcessNames.Count == 1 ? "" : "es")} hidden)";
        }
        else
        {
            ShowHiddenButton.Visibility = Visibility.Collapsed;
            HiddenCountLabel.Text = "";
        }
    }

    private void UpdateStatus(string message)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.UpdateStatus(message);
        });
    }
}
