using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Launching;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Shell;
using Kexplorer.Core.Work;

namespace Kexplorer.UI;

/// <summary>
/// WPF user control replacing the legacy KexplorerPanel (TreeView + DataGridView).
/// Each file-explorer tab gets its own ExplorerPanel with its own work queues.
/// </summary>
public partial class ExplorerPanel : UserControl, IKexplorerShell
{
    private WorkQueue? _mainQueue;
    private readonly Dictionary<string, WorkQueue> _driveQueues = new(StringComparer.OrdinalIgnoreCase);
    private PluginManager? _pluginManager;
    private LauncherService? _launcherService;
    private PluginContextAdapter? _pluginContext;
    private bool _initialized;

    // Observable collection for the file grid
    private readonly ObservableCollection<FileGridItem> _fileItems = new();

    // Track loaded drives for state persistence
    private readonly List<string> _loadedDrives = new();

    public string? CurrentPath { get; private set; }
    public List<string> LoadedDrives => _loadedDrives;

    public ExplorerPanel()
    {
        InitializeComponent();
        FileGrid.ItemsSource = _fileItems;
    }

    public async Task InitializeAsync(string? currentFolder, List<string>? drives,
        PluginManager pluginManager, LauncherService launcherService)
    {
        if (_initialized)
            return;
        _initialized = true;

        _pluginManager = pluginManager;
        _launcherService = launcherService;

        // Create the main work queue (for file list loading)
        _mainQueue = new WorkQueue(this, new WorkQueueOptions { WorkerCount = 1 });
        await _mainQueue.StartAsync();

        // Create plugin context
        _pluginContext = new PluginContextAdapter(this, _mainQueue, _launcherService);

        // Load drives
        var driveList = drives ?? new List<string>(Directory.GetLogicalDrives());
        foreach (var drive in driveList)
        {
            var driveLetter = drive.TrimEnd('\\', '/');
            if (!driveLetter.EndsWith(':'))
                driveLetter += ":";
            var fullPath = driveLetter + "\\";

            _loadedDrives.Add(fullPath);

            // Create a per-drive work queue
            var driveQueue = new WorkQueue(this, new WorkQueueOptions { WorkerCount = 1 });
            _driveQueues[driveLetter.Substring(0, 1)] = driveQueue;
            await driveQueue.StartAsync();

            // Add drive node to tree
            var treeItem = new TreeViewItem
            {
                Header = fullPath,
                Tag = new FileSystemNode(fullPath, fullPath, isDirectory: true)
            };
            // Add a dummy child so the expand arrow is shown
            treeItem.Items.Add(new TreeViewItem { Header = "Loading..." });
            FolderTree.Items.Add(treeItem);

            // Enqueue drive loading
            await driveQueue.EnqueueAsync(new DriveLoaderWorkItem(fullPath));
        }

        // If we have a current folder to navigate to, do it after drives load
        if (!string.IsNullOrEmpty(currentFolder))
        {
            CurrentPath = currentFolder;
            // Delay navigation slightly to let drive loading complete
            _ = Task.Run(async () =>
            {
                await Task.Delay(500);
                await NavigateToPathAsync(currentFolder);
            });
        }
    }

    public async Task ShutdownAsync()
    {
        if (_mainQueue is not null)
            await _mainQueue.StopAsync();

        foreach (var queue in _driveQueues.Values)
            await queue.StopAsync();
    }

    #region Tree View Events

