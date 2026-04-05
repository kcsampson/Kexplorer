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

1. **Dedicated reorder column** — Add a narrow column (pinned left, before the Name column) containing two small **▲ Up** / **▼ Down** arrow buttons per row. Clicking a button moves that row up or down by one position in the grid. **The column is hidden by default** and toggled visible via the "Reorder" toolbar button (see Enhancement-003).
2. **Persist the custom order** in the session state (`state.json`).
3. **Restore the custom order on startup** — when services are loaded, sort them into the persisted order. Services not in the persisted order (e.g., newly installed) are appended at the bottom.
4. **Edge behavior** — The ▲ button is disabled (greyed out) on the first row; the ▼ button is disabled on the last row.
5. **Multi-select aware** — If multiple rows are selected, clicking ▲/▼ on any selected row moves the entire selected block up or down as a unit.
6. Sorting by a column header should be a temporary override; the user's custom order can be restored via a "Reset to Custom Order" option or by clicking the sort header again.

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

- **Reorder column**: Use a `DataGridTemplateColumn` with a `CellTemplate` containing a small vertical `StackPanel` of two `Button` controls (▲ and ▼). Style them as flat/icon buttons (~16×16px each) so the column stays narrow (~36px). Pin this column as the first (frozen) column.
- The `ObservableCollection<ServiceInfo>` backing the grid already supports `Insert`, `Move`, and `RemoveAt` — use `Move(oldIndex, newIndex)` for in-place reordering.
- Button `Command` bindings should pass the row's data item and direction as `CommandParameter`. The view-model (or code-behind) calls `Move()` on the collection.
- Disable the ▲ button when the item is at index 0; disable ▼ when the item is at the last index. Use a value converter or trigger bound to the item's index.

### Acceptance Criteria

1. Each grid has a narrow reorder column (pinned left) with ▲/▼ buttons per row.
2. Clicking ▲ moves the row up one position; ▼ moves it down. Buttons are disabled at the top/bottom edges.
3. Multi-select: clicking ▲/▼ on a selected row moves the entire selected block.
4. The custom order is saved to `state.json` on exit.
5. The custom order is restored on startup.
6. Newly appearing services (not in the persisted order) are appended at the bottom.
7. Services that no longer exist are silently dropped from the persisted order.
8. Works for both Windows Services and Docker Containers grids independently.
9. (Stretch) Drag-and-drop row reordering.

---

## Enhancement-003: Pane toolbars for Services and Docker Containers

### Priority: Medium  
### Component: Hybrid Services Panel

### Description

Each pane (Windows Services and Docker Containers) needs a small toolbar strip above its grid. The toolbar is a lightweight extension point — new buttons will be added over time. The first two buttons are:

1. **Reorder** (toggle) — Toggles visibility of the ▲/▼ reorder column (Enhancement-001). Default state: **off** (reorder column hidden). When toggled on, the reorder column appears; when toggled off, it collapses. The toggle state is purely visual and does not affect the persisted row order.
2. **Refresh** — Re-fetches the listing from the OS / Docker daemon and repopulates the grid. For Windows Services this re-runs the `ServiceController.GetServices()` query (filtered to the tab's visible services). For Docker Containers this re-runs `wsl docker ps --all`. Previously hidden services reappear (same as legacy F5 behavior).

### Layout

```
+--------------------------------------------------------------------------------------------------------------+
|  Windows Services                                                        [⇅ Reorder] [↻ Refresh] [filter]    |
|  ----------------------------------------------------------------------------------------------------------- |
|  (▲▼) | Name        | Status  | Type | Machine | ...                                                        |
|  ...                                                                                                         |
+--------------------------------------------------------------------------------------------------------------+
|  Docker Containers                                                       [⇅ Reorder] [↻ Refresh] [filter]    |
|  ----------------------------------------------------------------------------------------------------------- |
|  (▲▼) | Name        | Status  | Image        | ...                                                           |
|  ...                                                                                                         |
+--------------------------------------------------------------------------------------------------------------+
```

- The toolbar sits between the pane header ("Windows Services" / "Docker Containers") and the grid.
- Toolbar buttons are small icon-style (`ToolBar` or a styled `StackPanel` with flat `Button` controls).
- The existing filter/search bar moves into the toolbar area (right-aligned).
- The `[+]` button (if present) also belongs in the toolbar.

### Implementation Notes

- **Reorder toggle**: Bind the reorder `DataGridTemplateColumn.Visibility` to a `bool ReorderModeActive` property on the panel/view-model. The toolbar button toggles this property. Use `Visibility.Collapsed` (not `Hidden`) so the column space is reclaimed when off.
- **Refresh**: Call the existing `RefreshServicesAsync()` / `RefreshDockerAsync()` methods (or equivalent). Disable the button and show a brief spinner/busy indicator while the refresh is in progress to prevent double-clicks.
- **Extensibility**: Use an `ItemsControl` or `ToolBar` bound to a collection of toolbar item descriptors so new buttons can be added without modifying XAML layout each time.

### Acceptance Criteria

1. Each pane has a toolbar strip above the grid.
2. The "Reorder" button toggles the ▲/▼ column visibility. Default: hidden.
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
