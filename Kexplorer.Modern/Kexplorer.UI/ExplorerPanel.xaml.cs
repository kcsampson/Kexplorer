using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
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

    /// <summary>
    /// When non-null, this tab is rooted at a specific folder rather than a drive.
    /// </summary>
    public string? RootFolderPath { get; private set; }

    /// <summary>
    /// For WSL tabs, the distro name (e.g., "Ubuntu").
    /// When non-null, this tab browses a WSL filesystem.
    /// </summary>
    public string? WslDistroName { get; private set; }

    public ExplorerPanel()
    {
        InitializeComponent();
        FileGrid.ItemsSource = _fileItems;
    }

    public async Task InitializeAsync(string? currentFolder, List<string>? drives,
        PluginManager pluginManager, LauncherService launcherService,
        List<string>? expandedFolders = null, string? selectedFolder = null,
        string? rootFolderPath = null, string? wslDistroName = null)
    {
        if (_initialized)
            return;
        _initialized = true;

        _pluginManager = pluginManager;
        _launcherService = launcherService;
        RootFolderPath = rootFolderPath;
        WslDistroName = wslDistroName;

        // Create the main work queue (for file list loading)
        _mainQueue = new WorkQueue(this, new WorkQueueOptions { WorkerCount = 1 });
        await _mainQueue.StartAsync();

        // Create plugin context
        _pluginContext = new PluginContextAdapter(this, _mainQueue, _launcherService);

        if (!string.IsNullOrEmpty(wslDistroName))
        {
            // WSL tab: rooted at \\wsl.localhost\{distroName}
            await InitializeWslTabAsync(wslDistroName, expandedFolders, selectedFolder);
        }
        else if (!string.IsNullOrEmpty(rootFolderPath))
        {
            // Folder-rooted tab: single root node showing the folder's short name
            await InitializeRootedFolderAsync(rootFolderPath, expandedFolders, selectedFolder);
        }
        else
        {
            // Standard drive-based tab
            await InitializeDriveTabAsync(currentFolder, drives, expandedFolders, selectedFolder);
        }
    }

    private async Task InitializeDriveTabAsync(string? currentFolder, List<string>? drives,
        List<string>? expandedFolders, string? selectedFolder)
    {
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

    private async Task InitializeWslTabAsync(string distroName,
        List<string>? expandedFolders, string? selectedFolder)
    {
        // If RootFolderPath was set to a WSL subfolder, use that; otherwise default to distro root
        var uncRoot = RootFolderPath is not null
                      && Kexplorer.Core.FileSystem.WslPathHelper.IsWslPath(RootFolderPath)
            ? RootFolderPath
            : Kexplorer.Core.FileSystem.WslPathHelper.GetUncRoot(distroName);
        RootFolderPath = uncRoot;

        var displayName = string.Equals(uncRoot,
            Kexplorer.Core.FileSystem.WslPathHelper.GetUncRoot(distroName),
            StringComparison.OrdinalIgnoreCase)
            ? distroName
            : Path.GetFileName(uncRoot.TrimEnd('\\', '/'));

        var rootNode = new FileSystemNode(displayName, uncRoot, isDirectory: true);

        var queue = new WorkQueue(this, new WorkQueueOptions { WorkerCount = 1 });
        _driveQueues["_wsl"] = queue;
        await queue.StartAsync();

        var treeItem = new TreeViewItem
        {
            Header = displayName,
            Tag = rootNode
        };

        if (Directory.Exists(uncRoot))
        {
            treeItem.Items.Add(new TreeViewItem { Header = "Loading..." });
            FolderTree.Items.Add(treeItem);

            await queue.EnqueueAsync(new FolderLoaderWorkItem(uncRoot));
        }
        else if (!Kexplorer.Core.FileSystem.WslPathHelper.IsDistroAvailable(distroName))
        {
            treeItem.Header = $"{displayName} (not available)";
            treeItem.Foreground = System.Windows.Media.Brushes.Gray;
            treeItem.ToolTip = $"WSL distro not available: {distroName}. Is WSL installed and the distro running?";
            FolderTree.Items.Add(treeItem);
        }
        else
        {
            treeItem.Header = $"{displayName} (not found)";
            treeItem.Foreground = System.Windows.Media.Brushes.Gray;
            treeItem.ToolTip = $"Folder not found: {uncRoot}";
            FolderTree.Items.Add(treeItem);
        }

        if (expandedFolders is { Count: > 0 } || !string.IsNullOrEmpty(selectedFolder))
        {
            var foldersToRestore = expandedFolders ?? new List<string>();
            var folderToSelect = selectedFolder;
            _ = Task.Run(async () =>
            {
                await Task.Delay(800);
                await RestoreNavigationStateAsync(foldersToRestore, folderToSelect);
            });
        }
    }

    private async Task InitializeRootedFolderAsync(string rootFolderPath,
        List<string>? expandedFolders, string? selectedFolder)
    {
        var shortName = Path.GetFileName(rootFolderPath.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(shortName))
            shortName = rootFolderPath; // fallback for drive roots

        var rootNode = new FileSystemNode(shortName, rootFolderPath, isDirectory: true);

        // Create a work queue for this rooted folder (keyed by drive letter if available)
        var driveLetter = rootFolderPath.Length >= 2 && rootFolderPath[1] == ':'
            ? rootFolderPath[..1]
            : "_root";
        var queue = new WorkQueue(this, new WorkQueueOptions { WorkerCount = 1 });
        _driveQueues[driveLetter] = queue;
        await queue.StartAsync();

        var treeItem = new TreeViewItem
        {
            Header = shortName,
            Tag = rootNode
        };

        if (Directory.Exists(rootFolderPath))
        {
            // Add dummy child for expand arrow
            treeItem.Items.Add(new TreeViewItem { Header = "Loading..." });
            FolderTree.Items.Add(treeItem);

            // Load children immediately
            await queue.EnqueueAsync(new FolderLoaderWorkItem(rootFolderPath));
        }
        else
        {
            // Folder doesn't exist — show error indicator
            treeItem.Header = $"{shortName} (not found)";
            treeItem.Foreground = System.Windows.Media.Brushes.Gray;
            treeItem.ToolTip = $"Folder not found: {rootFolderPath}";
            FolderTree.Items.Add(treeItem);
        }

        // Restore navigation state if provided
        if (expandedFolders is { Count: > 0 } || !string.IsNullOrEmpty(selectedFolder))
        {
            var foldersToRestore = expandedFolders ?? new List<string>();
            var folderToSelect = selectedFolder;
            _ = Task.Run(async () =>
            {
                await Task.Delay(800); // let root folder loading complete
                await RestoreNavigationStateAsync(foldersToRestore, folderToSelect);
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

            // Update status bar — show Linux-style path for WSL tabs
            var mainWindow = Window.GetWindow(this) as MainWindow;
            var displayPath = WslDistroName is not null
                ? Kexplorer.Core.FileSystem.WslPathHelper.ToLinuxPath(node.FullPath)
                : node.FullPath;
            mainWindow?.UpdateStatus(displayPath);

            // If the node is stale, reload its children
            if (node.Stale)
            {
                var queue = GetQueueForPath(node.FullPath);
                if (queue is not null)
                {
                    await queue.EnqueueAsync(new FolderLoaderWorkItem(node.FullPath));
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
            // Auto-scroll immediately if children are already rendered in the tree
            // (pre-loaded via DriveLoaderWorkItem recurseDepth or previous expansion).
            // Count visible child TreeViewItems, not FileSystemNode.Children,
            // since the tree may have a dummy "Loading..." placeholder.
            int visibleChildCount = 0;
            foreach (var item in treeItem.Items)
            {
                if (item is TreeViewItem child && child.Tag is FileSystemNode)
                    visibleChildCount++;
            }
            if (visibleChildCount > 0 && !_restoringNavigation)
            {
                AutoScrollOnExpand(treeItem, visibleChildCount);
            }

            var queue = GetQueueForPath(node.FullPath);

            if (queue is not null)
            {
                await queue.EnqueueAsync(new FolderLoaderWorkItem(node.FullPath));
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

            // Auto-scroll so expanded folder and its children are visible.
            // Skip during navigation restoration to avoid distracting scroll jumps.
            if (parentItem.IsExpanded && children.Count > 0 && !_restoringNavigation)
            {
                AutoScrollOnExpand(parentItem, children.Count);
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
    /// After a folder is expanded and children are loaded, scroll the tree so the parent
    /// and its children are comfortably visible. If there are many children, scroll the
    /// parent toward the top ~10% of the viewport. If there are few children, scroll just
    /// enough to make the last child visible.
    /// The scroll is animated (fast but trackable) and the parent node gets a brief
    /// highlight flash (~600ms) so the user keeps spatial context.
    /// </summary>
    private void AutoScrollOnExpand(TreeViewItem parentItem, int childCount)
    {
        // Defer to let the layout pass render the new child items
        Dispatcher.InvokeAsync(() =>
        {
            try
            {
            var scrollViewer = FindVisualChild<ScrollViewer>(FolderTree);
            if (scrollViewer is null)
                return;

            // Force layout so positions are accurate
            parentItem.UpdateLayout();

            // Get the parent item's position relative to the TreeView
            var parentTransform = parentItem.TransformToAncestor(FolderTree);
            var parentPosition = parentTransform.Transform(new Point(0, 0));
            double parentY = parentPosition.Y;

            double viewportHeight = scrollViewer.ViewportHeight;
            if (viewportHeight <= 0) return;

            double currentVerticalOffset = scrollViewer.VerticalOffset;

            // Approximate height per row (use the parent's header height as a proxy)
            double rowHeight = parentItem.ActualHeight > 0 && parentItem.Items.Count > 0
                ? parentItem.ActualHeight / (parentItem.Items.Count + 1)
                : 20.0;

            // Total height the children will occupy
            double childrenHeight = childCount * rowHeight;

            // How far below the viewport bottom the last child would be
            double lastChildBottom = parentY + rowHeight + childrenHeight;
            double overflow = lastChildBottom - viewportHeight;

            if (overflow <= 0)
            {
                // Everything already fits — no scrolling needed
                return;
            }

            double scrollAmount;
            if (childCount > 10)
            {
                // Many children: position the parent at ~10% from the top of the viewport
                double targetY = viewportHeight * 0.10;
                scrollAmount = parentY - targetY;
            }
            else
            {
                // Few children: scroll just enough to make the last child visible,
                // plus a small margin so it isn't flush with the bottom edge
                double margin = rowHeight;
                scrollAmount = overflow + margin;

                // But don't scroll the parent above the top 10% line
                double maxScroll = parentY - (viewportHeight * 0.10);
                if (scrollAmount > maxScroll && maxScroll > 0)
                    scrollAmount = maxScroll;
            }

            if (scrollAmount > 0)
            {
                AnimateScroll(scrollViewer, currentVerticalOffset,
                    currentVerticalOffset + scrollAmount, durationMs: 200);
            }

            // Brief highlight flash on the parent so the user keeps spatial context
            FlashHighlight(parentItem, durationMs: 600);
            }
            catch (InvalidOperationException)
            {
                // Scroll/animation is non-critical; can fail during startup when
                // the visual tree is still being generated or the dispatcher is
                // processing nested frames.
            }
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// Smoothly animate the ScrollViewer's vertical offset from one value to another.
    /// Uses a short DoubleAnimation on an attached helper so the scroll feels fast
    /// but the user can track the movement.
    /// </summary>
    private static void AnimateScroll(ScrollViewer scrollViewer, double from, double to, int durationMs)
    {
        // WPF's ScrollViewer.VerticalOffset is not a dependency property, so we drive
        // the scroll via a DoubleAnimation on a tiny helper property and forward each
        // tick to ScrollToVerticalOffset.
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // Use an attached property trick: animate a dummy property and hook Changed
        var helper = new ScrollAnimationHelper(scrollViewer);
        helper.RunAnimation(animation);
    }

    /// <summary>
    /// Flash the parent TreeViewItem's background briefly so the user can see which
    /// folder was expanded after the scroll moves it.
    /// </summary>
    private static void FlashHighlight(TreeViewItem item, int durationMs)
    {
        try
        {
        // Resolve the theme-aware accent color or fall back to a soft blue
        var highlightColor = Color.FromArgb(80, 100, 160, 255); // semi-transparent blue
        if (Application.Current.Resources["AccentBrush"] is SolidColorBrush accentBrush)
        {
            var c = accentBrush.Color;
            highlightColor = Color.FromArgb(80, c.R, c.G, c.B);
        }

        var flashBrush = new SolidColorBrush(highlightColor);
        var originalBackground = item.Background;
        item.Background = flashBrush;

        var fadeAnimation = new ColorAnimation
        {
            From = highlightColor,
            To = Colors.Transparent,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        fadeAnimation.Completed += (s, e) =>
        {
            item.Background = originalBackground;
        };

        flashBrush.BeginAnimation(SolidColorBrush.ColorProperty, fadeAnimation);
        }
        catch (InvalidOperationException)
        {
            // Animation can fail during startup if content generation is in progress.
        }
    }

    /// <summary>
    /// Helper that bridges a DoubleAnimation to ScrollViewer.ScrollToVerticalOffset
    /// since VerticalOffset is not a dependency property.
    /// </summary>
    private sealed class ScrollAnimationHelper : Animatable
    {
        private readonly ScrollViewer _scrollViewer;

        public static readonly DependencyProperty ScrollValueProperty =
            DependencyProperty.Register(
                nameof(ScrollValue),
                typeof(double),
                typeof(ScrollAnimationHelper),
                new PropertyMetadata(0.0, OnScrollValueChanged));

        public double ScrollValue
        {
            get => (double)GetValue(ScrollValueProperty);
            set => SetValue(ScrollValueProperty, value);
        }

        public ScrollAnimationHelper(ScrollViewer scrollViewer)
        {
            _scrollViewer = scrollViewer;
        }

        public void RunAnimation(DoubleAnimation animation)
        {
            BeginAnimation(ScrollValueProperty, animation);
        }

        protected override Freezable CreateInstanceCore() => new ScrollAnimationHelper(_scrollViewer);

        private static void OnScrollValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ScrollAnimationHelper helper)
            {
                helper._scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
            }
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;
            var nested = FindVisualChild<T>(child);
            if (nested is not null)
                return nested;
        }
        return null;
    }

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

            // Group expanded paths by drive letter (or "_wsl" for WSL) to parallelize across drives
            var pathsByDrive = new Dictionary<string, List<string>>();
            foreach (var p in expandedFolders)
            {
                string key;
                if (Kexplorer.Core.FileSystem.WslPathHelper.IsWslPath(p))
                    key = "_wsl";
                else if (p.Length >= 2 && p[1] == ':')
                    key = p[..1].ToUpperInvariant();
                else
                    continue;

                if (!pathsByDrive.ContainsKey(key))
                    pathsByDrive[key] = new List<string>();
                pathsByDrive[key].Add(p);
            }
            // Sort each group by path length (shortest first = shallowest)
            foreach (var key in pathsByDrive.Keys)
                pathsByDrive[key] = pathsByDrive[key].OrderBy(p => p.Length).ToList();

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

        // WSL UNC paths: \\wsl.localhost\Ubuntu\...
        if (Kexplorer.Core.FileSystem.WslPathHelper.IsWslPath(fullPath))
        {
            return Kexplorer.Core.FileSystem.WslPathHelper.DecomposePath(fullPath);
        }

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

    /// <summary>
    /// Finds the appropriate work queue for a given path.
    /// Handles drive-letter paths, WSL UNC paths, and rooted-folder fallback.
    /// </summary>
    private WorkQueue? GetQueueForPath(string path)
    {
        // WSL tabs use "_wsl" key
        if (Kexplorer.Core.FileSystem.WslPathHelper.IsWslPath(path))
        {
            _driveQueues.TryGetValue("_wsl", out var wslQueue);
            return wslQueue ?? _driveQueues.Values.FirstOrDefault();
        }

        // Drive-letter paths
        if (path.Length >= 2 && path[1] == ':')
        {
            var drive = path[..1];
            if (_driveQueues.TryGetValue(drive, out var driveQueue))
                return driveQueue;
        }

        // Fallback for rooted-folder tabs
        return _driveQueues.Values.FirstOrDefault();
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

        // "Open in New Tab" — opens a new explorer tab rooted at this folder
        if (node.IsDirectory)
        {
            var openInNewTab = new MenuItem { Header = "Open in New Tab" };
            var capturedPath = node.FullPath;
            var capturedWslDistro = WslDistroName;
            openInNewTab.Click += (s, e) =>
            {
                var mainWindow = Window.GetWindow(this) as MainWindow;
                if (capturedWslDistro is not null)
                    mainWindow?.AddWslExplorerTab(capturedWslDistro, capturedPath);
                else
                    mainWindow?.AddRootedExplorerTab(capturedPath);
            };
            TreeContextMenu.Items.Add(openInNewTab);
            TreeContextMenu.Items.Add(new Separator());
        }

        // Collect valid plugins, partitioned into grouped (IMenuGroupPlugin) and ungrouped
        var validPlugins = new List<IFolderPlugin>();
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
            validPlugins.Add(plugin);
        }

        // Group plugins that implement IMenuGroupPlugin by their MenuGroup
        var grouped = validPlugins
            .OfType<IMenuGroupPlugin>()
            .GroupBy(p => p.MenuGroup)
            .ToDictionary(g => g.Key, g => g.Cast<IFolderPlugin>().ToList());
        var groupedSet = new HashSet<IFolderPlugin>(grouped.Values.SelectMany(v => v));

        // Build menu: ungrouped plugins as flat items, grouped plugins as submenus
        var emittedGroups = new HashSet<string>();
        foreach (var plugin in validPlugins)
        {
            if (groupedSet.Contains(plugin))
            {
                var group = ((IMenuGroupPlugin)plugin).MenuGroup;
                if (!emittedGroups.Add(group)) continue; // already emitted this group

                var groupPlugins = grouped[group];
                if (groupPlugins.Count == 1)
                {
                    // Single plugin in group — just add it flat
                    AddPluginMenuItem(TreeContextMenu, groupPlugins[0], node.FullPath);
                }
                else
                {
                    // Multiple plugins — create a parent menu with submenu items
                    var parentItem = new MenuItem { Header = group };
                    // Force submenu to open on the right regardless of system MenuDropAlignment
                    parentItem.Loaded += (s, e) =>
                    {
                        if (parentItem.Template.FindName("PART_Popup", parentItem)
                            is System.Windows.Controls.Primitives.Popup popup)
                        {
                            popup.Placement = System.Windows.Controls.Primitives.PlacementMode.Right;
                        }
                    };
                    // Wire the parent click to the first plugin (the default action)
                    var defaultPlugin = groupPlugins[0];
                    parentItem.Click += async (s, e) =>
                    {
                        try
                        {
                            await defaultPlugin.ExecuteAsync(node.FullPath, _pluginContext!, CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            await _pluginContext!.Shell.ReportErrorAsync($"Plugin '{defaultPlugin.Name}' failed: {ex.Message}", ex);
                        }
                    };
                    // Add each grouped plugin as a submenu item
                    foreach (var gp in groupPlugins)
                    {
                        var subItem = new MenuItem { Header = gp.Name.Replace(group + " — ", "") };
                        var capturedGp = gp;
                        subItem.Click += async (s, e) =>
                        {
                            e.Handled = true; // prevent parent click from also firing
                            try
                            {
                                await capturedGp.ExecuteAsync(node.FullPath, _pluginContext!, CancellationToken.None);
                            }
                            catch (Exception ex)
                            {
                                await _pluginContext!.Shell.ReportErrorAsync($"Plugin '{capturedGp.Name}' failed: {ex.Message}", ex);
                            }
                        };
                        parentItem.Items.Add(subItem);
                    }
                    TreeContextMenu.Items.Add(parentItem);
                }
            }
            else
            {
                AddPluginMenuItem(TreeContextMenu, plugin, node.FullPath);
            }
        }
    }

    private void AddPluginMenuItem(ContextMenu menu, IFolderPlugin plugin, string folderPath)
    {
        var menuItem = new MenuItem { Header = plugin.Name };
        var capturedPlugin = plugin;
        menuItem.Click += async (s, e) =>
        {
            try
            {
                await capturedPlugin.ExecuteAsync(folderPath, _pluginContext!, CancellationToken.None);
            }
            catch (Exception ex)
            {
                await _pluginContext!.Shell.ReportErrorAsync($"Plugin '{capturedPlugin.Name}' failed: {ex.Message}", ex);
            }
        };
        menu.Items.Add(menuItem);
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