    private async void FolderTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem treeItem && treeItem.Tag is FileSystemNode node && node.IsDirectory)
        {
            CurrentPath = node.FullPath;

            // Update status bar
            var mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.UpdateStatus(node.FullPath);

            // If the node is stale, reload its children
            if (node.Stale && node.FullPath.Length >= 2 && node.FullPath[1] == ':')
            {
                var drive = node.FullPath[..1];
                if (_driveQueues.TryGetValue(drive, out var driveQueue))
                {
                    await driveQueue.EnqueueAsync(new FolderLoaderWorkItem(node.FullPath));
                }
            }

            // Load file list (always in main queue)
            if (_mainQueue is not null)
            {
                await _mainQueue.EnqueueAsync(new FileListWorkItem(node.FullPath));
            }
        }
    }

    private async void FolderTree_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem treeItem && treeItem.Tag is FileSystemNode node)
        {
            var drive = node.DriveLetter;
            if (drive is not null && _driveQueues.TryGetValue(drive, out var driveQueue))
            {
                await driveQueue.EnqueueAsync(new FolderLoaderWorkItem(node.FullPath));
            }
        }
    }

    private void FolderTree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5 && FolderTree.SelectedItem is TreeViewItem treeItem
            && treeItem.Tag is FileSystemNode node)
        {
            node.MarkStale();
            FolderTree_SelectedItemChanged(sender,
                new RoutedPropertyChangedEventArgs<object>(null!, treeItem));
            e.Handled = true;
        }
    }

    #endregion

    #region File Grid Events

    private void FileGrid_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (FileGrid.SelectedItem is FileGridItem item)
        {
            _launcherService?.Launch(item.FullPath);
        }
    }

    private void FileGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && FileGrid.SelectedItem is FileGridItem item)
        {
            _launcherService?.Launch(item.FullPath);
            e.Handled = true;
        }
    }

    #endregion

    #region IKexplorerShell Implementation

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

    public Task RefreshPathAsync(string path, CancellationToken cancellationToken = default)
    {
        // Find the tree item for this path and mark it stale
        Dispatcher.InvokeAsync(() =>
        {
            var treeItem = FindTreeItem(FolderTree.Items, path);
            if (treeItem?.Tag is FileSystemNode node)
            {
                node.MarkStale();
            }
        });
        return Task.CompletedTask;
    }

    public Task SetTreeChildrenAsync(string parentPath, IReadOnlyList<FileSystemNode> children, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var parentItem = FindTreeItem(FolderTree.Items, parentPath);
            if (parentItem is null)
                return;

            // Preserve expanded state of existing children
            var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectExpandedPaths(parentItem, expandedPaths);

            parentItem.Items.Clear();

            foreach (var child in children)
            {
                var childItem = CreateTreeItem(child);
                parentItem.Items.Add(childItem);

                // Restore expanded state
                if (expandedPaths.Contains(child.FullPath))
                {
                    childItem.IsExpanded = true;
                }
            }

            if (parentItem.Tag is FileSystemNode parentNode)
            {
                parentNode.Stale = false;
                parentNode.Loaded = true;
                parentNode.Children.Clear();
                parentNode.Children.AddRange(children);
            }
        });
        return Task.CompletedTask;
    }

    public Task SetFileListAsync(string directoryPath, IReadOnlyList<FileEntry> files, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            _fileItems.Clear();
            foreach (var file in files)
            {
                _fileItems.Add(new FileGridItem(file));
            }
        });
        return Task.CompletedTask;
    }

    public Task NavigateToPathAsync(string path, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var treeItem = FindTreeItem(FolderTree.Items, path);
            if (treeItem is not null)
            {
                treeItem.IsSelected = true;
                treeItem.BringIntoView();
            }
        });
        return Task.CompletedTask;
    }

    public Task RemoveTreeNodeAsync(string path, CancellationToken cancellationToken = default)
    {
        Dispatcher.InvokeAsync(() =>
        {
            var treeItem = FindTreeItem(FolderTree.Items, path);
            if (treeItem is not null)
            {
                if (treeItem.Parent is TreeViewItem parentItem)
                    parentItem.Items.Remove(treeItem);
                else
                    FolderTree.Items.Remove(treeItem);

                // Remove from loaded drives if it's a drive root
                _loadedDrives.RemoveAll(d =>
                    string.Equals(d.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
            }
        });
        return Task.CompletedTask;
    }

    #endregion

    #region Tree Helpers

    private TreeViewItem CreateTreeItem(FileSystemNode node)
    {
        var treeItem = new TreeViewItem
        {
            Header = node.Name,
            Tag = node
        };

        if (node.Children.Count > 0)
        {
            foreach (var child in node.Children)
            {
                treeItem.Items.Add(CreateTreeItem(child));
            }
        }
        else if (node.IsDirectory)
        {
            // Add dummy child for expand arrow
            treeItem.Items.Add(new TreeViewItem { Header = "Loading..." });
        }

        return treeItem;
    }

    private TreeViewItem? FindTreeItem(ItemCollection items, string path)
    {
        foreach (var item in items)
        {
            if (item is TreeViewItem treeItem)
            {
                if (treeItem.Tag is FileSystemNode node &&
                    string.Equals(node.FullPath.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                {
                    return treeItem;
                }

                var found = FindTreeItem(treeItem.Items, path);
                if (found is not null)
                    return found;
            }
        }
        return null;
    }

    private void CollectExpandedPaths(TreeViewItem parent, HashSet<string> expandedPaths)
    {
        foreach (var item in parent.Items)
        {
            if (item is TreeViewItem treeItem && treeItem.IsExpanded && treeItem.Tag is FileSystemNode node)
            {
                expandedPaths.Add(node.FullPath);
                CollectExpandedPaths(treeItem, expandedPaths);
            }
        }
    }

    #endregion

    #region Context Menu Building

    private void TreeContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        if (FolderTree.SelectedItem is TreeViewItem treeItem && treeItem.Tag is FileSystemNode node)
        {
            BuildTreeContextMenu(node);
        }
        else
        {
            TreeContextMenu.Items.Clear();
        }
    }

    private void FileContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        try
        {
            if (FileGrid.SelectedItems.Count > 0 && FileGrid.SelectedItem is FileGridItem item)
            {
                var fileEntry = new FileEntry(item.Name, item.FullPath, item.Size, item.LastModified, item.Extension);
                BuildFileContextMenu(fileEntry);
            }
            else
            {
                FileContextMenu.Items.Clear();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error building file context menu: {ex}");
            FileContextMenu.Items.Clear();
        }
    }

    internal void BuildTreeContextMenu(FileSystemNode node)
    {
        TreeContextMenu.Items.Clear();
        if (_pluginManager is null || _pluginContext is null) return;

        foreach (var plugin in _pluginManager.FolderPlugins.OrderBy(p => p.Name))
        {
            try
            {
                if (!plugin.IsActive) continue;
                if (!plugin.IsValidForFolder(node.FullPath)) continue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IsValidForFolder for {plugin.Name}: {ex}");
                continue;
            }

            var menuItem = new MenuItem { Header = plugin.Name };
            var capturedPlugin = plugin;
            menuItem.Click += async (s, e) =>
            {
                try
                {
                    await capturedPlugin.ExecuteAsync(node.FullPath, _pluginContext, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    await _pluginContext.Shell.ReportErrorAsync($"Plugin '{capturedPlugin.Name}' failed: {ex.Message}", ex);
                }
            };
            TreeContextMenu.Items.Add(menuItem);
        }
    }

    internal void BuildFileContextMenu(FileEntry file)
    {
        FileContextMenu.Items.Clear();
        if (_pluginManager is null || _pluginContext is null) return;

        foreach (var plugin in _pluginManager.FilePlugins.OrderBy(p => p.Name))
        {
            try
            {
                if (!plugin.IsActive) continue;
                if (!plugin.IsValidForFile(file)) continue;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IsValidForFile for {plugin.Name}: {ex}");
                continue;
            }

            var menuItem = new MenuItem { Header = plugin.Name };
            var capturedPlugin = plugin;
            menuItem.Click += async (s, e) =>
            {
                try
                {
                    var selectedFiles = FileGrid.SelectedItems
                        .OfType<FileGridItem>()
                        .Select(item => new FileEntry(item.Name, item.FullPath, item.Size, item.LastModified, item.Extension))
                        .ToList();
                    await capturedPlugin.ExecuteAsync(CurrentPath ?? "", selectedFiles, _pluginContext, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    await _pluginContext.Shell.ReportErrorAsync($"Plugin '{capturedPlugin.Name}' failed: {ex.Message}", ex);
                }
            };
            FileContextMenu.Items.Add(menuItem);
        }
    }

    #endregion
}

/// <summary>
/// View model for the file grid rows.
/// </summary>
public class FileGridItem
{
    public FileGridItem(FileEntry entry)
    {
        Name = entry.Name;
        FullPath = entry.FullPath;
        Size = entry.Size;
        LastModified = entry.LastModified;
        Extension = entry.Extension;
    }

    public string Name { get; }
    public string FullPath { get; }
    public long Size { get; }
    public DateTime LastModified { get; }
    public string Extension { get; }

    public string SizeDisplay => FormatSize(Size);

    private static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
