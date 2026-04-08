# 03 — Bugs and Enhancements

**Status:** Draft  
**Date:** 2026-04-05  
**Author:** (owner) + AI assistant  
**Related:** 01-Modern-Refresh.md, 02-hybrid-services-view

---

## Bug-001: "Hide from View" context menu does nothing in Services listing

### Severity: Medium  
### Component: Hybrid Services Panel / Plugins

### Description

The **"Hide from View"** context menu item on Windows Services (and Docker containers) does not actually remove the selected row(s) from the grid. The menu item appears and can be clicked, but the service remains visible.

### Root Cause

The legacy `HideServiceScript` directly removed `DataRow` entries from the `DataView.Table.Rows` collection on the UI thread. In the Modern codebase, `HideServicePlugin.ExecuteAsync()` only reports a status message (`"Hidden: {svc.DisplayName}"`) via the shell status bar — it **does not** remove the item from the `ObservableCollection<ServiceInfo>`. The plugin's comment says *"This is handled by the ServicesPanel directly"*, but no such handling exists in `HybridServicesPanel`.

### Existing Code (Modern)

**`Kexplorer.Plugins/BuiltIn/ServicePlugins.cs`** — `HideServicePlugin`:
- Reports status only; no removal logic.

**`Kexplorer.UI/HybridServicesPanel.xaml.cs`** — `BuildServiceContextMenu()`:
- Calls `plugin.ExecuteAsync()` on click but does not inspect the result or perform any post-execution removal.

**`Kexplorer.Core/State/SessionState.cs`** — `SessionStateManager`:
- `GetVisibleServiceNames()` already snapshots the current `_services` collection on save. So if services were properly removed from `_services`, the hidden items would be excluded from the next session restore automatically.

### Proposed Fix

Add a `RemoveServicesAsync(IReadOnlyList<ServiceInfo> services)` method to `IHybridServiceShell` (or equivalent shell interface). Have `HideServicePlugin.ExecuteAsync()` call it to remove the selected services from the panel's `ObservableCollection<ServiceInfo>`. The panel dispatches the removal to the UI thread.

Alternatively, the `HybridServicesPanel` could detect that a `HideServicePlugin` was executed and remove the selected items post-execution, but the shell-callback approach is cleaner and consistent with how `HideDrivePlugin` uses `context.Shell.RemoveTreeNodeAsync()`.

### Acceptance Criteria

1. Right-click a service → "Hide from View" → the row is removed from the grid immediately.
2. Multiple selection: hide multiple services at once.
3. Hidden services are excluded from the session state saved to `state.json`.
4. On next startup, hidden services remain hidden (they are not in `VisibleServices`).
5. "Refresh" re-fetches from the OS, restoring previously hidden services (same as legacy behavior).
6. The same fix applies to the Docker container grid's "Hide from View" if present.

---

## Enhancement-001: Re-order rows in Services and Docker Containers grids

### Priority: Medium  
### Component: Hybrid Services Panel

### Description

The Services and Docker Containers grids currently display rows in whatever order they are returned by the OS (or by the plugin manager). In practice, the user manages groups of related services (e.g., a deployment profile of ~10 services) and wants to arrange them in a logical order — e.g., infrastructure services at the top, application services below, ordered by dependency or startup sequence.

### Desired Behavior

1. **Up / Down toolbar buttons** — Each pane toolbar (see Enhancement-003) contains an **▲ Up** button and a **▼ Down** button. Clicking Up moves the selected row up one position in the grid; clicking Down moves it down one position.
2. **Selection required** — Both buttons are **disabled** when no row is selected. They become enabled as soon as a row is selected.
3. **Edge behavior** — The ▲ Up button is disabled when the selected row is already at the top (index 0); the ▼ Down button is disabled when the selected row is already at the bottom (last index).
4. **Selection follows the row** — After a move, the moved row remains selected so the user can press the button repeatedly to move it multiple positions.
5. **Persist the custom order** in the session state (`state.json`).
6. **Restore the custom order on startup** — when services are loaded, sort them into the persisted order. Services not in the persisted order (e.g., newly installed) are appended at the bottom.
7. **Multi-select aware** — If multiple rows are selected, clicking ▲/▼ moves the entire selected block up or down as a unit. The block selection is maintained after the move.
8. Sorting by a column header should be a temporary override; the user's custom order can be restored via a "Reset to Custom Order" option or by clicking the sort header again.

### State Model Changes

Extend `TabState` in `SessionState.cs`:

```csharp
public class TabState
{
    // ... existing fields ...

    /// <summary>
    /// Ordered list of service display-name//machine pairs defining the user's custom row order.
    /// Null or empty means no custom order (use default OS order).
    /// </summary>
    public List<string>? ServiceOrder { get; set; }

    /// <summary>
    /// Ordered list of Docker container names defining the user's custom row order.
    /// </summary>
    public List<string>? DockerContainerOrder { get; set; }
}
```

On save, snapshot the current grid order. On load, use the persisted order as a sort key when populating the `ObservableCollection`.

