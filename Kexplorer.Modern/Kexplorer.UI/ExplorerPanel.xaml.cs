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
    private CancellationTokenSource? _restoreCts;
    private bool _restoringNavigation;

    public string? CurrentPath { get; private set; }
    public List<string> LoadedDrives => _loadedDrives;

    public ExplorerPanel()
    {
        InitializeComponent();
        FileGrid.ItemsSource = _fileItems;
    }

    public async Task InitializeAsync(string? currentFolder, List<string>? drives,
        PluginManager pluginManager, LauncherService launcherService,
        List<string>? expandedFolders = null, string? selectedFolder = null)
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

        // If we have expanded folders to restore, do it after drives load
        if (expandedFolders is { Count: > 0 } || !string.IsNullOrEmpty(selectedFolder))
        {
            var foldersToRestore = expandedFolders ?? new List<string>();
            var folderToSelect = selectedFolder ?? currentFolder;
            _ = Task.Run(async () =>
            {
                await Task.Delay(800); // let drive loading complete
                await RestoreNavigationStateAsync(foldersToRestore, folderToSelect);
            });
        }
        else if (!string.IsNullOrEmpty(currentFolder))
        {
            CurrentPath = currentFolder;
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
        // If the user clicks manually while restoration is in progress, cancel it
        if (_restoringNavigation)
        {
            _restoreCts?.Cancel();
        }

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

    /// <summary>
    /// Collect all expanded folder paths in the tree (for state persistence).
    /// </summary>
    public List<string> GetExpandedFolders()
    {
        var result = new List<string>();
        foreach (var item in FolderTree.Items)
        {
            if (item is TreeViewItem treeItem)
                CollectAllExpandedPaths(treeItem, result);
        }
        return result;
    }

    private void CollectAllExpandedPaths(TreeViewItem item, List<string> result)
    {
        if (item.IsExpanded && item.Tag is FileSystemNode node)
        {
            result.Add(node.FullPath);
            foreach (var child in item.Items)
            {
                if (child is TreeViewItem childItem)
                    CollectAllExpandedPaths(childItem, result);
            }
        }
    }

    /// <summary>
    /// Restore navigation state: expand persisted folders, then select the target folder.
    /// </summary>
    private async Task RestoreNavigationStateAsync(List<string> expandedFolders, string? selectedFolder)
    {
        _restoreCts = new CancellationTokenSource();
        _restoringNavigation = true;
        var ct = _restoreCts.Token;

        try
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                mainWindow?.UpdateStatus("Restoring navigation\u2026");
            });

            // Group expanded paths by drive letter to parallelize across drives
            var pathsByDrive = expandedFolders
                .Where(p => p.Length >= 2 && p[1] == ':')
                .GroupBy(p => p[..1].ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Length).ToList());

            // Expand each drive's paths
            var tasks = new List<Task>();
            foreach (var (drive, paths) in pathsByDrive)
            {
                tasks.Add(RestoreDrivePathsAsync(drive, paths, ct));
            }
            await Task.WhenAll(tasks);

            // Select the target folder
            if (!ct.IsCancellationRequested && !string.IsNullOrEmpty(selectedFolder))
            {
                CurrentPath = selectedFolder;
                await NavigateToPathAsync(selectedFolder, ct);
            }

            await Dispatcher.InvokeAsync(() =>
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                mainWindow?.UpdateStatus(CurrentPath ?? "Ready");
            });
        }
        catch (OperationCanceledException)
        {
            // User navigated manually — stop restoring
        }
        finally
        {
            _restoringNavigation = false;
        }
    }

    private async Task RestoreDrivePathsAsync(string driveLetter, List<string> paths, CancellationToken ct)
    {
        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();
            await RestoreExpandedPathAsync(path, ct);
        }
    }

    private async Task RestoreExpandedPathAsync(string fullPath, CancellationToken ct)
    {
        // Decompose path into segments: C:\, Users, dev, projects
        var segments = DecomposePath(fullPath);
        if (segments.Count == 0) return;

        // The first segment is the drive root (e.g. "C:\")
        var driveRoot = segments[0];

        TreeViewItem? current = null;
        await Dispatcher.InvokeAsync(() =>
        {
            current = FindTreeItem(FolderTree.Items, driveRoot);
        });

        if (current is null) return;

        for (int i = 1; i < segments.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var segment = segments[i];

            // Ensure children are loaded — trigger expand and wait
            bool needsLoad = false;
            var currentCapture = current;
            await Dispatcher.InvokeAsync(() =>
            {
                if (currentCapture!.Tag is FileSystemNode node && !node.Loaded)
                {
                    needsLoad = true;
                    currentCapture.IsExpanded = true;
                }
                else if (!currentCapture.IsExpanded)
                {
                    currentCapture.IsExpanded = true;
                }
            });

            if (needsLoad)
            {
                // Wait for the folder to finish loading (children populated)
                await WaitForNodeLoadedAsync(current!, ct);
            }

            // Find the matching child
            TreeViewItem? child = null;
            await Dispatcher.InvokeAsync(() =>
            {
                foreach (var item in currentCapture!.Items)
                {
                    if (item is TreeViewItem treeItem && treeItem.Tag is FileSystemNode childNode &&
                        string.Equals(childNode.Name, segment, StringComparison.OrdinalIgnoreCase))
                    {
                        child = treeItem;
                        break;
                    }
                }
            });

            if (child is null)
                break; // Folder no longer exists

            current = child;
        }

        // Expand the final node
        if (current is not null)
        {
            await Dispatcher.InvokeAsync(() => current.IsExpanded = true);
        }
    }

    private async Task WaitForNodeLoadedAsync(TreeViewItem treeItem, CancellationToken ct)
    {
        // Poll until the node's children are loaded (FileSystemNode.Loaded == true)
        for (int i = 0; i < 60; i++) // up to ~6 seconds
        {
            ct.ThrowIfCancellationRequested();
            bool loaded = false;
            await Dispatcher.InvokeAsync(() =>
            {
                if (treeItem.Tag is FileSystemNode node)
                    loaded = node.Loaded;
            });
            if (loaded) return;
            await Task.Delay(100, ct);
        }
    }

    private static List<string> DecomposePath(string fullPath)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(fullPath)) return result;

        // First segment is the drive root
        if (fullPath.Length >= 3 && fullPath[1] == ':' && fullPath[2] == '\\')
        {
            result.Add(fullPath[..3]); // "C:\"
            var remaining = fullPath[3..];
            if (!string.IsNullOrEmpty(remaining))
            {
                result.AddRange(remaining.Split('\\', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        return result;
    }

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
