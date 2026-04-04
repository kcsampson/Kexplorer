# Hybrid Services View - Development Tasks

**Spec:** [README.md](README.md)
**Constraint:** Do NOT modify any existing files. Copy-and-extend only.

---

## Phase 1: Scaffolding (Get the split panel on screen) ✅ COMPLETE

### 1.1 Copy ServicesPanel to HybridServicesPanel ✅
- Copy `ServicesPanel.xaml` → `HybridServicesPanel.xaml`
- Copy `ServicesPanel.xaml.cs` → `HybridServicesPanel.xaml.cs`
- Rename class references from `ServicesPanel` to `HybridServicesPanel`
- Verify it compiles and behaves identically to the original

### 1.2 Wire up HybridServicesPanel as a new tab type ✅
- Add "Hybrid Services" option to `MainWindow.xaml.cs` tab creation (alongside existing "Services")
- Both tab types should coexist in the menu: File > New Tab > Services / Hybrid Services
- Verify the new tab opens and loads services the same as the original
- **Added:** `HybridServices` to `TabType` enum in `SessionState.cs`
- **Added:** `AddHybridServicesTab()` in `MainWindow.xaml.cs` with state save/restore/shutdown

### 1.3 Split the panel into two panes ✅
- Replace the single `<Grid>` in `HybridServicesPanel.xaml` with a vertical `<Grid>` containing two rows and a `<GridSplitter>`
- Top pane: existing Windows Services DataGrid (moved into top row)
- Bottom pane: placeholder Docker Containers DataGrid (empty for now, static columns: Name, Status, Image, Ports)
- Bottom pane header: "Docker Containers" label with [filter] placeholder
- Verify the splitter is draggable and both panes render

---

## Phase 2: Docker Container Loading (Get real data in the bottom pane) ✅ COMPLETE

### 2.1 Create DockerContainerInfo model ✅
- New file: `Kexplorer.Core/Plugins/DockerContainerInfo.cs`
- Properties: ContainerId, Name, Image, Status, Ports, GPUs, Mounts (count), Network, Created
- Similar pattern to `ServiceInfo`

### 2.2 Create WslDockerService helper ✅
- New file: `Kexplorer.Core/Docker/WslDockerService.cs`
- Encapsulates all `wsl docker ...` command execution
- Methods:
  - `ListContainersAsync()` → runs `wsl docker ps --all --format '{{json .}}'`, parses JSON, returns `List<DockerContainerInfo>`
  - `InspectContainerAsync(name)` → runs `wsl docker inspect <name>`, returns parsed inspect data
  - `StartAsync(name)`, `StopAsync(name)`, `RestartAsync(name)`, `RemoveAsync(name)`
  - `GetLogsAsync(name, tailLines)` → runs `wsl docker logs --tail <n> <name>`
  - `CheckAvailabilityAsync()` → runs `wsl docker info`, returns bool
- Handle graceful degradation: if `wsl` or `docker` not available, return clear error state
- **Added:** Input sanitization via `EscapeArg()` to prevent command injection

### 2.3 Create DockerContainerLoaderWorkItem ✅
- New file: `Kexplorer.Core/Work/DockerContainerLoaderWorkItem.cs`
- Follows `ServiceLoaderWorkItem` pattern
- Uses `WslDockerService.ListContainersAsync()` to load containers
- Reports results via a new `IHybridServiceShell.SetDockerContainerListAsync()` callback

### 2.4 Define IHybridServiceShell interface ✅
- New file: `Kexplorer.Core/Work/IHybridServiceShell.cs`
- Extends `IServiceShell` with:
  - `SetDockerContainerListAsync(List<DockerContainerInfo> containers)`
  - `SetDockerStatusAsync(string message)` (for "Docker not available" etc.)

### 2.5 Bind Docker data to the bottom pane ✅
- `HybridServicesPanel.xaml.cs`: implement `IHybridServiceShell`
- On tab open, fire both `ServiceLoaderWorkItem` (top) and `DockerContainerLoaderWorkItem` (bottom) in parallel
- Bind `ObservableCollection<DockerContainerInfo>` to the bottom DataGrid
- Show Docker availability status (connected / not available / daemon not running)
- **WorkQueue:** WorkerCount=2 so both loaders run in parallel

---

## Phase 3: Docker Context Menu Actions (Start/Stop/Restart/Remove) ✅ COMPLETE

### 3.1 Create DockerPlugins ✅
- New file: `Kexplorer.Plugins/BuiltIn/DockerPlugins.cs`
- `[DockerContext]` attribute (new, mirrors `[ServiceContext]`) — added to `IKexplorerPlugin.cs`
- `IDockerPlugin` interface — added to `IKexplorerPlugin.cs` (mirrors `IServicePlugin`)
- `PluginManager` extended with `DockerPlugins` list, registration, init, and assembly scanning
- Plugins:
  - `StartContainerPlugin` → `WslDockerService.StartAsync()`
  - `StopContainerPlugin` → `WslDockerService.StopAsync()`
  - `RestartContainerPlugin` → `WslDockerService.RestartAsync()`
  - `RemoveContainerPlugin` → `WslDockerService.RemoveAsync()` (with confirmation dialog)
  - `RefreshDockerPlugin` → reload container list