### Implementation Notes

- **Toolbar buttons**: Add `▲ Up` and `▼ Down` buttons to each pane toolbar (see Enhancement-003). Use `ICommand` (e.g., `RelayCommand`) bound to move-up / move-down methods on the panel or view-model.
- The `ObservableCollection<ServiceInfo>` backing the grid already supports `Insert`, `Move`, and `RemoveAt` — use `Move(oldIndex, newIndex)` for in-place reordering.
- **Enable/disable logic**: Bind `Command.CanExecute` to check that (a) a row is selected, (b) the selected row is not already at the boundary (index 0 for Up, last index for Down). Re-evaluate `CanExecute` after every move and after selection changes.
- **Maintain selection**: After calling `Move()`, programmatically set `DataGrid.SelectedItem` (or `SelectedIndex`) back to the moved item so the user can press the button repeatedly.
- No dedicated reorder column is needed — the arrows live in the toolbar, not in the grid cells.

### Acceptance Criteria

1. Each pane toolbar has ▲ Up and ▼ Down buttons (no per-row reorder column).
2. Both buttons are disabled when no row is selected.
3. Clicking ▲ moves the selected row up one position; ▼ moves it down. ▲ is disabled at the top edge; ▼ at the bottom edge.
4. The moved row stays selected after the move.
5. Multi-select: clicking ▲/▼ moves the entire selected block as a unit.
6. The custom order is saved to `state.json` on exit.
7. The custom order is restored on startup.
8. Newly appearing services (not in the persisted order) are appended at the bottom.
9. Services that no longer exist are silently dropped from the persisted order.
10. Works for both Windows Services and Docker Containers grids independently.
11. (Stretch) Drag-and-drop row reordering.

---

## Enhancement-003: Pane toolbars for Services and Docker Containers

### Priority: Medium  
### Component: Hybrid Services Panel

### Description

Each pane (Windows Services and Docker Containers) needs a small toolbar strip above its grid. The toolbar is a lightweight extension point — new buttons will be added over time. The first two buttons are:

1. **▲ Up** — Moves the selected row up one position in the grid (Enhancement-001). Disabled when no row is selected or the selected row is already at the top.
2. **▼ Down** — Moves the selected row down one position in the grid (Enhancement-001). Disabled when no row is selected or the selected row is already at the bottom.
3. **Refresh** — Re-fetches the listing from the OS / Docker daemon and repopulates the grid. For Windows Services this re-runs the `ServiceController.GetServices()` query (filtered to the tab's visible services). For Docker Containers this re-runs `wsl docker ps --all`. Previously hidden services reappear (same as legacy F5 behavior).

### Layout

```
+--------------------------------------------------------------------------------------------------------------+
|  Windows Services                                                  [▲ Up] [▼ Down] [↻ Refresh] [filter]      |
|  ----------------------------------------------------------------------------------------------------------- |
|  Name        | Status  | Type | Machine | ...                                                               |
|  ...                                                                                                         |
+--------------------------------------------------------------------------------------------------------------+
|  Docker Containers                                                 [▲ Up] [▼ Down] [↻ Refresh] [filter]      |
|  ----------------------------------------------------------------------------------------------------------- |
|  Name        | Status  | Image        | ...                                                                  |
|  ...                                                                                                         |
+--------------------------------------------------------------------------------------------------------------+
```

- The toolbar sits between the pane header ("Windows Services" / "Docker Containers") and the grid.
- Toolbar buttons are small icon-style (`ToolBar` or a styled `StackPanel` with flat `Button` controls).
- The existing filter/search bar moves into the toolbar area (right-aligned).
- The `[+]` button (if present) also belongs in the toolbar.

### Implementation Notes

- **Up / Down buttons**: Use `ICommand` bindings (`MoveUpCommand`, `MoveDownCommand`). `CanExecute` checks for a selected row and boundary conditions (index 0 / last index). After calling `ObservableCollection.Move()`, re-select the moved item. Re-evaluate `CanExecute` on selection change and after each move via `CommandManager.InvalidateRequerySuggested()` or explicit raise.
- **Refresh**: Call the existing `RefreshServicesAsync()` / `RefreshDockerAsync()` methods (or equivalent). Disable the button and show a brief spinner/busy indicator while the refresh is in progress to prevent double-clicks.
- **Extensibility**: Use an `ItemsControl` or `ToolBar` bound to a collection of toolbar item descriptors so new buttons can be added without modifying XAML layout each time.

### Acceptance Criteria

1. Each pane has a toolbar strip above the grid.
2. The ▲ Up and ▼ Down buttons move the selected row up/down. Disabled when no row is selected or at boundary.
3. The "Refresh" button re-fetches and repopulates the grid. Previously hidden items reappear.
4. The Refresh button is disabled while a refresh is in progress.
5. The toolbar is present on both the Windows Services and Docker Containers panes.
6. The existing filter bar is integrated into the toolbar area.

---

## Enhancement-002: Remember navigation state and restore on startup

### Priority: High  
### Component: File Explorer tabs, Session State

### Description

Legacy KExplorer persisted the full navigation state of every tab — which drives were loaded, which folders were expanded, and which folder was selected — and restored it on startup. The Modern refresh needs the same capability, but with an important implementation consideration: **folder tree expansion is asynchronous**. You cannot expand a tree node until its children have been loaded from disk, so the restoration must be an async, depth-first operation that waits for each level to load before expanding the next.

### Legacy Behavior (reference)

`TreeViewPersistState.cs` captured per-tab:
- `drives[]` — which drives were loaded as root nodes.
- `currentFolder` — the full path of the selected folder.
- Implicitly, the expanded nodes were encoded in the tree path: to reach `C:\Users\dev\projects`, `C:\`, `Users`, `dev`, and `projects` must all be expanded.

On restore, the legacy code loaded each drive, then walked the folder path and expanded each node in sequence (synchronously on the UI thread, which could block — acceptable in WinForms era, not in WPF).

### Proposed Design

#### 1. Persist the navigation state

Extend `TabState`:

```csharp
public class TabState
{
    // ... existing fields ...

    /// <summary>
    /// Full paths of all expanded folders in the tree.
    /// Used to restore the visual tree state on startup.
    /// </summary>
    public List<string>? ExpandedFolders { get; set; }

    /// <summary>
    /// Full path of the currently selected (highlighted) folder.
    /// </summary>
    public string? SelectedFolder { get; set; }
}
```

On save, walk the tree and collect all expanded node paths. Also capture the selected node's path.

#### 2. Async restoration sequence

On startup, for each file explorer tab:

```
1. Load root drives (already async via DriveLoaderWorkUnit)
2. For each expanded path, decompose into segments: C:\, Users, dev, projects
3. Starting from the root:
   a. Find the matching root drive node
   b. Trigger child-folder load (async — enqueue FolderWorkUnit / wait for completion)
   c. Expand the node in the UI
   d. Find the next matching child node
   e. Repeat (b–d) until the full path is expanded or a segment is not found
4. After all expanded paths are restored, select the SelectedFolder node and scroll it into view
```

#### 3. Key considerations

- **Parallelism across tabs**: Each tab's restoration can proceed independently and in parallel.
- **Parallelism within a tab**: Multiple expanded paths that share no common prefix can be expanded in parallel. Paths that share a prefix naturally serialize (you must expand `C:\Users` before you can expand `C:\Users\dev`).
- **Missing folders**: If a folder no longer exists, skip it silently and continue restoring the rest of the tree. Log a warning.
- **Performance**: Loading deep trees on spinning disks or network drives can be slow. Show a subtle progress indicator (e.g., pulsing node icon or status bar message: "Restoring navigation…"). Do not block the UI — the user should be able to interact with the partially restored tree while deeper levels are still loading.
- **Cancellation**: If the user manually clicks on a different node or tab while restoration is in progress, cancel the remaining restoration for that tab (the user has taken control).
- **Error handling**: Network drives or removable media may not be available at startup. Fail gracefully — skip unavailable drives, restore what is reachable.

#### 4. Depth-first async helper (pseudocode)

```csharp
async Task RestoreExpandedPathAsync(TreeNode root, string[] pathSegments, CancellationToken ct)
{
    var current = root;
    foreach (var segment in pathSegments)
    {
        ct.ThrowIfCancellationRequested();

        // Ensure children are loaded
        if (!current.IsLoaded)
            await current.LoadChildrenAsync(ct);

        // Find the matching child
        var child = current.Children.FirstOrDefault(c => c.Name.Equals(segment, StringComparison.OrdinalIgnoreCase));
        if (child is null)
        {
            // Folder no longer exists — stop here
            break;
        }

        child.IsExpanded = true;
        current = child;
    }
}
```

### Acceptance Criteria

1. On exit, the full set of expanded folder paths and the selected folder are persisted per tab to `state.json`.
2. On startup, each tab's tree is restored to match the saved state — same drives loaded, same folders expanded, same folder selected.
3. Restoration is fully asynchronous — the UI remains responsive throughout.
4. Missing or unavailable folders are skipped silently (no error dialogs), with a log entry.
5. The user can interact with the tree during restoration; manual navigation cancels further auto-expansion for that tab.
6. Multiple tabs restore in parallel.
7. A status bar indicator shows "Restoring navigation…" while restoration is in progress, and clears when complete.

---

## Enhancement-004: View Logs for Windows Services

### Priority: High  
### Component: Hybrid Services Panel, Plugins

### Description

Docker containers already have "View Logs" (`docker logs --tail 500`). Windows Services need equivalent log-viewing support. The challenge is that each service type stores logs in a different location — there is no single convention. The log path must be **derived from the service's binPath** based on the executable name and its command-line flags.

### Log Location Rules

#### Rule 1: `enable2020-*` services

Executables matching the pattern `enable2020-*.exe` (e.g., `enable2020-api-go-service.exe`, `enable2020-worker.exe`) encode their log location in binPath flags:

- **`-logDir`** flag → base directory for logs  
- **`-componentName`** flag → sub-folder within the base directory  

**Log folder** = `{-logDir}\{-componentName}\`   then the most recent file is the log.

Example binPath:
```
"C:\services\enable2020-api-go-service.exe" -port 8080 -logDir C:\logs\enable2020 -componentName api-go-service -db-host db-prod
```
→ Log folder: `C:\logs\enable2020\api-go-service\`

All `.log` files in that folder are candidates. Show the most recent (by last-write-time), tail 500 lines.

#### Rule 2: `ew-graphql-mcp.exe`

The log file is at a **fixed relative path** from the executable's directory:

**Log file** = `{exe directory}\logs\ew-graphql-mcp.log`

Example binPath:
```
C:\services\graphql\ew-graphql-mcp.exe --port 4000
```
→ Exe directory: `C:\services\graphql\`  
→ Log file: `C:\services\graphql\logs\ew-graphql-mcp.log`

Tail 500 lines.

#### Rule N: Future services

Other executables may have different log conventions. The resolver should be extensible — a registry of `IServiceLogResolver` strategies that are tried in order until one matches.

### Proposed Design

#### 1. Log resolver interface

```csharp
public interface IServiceLogResolver
{
    /// <summary>
    /// Returns true if this resolver knows how to find logs for the given service.
    /// </summary>
    bool CanResolve(ServiceInfo service, string exePath, Dictionary<string, string> binPathFlags);

    /// <summary>
    /// Returns the full path(s) to the log file(s) for the service.
    /// </summary>
    IReadOnlyList<string> ResolveLogPaths(ServiceInfo service, string exePath, Dictionary<string, string> binPathFlags);
}
```

#### 2. Built-in resolvers

| Resolver | Matches | Logic |
|---|---|---|
| `Enable2020LogResolver` | Exe name starts with `enable2020-` | Combine `-logDir` + `-componentName` flags; find most recent `.log` file in that folder |
| `EwGraphqlMcpLogResolver` | Exe name is `ew-graphql-mcp.exe` | `{exe dir}\logs\ew-graphql-mcp.log` |

#### 3. Log viewer behavior

- **Trigger**: Context menu "View Logs" on a selected Windows Service (new `[ServiceContext]` plugin).
- **Resolution**: Parse the service's binPath (executable path + flags — reuse the flag-parsing logic from Enhancement-001 / spec 02). Pass to the resolver chain.
- **Display**: Open a scrollable log viewer panel (same component used for Docker container logs). Show the **last 500 lines** of the resolved log file. Auto-scroll to the bottom.
- **Follow mode** (stretch): Optional tail-follow (`FileSystemWatcher` or polling) to stream new lines as they are written, similar to Docker `logs -f`.
- **Error cases**:
  - No resolver matches → status bar message: "No log location configured for this service type."
  - Resolved path doesn't exist → status bar message: "Log file not found: {path}"
  - File is locked → read with `FileShare.ReadWrite` (standard for tailing active log files).

#### 4. Integration with binPath flag parsing

The binPath flag parser (already specified in 02-hybrid-services-view for dynamic flag columns) produces a `Dictionary<string, string>` of flag→value pairs. The log resolvers consume this same dictionary. No duplicate parsing needed.

### Acceptance Criteria

1. Right-click a Windows Service → "View Logs" context menu item.
2. For `enable2020-*` services, the log viewer opens showing the last 500 lines of the most recent `.log` file in `{-logDir}\{-componentName}\`.
3. For `ew-graphql-mcp.exe`, the log viewer opens showing the last 500 lines of `{exe dir}\logs\ew-graphql-mcp.log`.
4. Log file is read with `FileShare.ReadWrite` so it works on actively written files.
5. If no resolver matches or the log file is not found, a clear status message is shown (no crash, no empty viewer).
6. The log viewer panel is the same component used for Docker container logs (consistent UX).
7. The resolver system is extensible — adding a new service type requires only a new `IServiceLogResolver` implementation.

---

## Enhancement-005: Skinnable application with "Kimbonics" theme

### Priority: Low  
### Component: Application-wide (UI layer)

### Description

Make the application skinnable with switchable color/font schemes. Ship two built-in skins:

1. **Standard** — the current default appearance (preserve as-is).
2. **Kimbonics** — a sci-fi aesthetic inspired by *The Matrix*: dark backgrounds, bright gold text, monospace font.

### Skin Definitions

#### Standard

The existing color scheme and font choices. No changes — just formalize them as named resources so they can be swapped.

#### Kimbonics

| Element | Value | Notes |
|---|---|---|
| **Primary background** | `#0D0D0D` (near-black) | Deep dark field, like a terminal in the dark |
| **Secondary background** (panels, grid rows) | `#1A1A1A` | Subtle lift for panels and alternate rows |
| **Alternate row background** | `#121212` | Grid row striping |
| **Primary text / foreground** | `#FFD700` (Gold) | Bright gold — the signature color |
| **Secondary text** (dimmed labels, disabled) | `#B8960C` (dark gold) | Muted gold for less prominent text |
| **Accent / highlight** (selection, active tab) | `#00FF41` (Matrix green) | Sparingly — selected rows, active indicators |
| **Border / separator** | `#333333` | Subtle dark grey grid lines and splitters |
| **Error / stopped status** | `#FF4444` | Red for stopped services, errors |
| **Running / success status** | `#00FF41` (Matrix green) | Running services, success indicators |
| **Toolbar background** | `#111111` | Slightly lighter than primary background |
| **Font family** | `Consolas, Courier New, monospace` | Consolas preferred (ships with Windows); Courier New fallback |
| **Font size (grid)** | `13px` | Slightly larger for readability with monospace |
| **Font size (headers)** | `15px` | |
| **Scrollbar track** | `#1A1A1A` | Blends with background |
| **Scrollbar thumb** | `#FFD700` at 40% opacity | Gold tint, not overpowering |

### Architecture

#### 1. Resource dictionaries per skin

Each skin is a WPF `ResourceDictionary` XAML file defining named brushes, colors, font families, and font sizes:

```
Kexplorer.UI/
  Themes/
    Standard.xaml      ← current colors formalized as resources
    Kimbonics.xaml     ← dark/gold/monospace theme
```

Both dictionaries define the **same resource keys** (e.g., `PrimaryBackgroundBrush`, `PrimaryForegroundBrush`, `GridFontFamily`, `GridFontSize`, etc.). Controls bind to these keys via `{DynamicResource}`.

#### 2. Theme switching at runtime

```csharp
public static class ThemeManager
{
    public static void ApplyTheme(string themeName)
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Kexplorer.UI;component/Themes/{themeName}.xaml")
        };

        // Replace the theme dictionary in the app's merged dictionaries
        var app = Application.Current;
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Contains("IsThemeDictionary"));
        if (existing != null)
            app.Resources.MergedDictionaries.Remove(existing);

        app.Resources.MergedDictionaries.Add(dict);
    }
}
```

Use `DynamicResource` (not `StaticResource`) throughout all XAML so changes take effect immediately without restarting.

#### 3. Persisting the choice

Add to `SessionState`:

```csharp
public class SessionState
{
    // ... existing fields ...
    public string ThemeName { get; set; } = "Standard";
}
```

On startup, call `ThemeManager.ApplyTheme(state.ThemeName)`. The user switches via a menu (e.g., View → Theme → Standard / Kimbonics) or a toolbar dropdown.

#### 4. Resource key catalog (minimum set)

| Key | Type | Usage |
|---|---|---|
| `PrimaryBackgroundBrush` | `SolidColorBrush` | Window, panel backgrounds |
| `SecondaryBackgroundBrush` | `SolidColorBrush` | Card/panel backgrounds |
| `AlternateRowBrush` | `SolidColorBrush` | Grid alternate row |
| `PrimaryForegroundBrush` | `SolidColorBrush` | Main text |
| `SecondaryForegroundBrush` | `SolidColorBrush` | Dimmed / secondary text |
| `AccentBrush` | `SolidColorBrush` | Selection, highlights |
| `BorderBrush` | `SolidColorBrush` | Separators, grid lines |
| `ErrorBrush` | `SolidColorBrush` | Error/stopped indicators |
| `SuccessBrush` | `SolidColorBrush` | Running/success indicators |
| `ToolbarBackgroundBrush` | `SolidColorBrush` | Toolbar strip |
| `GridFontFamily` | `FontFamily` | DataGrid, tree view |
| `GridFontSize` | `double` | DataGrid cells |
| `HeaderFontSize` | `double` | Section headers |
| `ScrollbarThumbBrush` | `SolidColorBrush` | Custom scrollbar styling |

### Acceptance Criteria

1. Application ships with two skins: "Standard" and "Kimbonics".
2. "Standard" is the default and matches the current appearance exactly.
3. "Kimbonics" applies: near-black background, gold (`#FFD700`) primary text, `Consolas` / `Courier New` monospace font, Matrix green accents.
4. Theme can be switched at runtime via menu (View → Theme) without restarting.
5. The selected theme is persisted in `state.json` and restored on startup.
6. All UI elements (grids, tree views, toolbars, context menus, status bar, log viewer, splitters) respect the active skin's resources.
7. Adding a new skin requires only a new `.xaml` resource dictionary with the same keys — no code changes.

---

## Enhancement-006: File Explorer Context Menu — Open Folder in New Tab

### Priority: Medium  
### Component: File Explorer tabs, Tree View, Tab Management

### Description

Add an **"Open in New Tab"** context menu item to folder nodes in the File Explorer tree view. This opens a **new file explorer tab** whose root is the selected folder — not the drive letter. The tab displays the folder's short name (e.g., `AppData`) as both the root node label and the tab title. Everything below the root behaves identically to a normal file explorer tab (expand, collapse, navigate, file listing, context menus, etc.), but the tree starts at the chosen folder rather than at a drive.

### Common Use Case

Frequently accessed deep paths like `C:\Users\{username}\AppData` are tedious to navigate to every time. With this feature the user can right-click `AppData`, choose "Open in New Tab", and have a permanent tab rooted at that folder — no drive letter, no parent traversal required. The tab persists across sessions (via session state), so it is always ready.

### Desired Behavior

1. **Context menu**: Right-click any folder node in the File Explorer tree → context menu includes **"Open in New Tab"**.
2. **New tab creation**: Clicking the menu item opens a new File Explorer tab.
3. **Root node**: The new tab's tree has a single root node whose label is the folder's **short name** (e.g., `AppData`, `projects`, `.ssh`). No drive letter or parent path is shown in the tree.
4. **Full path in status bar**: When the root node is selected (clicked), the bottom status bar displays the **full absolute path** (e.g., `C:\Users\kimball.sampson\AppData`). This is the same status bar behavior as any other selected folder — it always shows the full path of the selected node.
5. **Tab title**: The tab header shows the folder's short name (e.g., `AppData`).
6. **Normal sub-tree behavior**: Expanding, collapsing, navigating, file listing in the right pane, context menus on child folders — all work identically to a normal drive-rooted tab.
7. **Session persistence**: The rooted folder path is saved in `state.json` so the tab is restored on startup with the same root.

### State Model Changes

Extend `TabState` in `SessionState.cs`:

```csharp
public class TabState
{
    // ... existing fields ...

    /// <summary>
    /// When set, this tab is rooted at a specific folder rather than a drive.
    /// The tree shows only this folder as the root node (using its short name).
    /// Null means the tab uses standard drive-based roots.
    /// </summary>
    public string? RootFolderPath { get; set; }
}
```

On startup, if `RootFolderPath` is non-null:
- Do **not** load drive nodes.
- Create a single root `FileSystemNode` pointing to `RootFolderPath`.
- Set the node's display label to `Path.GetFileName(RootFolderPath)` (the short name).
- The node's underlying `FullPath` remains the absolute path (used for status bar display and all file operations).

### Implementation Notes

- **Context menu integration**: Add "Open in New Tab" to the existing folder-node context menu builder (alongside "Expand", "Refresh", etc.). The menu item should call a method on the main window / tab manager to create a new `FileExplorerTab` with the selected node's full path as the root.
- **Tab creation**: The tab manager's `CreateTab` method (or equivalent) should accept an optional `rootFolderPath` parameter. When provided, it initializes the tree with a single folder root instead of calling `DriveLoaderWorkUnit`.
- **Root node loading**: The root folder node should auto-load its children on tab creation (same as how a drive node loads its top-level folders), so the user sees the folder's contents immediately.
- **Navigation restoration**: Enhancement-002's async restoration logic should work unchanged — `ExpandedFolders` and `SelectedFolder` paths are all under `RootFolderPath`, so the depth-first expansion walks from the custom root instead of a drive node.
- **Edge cases**:
  - If the rooted folder no longer exists at startup, show the tab with an error indicator on the root node (e.g., greyed-out icon + tooltip "Folder not found"). Do not remove the tab — the user may reconnect the drive or restore the folder.
  - The user should not be able to navigate "above" the root (no `..` or parent node). The root is the ceiling of the tree.

### Acceptance Criteria

1. Right-click a folder in the File Explorer tree → "Open in New Tab" appears in the context menu.
2. Clicking it creates a new tab with a single root node labeled with the folder's short name (no drive letter or parent path).
3. The tab header shows the folder's short name.
4. Clicking the root node displays the full absolute path in the bottom status bar.
5. Expanding the root shows its child folders; all tree operations (expand, collapse, refresh, file listing) work normally.
6. The rooted tab is persisted in `state.json` (`RootFolderPath` field) and restored on startup.
7. If the rooted folder does not exist at startup, the tab still appears with an error indicator — it is not silently removed.
8. There is no way to navigate above the root folder in the tree (no parent node or `..` entry).

---

## Enhancement-007: "Open Terminal Here" opens a new tab in Windows Terminal (not a new window)

### Priority: Medium  
### Component: File Explorer Context Menu, Plugins

### Description

The File Explorer context menu includes an **"Open Terminal Here"** action (referenced in 01-Modern-Refresh §4.3.3 as `OpenTerminalHere`). The current behavior launches a **new Windows Terminal window** for each invocation, which quickly clutters the taskbar with redundant windows.

Windows Terminal (`wt.exe`) natively supports opening a **new tab in the most-recently-used (MRU) existing window** via the `-w 0` flag. The enhancement changes the launch behavior to prefer a new tab in an existing Windows Terminal window, and allows the user to choose the shell profile (PowerShell, CMD, or Ubuntu/WSL) for the new tab.

### Background — Windows Terminal CLI

Windows Terminal's `wt.exe` accepts command-line arguments to control window targeting and profile selection:

| Command | Behavior |
|---|---|
| `wt.exe -w 0 new-tab -d "C:\path"` | Opens a new tab in the MRU window, using the default profile, starting in the given directory |
| `wt.exe -w 0 new-tab -p "PowerShell" -d "C:\path"` | New tab in MRU window using the "PowerShell" profile |
| `wt.exe -w 0 new-tab -p "Command Prompt" -d "C:\path"` | New tab in MRU window using the "Command Prompt" (CMD) profile |
| `wt.exe -w 0 new-tab -p "Ubuntu" -d "C:\path"` | New tab in MRU window using the "Ubuntu" (WSL) profile |

The `-w 0` flag means "target the most recently used window". If no Windows Terminal window exists, it creates one.

### Desired Behavior

1. **Context menu**: Right-click any folder node in the File Explorer tree → the context menu includes:
   - **"Open Terminal Here"** — opens a new tab in the existing Windows Terminal window using the user's **default** profile, with the working directory set to the selected folder.
   - **"Open Terminal Here ▸"** (submenu) — offers specific profile choices:
     - **PowerShell**
     - **Command Prompt**
     - **Ubuntu (WSL)**

2. **New tab, not new window**: All options use `wt.exe -w 0 new-tab ...` so the tab opens in the most recently used Windows Terminal window. No new window is created (unless no Windows Terminal window exists yet, in which case one is created automatically by `wt.exe`).

3. **Working directory**: The `-d` flag is set to the full absolute path of the right-clicked folder node.

4. **WSL path translation**: When opening an Ubuntu/WSL tab, the Windows path must be translated to a WSL-compatible path. For example, `C:\Users\dev\projects` becomes `/mnt/c/Users/dev/projects`. The `-d` flag for WSL tabs should use the translated path.

5. **Fallback**: If `wt.exe` is not found on `PATH` or is not installed, fall back to launching `powershell.exe -NoExit -Command "Set-Location 'C:\path'"` (opens a standalone PowerShell window). Show a one-time status bar message: "Windows Terminal not found — falling back to PowerShell."

### Implementation Notes

- **Plugin**: Implement as a `[FolderContext]` plugin (e.g., `OpenTerminalHerePlugin`) following the existing plugin pattern.
- **Profile names**: The profile names ("PowerShell", "Command Prompt", "Ubuntu") are Windows Terminal defaults. Users who have renamed their profiles will need to adjust. Consider making the profile names configurable in a future iteration.
- **WSL path translation**: Convert `X:\path\to\folder` → `/mnt/x/path/to/folder` (lowercase drive letter, forward slashes). Use `Regex.Replace` or simple string manipulation.
- **Process launch**: Use `Process.Start` with `wt.exe` and the appropriate arguments. Do not wait for the process to exit (fire-and-forget).
- **Detection**: Check for `wt.exe` via `Process.Start("where", "wt")` or by checking `%LOCALAPPDATA%\Microsoft\WindowsApps\wt.exe` existence.

### Acceptance Criteria

1. Right-click a folder → "Open Terminal Here" opens a **new tab** in the existing Windows Terminal window (not a new window).
2. The new tab's working directory is the selected folder's full path.
3. The submenu offers PowerShell, Command Prompt, and Ubuntu (WSL) profile choices.
4. WSL tabs translate the Windows path to `/mnt/...` format.
5. If no Windows Terminal window exists, one is created automatically (first tab).
6. If Windows Terminal is not installed, falls back to a standalone PowerShell window with a status bar notification.
7. No extra windows clutter the taskbar — repeated "Open Terminal Here" calls reuse the same Windows Terminal window via `-w 0`.

---

## Enhancement-008: WSL File Explorer Tab

### Priority: Medium  
### Component: File Explorer tabs, Tab Management, FileSystem

### Description

Add a new type of File Explorer tab that browses the **WSL (Windows Subsystem for Linux)** filesystem, rooted at `\\wsl.localhost\Ubuntu`. This gives users native-feeling access to their WSL Ubuntu filesystem from within Kexplorer, using the same tree view, file listing, and context menu experience as a standard Windows File Explorer tab.

Windows exposes WSL filesystems via the UNC path `\\wsl.localhost\<distro>`. For Ubuntu, the root is `\\wsl.localhost\Ubuntu`, which maps to `/` inside the WSL instance. Standard .NET `System.IO` APIs (`Directory.GetDirectories`, `Directory.GetFiles`, `FileInfo`, `DirectoryInfo`) work against this UNC path, so no special P/Invoke or WSL interop is required — the existing `DirectoryLoader` / `FolderWorkUnit` infrastructure can be reused with the UNC root.

### Desired Behavior

1. **New Tab Type**: The "New Tab" menu (or toolbar) offers a **"WSL File Explorer"** option alongside the existing local File Explorer and Hybrid Services tabs.
2. **Root Node**: The new tab's tree has a single root node labeled **`Ubuntu`** (or the distro name). The node's underlying path is `\\wsl.localhost\Ubuntu`.
3. **Tab Header**: The tab header shows **`Ubuntu (WSL)`** to distinguish it from local file explorer tabs.
4. **Tree Browsing**: Expanding the root node shows the top-level Linux directories (`/bin`, `/etc`, `/home`, `/usr`, `/var`, etc.). Further expansion and navigation works identically to a local file explorer tab — the same `FolderWorkUnit` / `DirectoryLoader` pipeline handles child-folder enumeration.
5. **File Listing**: Selecting a folder in the tree populates the right-side file listing panel with files and subfolders, just like local tabs.
6. **Context Menus**: Standard folder context menus apply (Expand, Refresh, Open in New Tab, etc.). "Open Terminal Here" should open a WSL (Ubuntu) terminal tab in Windows Terminal with the working directory translated to the corresponding Linux path (strip `\\wsl.localhost\Ubuntu` prefix and convert backslashes to forward slashes).
7. **Performance**: WSL filesystem access over UNC can be slower than local disk. The async loading pipeline (`FolderWorkUnit` with cancellation) handles this naturally, but consider showing a loading indicator on nodes that take longer than usual.

### Path Handling

| Context | Path Format | Example |
|---|---|---|
| Internal (tree nodes, DirectoryLoader) | Windows UNC | `\\wsl.localhost\Ubuntu\home\user\projects` |
| Status bar display | Linux-style | `/home/user/projects` |
| "Open Terminal Here" (WSL profile) | Linux-style | `/home/user/projects` |
| Session state persistence | Windows UNC | `\\wsl.localhost\Ubuntu\home\user\projects` |

**UNC → Linux path translation**:  
Strip `\\wsl.localhost\Ubuntu` prefix → replace `\` with `/` → result is the Linux-native path.

**Linux → UNC translation** (if needed):  
Prepend `\\wsl.localhost\Ubuntu` → replace `/` with `\`.

### State Model Changes

Extend `TabState` in `SessionState.cs`:

```csharp
public class TabState
{
    // ... existing fields ...

    /// <summary>
    /// The type of file explorer tab.
    /// "Local" (default) = standard Windows drives.
    /// "WSL" = WSL filesystem rooted at \\wsl.localhost\{DistroName}.
    /// </summary>
    public string? ExplorerType { get; set; }

    /// <summary>
    /// For WSL tabs, the distro name (e.g., "Ubuntu").
    /// Used to construct the UNC root: \\wsl.localhost\{WslDistroName}.
    /// </summary>
    public string? WslDistroName { get; set; }
}
```

### Implementation Notes

- **Reuse existing infrastructure**: The `DirectoryLoader`, `FolderWorkUnit`, `FileListWorkUnit`, and `FileSystemNode` classes should work against UNC paths without modification. The .NET `DirectoryInfo` and `FileInfo` APIs transparently handle `\\wsl.localhost\...` paths.
- **Tab creation**: The tab manager's `CreateTab` method should accept an `ExplorerType` parameter. When `"WSL"` is specified, initialize the tree with a single root node at `\\wsl.localhost\{distroName}` with a display label of the distro name.
- **Status bar path display**: When a WSL tab node is selected, the status bar should show the Linux-style path (translated from UNC) for readability. This is a display-only transformation — all internal operations use the UNC path.
- **WSL availability check**: On startup or when the user clicks "New WSL Tab", verify that `\\wsl.localhost\Ubuntu` is accessible (`Directory.Exists`). If WSL is not installed or the distro is not running, show a status bar message: "WSL Ubuntu not available — is WSL installed and the distro running?" Do not create the tab.
- **Enhancement-006 integration**: "Open in New Tab" from a WSL folder node should create another WSL-rooted tab (inheriting the `ExplorerType` = `"WSL"` and `WslDistroName`), not a local tab.
- **Enhancement-007 integration**: "Open Terminal Here" from a WSL folder node should use the Ubuntu profile (`wt.exe -w 0 new-tab -p "Ubuntu" -d "/home/user/..."`) with the Linux-translated path.
- **Future extensibility**: The `WslDistroName` field allows supporting additional distros (e.g., Debian, openSUSE) by simply specifying a different name. The UNC root pattern `\\wsl.localhost\{distro}` is consistent across all WSL2 distros.

### Acceptance Criteria

1. A "WSL File Explorer" option is available when creating a new tab.
2. The WSL tab's tree has a single root node labeled with the distro name (e.g., `Ubuntu`).
3. The tab header shows `Ubuntu (WSL)`.
4. Expanding the root shows Linux top-level directories (`/bin`, `/etc`, `/home`, etc.).
5. Tree navigation, file listing, expand/collapse, and refresh all work identically to local file explorer tabs.
6. The status bar shows Linux-style paths (`/home/user/...`) when a WSL node is selected, not the UNC path.
7. "Open Terminal Here" on a WSL folder opens a WSL/Ubuntu tab in Windows Terminal with the correct Linux path.
8. "Open in New Tab" on a WSL folder creates another WSL-type tab (not a local tab).
9. The WSL tab is persisted in `state.json` (with `ExplorerType` and `WslDistroName`) and restored on startup.
10. If WSL or the specified distro is not available, the user is informed via a status bar message and the tab is not created (or shows an error indicator on restore).
11. The async loading pipeline handles slower WSL filesystem responses gracefully (loading indicators, cancellation support).