### 3.2 Wire context menu to Docker pane ✅
- Build Docker context menu dynamically from `[DockerContext]` plugins (same pattern as `BuildContextMenu()` in existing services code)
- Right-click on a container row → shows Start/Stop/Restart/Remove/Refresh
- **Added:** `BuildDockerContextMenu()` in `HybridServicesPanel.xaml.cs`

---

## Phase 4: Docker Logs & Shell

### 4.1 View Logs plugin
- New plugin: `ViewDockerLogsPlugin` in `DockerPlugins.cs`
- Context menu: "View Logs"
- Runs `wsl docker logs --tail 500 <name>`
- Opens output in a scrollable text panel (could reuse terminal/output infrastructure or a simple TextBox for v1)
- Stretch: "Follow Logs" option that streams `wsl docker logs -f <name>`

### 4.2 Shell into container plugin
- New plugin: `DockerShellPlugin` in `DockerPlugins.cs`
- Context menu: "Shell"
- Launches `wsl docker exec -it <name> /bin/bash` in a new terminal tab
- Fallback to `/bin/sh` if bash not available

---

## Phase 5: Docker Inspect Detail Panel

### 5.1 Inspect data model
- New file: `Kexplorer.Core/Docker/DockerInspectInfo.cs`
- Parsed fields: Image, Status, Created, PortMappings, Volumes, Environment, GPUs, RestartPolicy, NetworkMode, Entrypoint, Cmd
- Parse from `docker inspect` JSON output

### 5.2 Inspect detail panel UI
- Add a collapsible detail panel below the Docker DataGrid (or as a flyout on double-click)
- Displays the readable summary from the spec wireframe
- "Show Raw JSON" toggle
- "Copy docker run command" button → reconstructs the `docker run ...` equivalent from inspect data

---

## Phase 6: Dynamic Flag Columns (Windows Services binPath parsing)

### 6.1 BinPath parser
- New file: `Kexplorer.Core/Services/BinPathParser.cs`
- Input: raw binPath string (e.g., `"C:\svc\myapi.exe" --port 8080 --db-host db-prod`)
- Output: `BinPathInfo { ExecutablePath, Flags: Dictionary<string, string> }`
- Handle: quoted exe paths, `--flag value`, `--flag=value`, `-flag value`, boolean flags (no value → `"true"`)
- Unit tests for parsing edge cases

### 6.2 Extend ServiceInfo with parsed flags
- New file: `Kexplorer.Core/Plugins/ExtendedServiceInfo.cs` (wraps `ServiceInfo`, adds `BinPath` string and `ParsedFlags` dictionary)
- `ServiceLoaderWorkItem` is reused as-is; the hybrid panel queries binPath separately via `sc qc` for each service after load

### 6.3 Query binPath for each service
- In `HybridServicesPanel.xaml.cs`, after services load, run `sc qc <serviceName>` for each service to get the binPath
- Parse with `BinPathParser`
- Populate `ExtendedServiceInfo.ParsedFlags`

### 6.4 Generate dynamic columns in the Services DataGrid
- After all services are loaded and flags parsed, compute the union of all flag names
- Add a `DataGridTextColumn` for each flag (pinned static columns on the left: Name, Status, Type, Machine)
- Cells show flag values or `--` if not present
- Add a truncated `binPath` column at the far right with tooltip for full string
- Columns must be sortable and included in filter/search

---

## Phase 7: Polish & Integration

### 7.1 Filter bars for each pane
- Add a filter TextBox above each DataGrid (Services and Docker)
- Filters across all visible columns including dynamic flag columns
- Real-time filtering as user types

### 7.2 Docker stats overlay (stretch)
- Toggle button in Docker pane header: "Stats"
- When active, runs `wsl docker stats --no-stream --format '{{json .}}'` periodically
- Adds CPU% and Mem% columns to the Docker grid (or overlays them)

### 7.3 Graceful degradation polish
- Clean "Docker not available" / "Docker daemon not running" states in the bottom pane
- "Start Docker" button: `wsl sudo service docker start`
- Windows Services pane always works regardless

---

## Task Dependencies

```
Phase 1 (Scaffolding)
  └─→ Phase 2 (Docker Loading)
        ├─→ Phase 3 (Docker Context Menus)
        │     └─→ Phase 4 (Logs & Shell)
        └─→ Phase 5 (Inspect Panel)
  └─→ Phase 6 (Dynamic Flag Columns) ← independent of Docker phases
Phase 7 (Polish) ← after all above
```

Phase 6 (flag columns) is independent of the Docker work and can be developed in parallel.

---

## Unplanned / Additional Work Done

### Global UI: Font size and weight bump ✅
- `App.xaml`: Added implicit styles for Window (14pt), DataGrid/MenuItem/TabItem (13.5pt), StatusBar (13pt)
- All set to `FontWeight="Medium"` for slightly bolder text across the app

### Info Detail Panel on Hybrid Services tab ✅
- Added a third row (with GridSplitter) at the bottom of `HybridServicesPanel.xaml`
- Scrollable read-only TextBox in Consolas font
- **Service selected:** Runs `sc qc <serviceName>` in background, parses `BINARY_PATH_NAME`, shows full binPath
- **Container selected:** Calls `docker inspect`, parses JSON, reconstructs equivalent `docker run` command
  - Includes: `--name`, `--restart`, `-p` ports, `-v` volumes, `--network`, `--gpus`, `-e` env vars, image, cmd
- Partially addresses Phase 5.2 (inspect panel) and Phase 6.3 (binPath query) ahead of schedule
