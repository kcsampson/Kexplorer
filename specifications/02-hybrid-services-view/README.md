# 02 - Hybrid Services View

**Status:** Draft
**Date:** 2026-04-03
**Related:** 01-Modern-Refresh.md (sections 4.7 Services Registry, 4.8 Docker Dashboard)

## Problem

KExplorer's Services tab currently shows only Windows Services. Docker containers serve an analogous role (long-running processes that need to be started, stopped, restarted, and monitored). In hybrid deployments, the operator manages both simultaneously and needs a unified view rather than switching between tabs.

## Proposal

Replace the planned standalone Docker tab (01-Modern-Refresh, section 4.8) with a **split-pane Services view** that shows both Windows Services and Docker containers in a single tab.

### Layout

```
+--------------------------------------------------------------------------------------------------------------+
|  Services                                                                                             [tab]  |
+--------------------------------------------------------------------------------------------------------------+
|  Windows Services                                                                          [filter] [+]      |
|  ----------------------------------------------------------------------------------------------------------- |
|  Name        | Status  | Type | Machine | --port | --db-host   | --log-level | --grpc-port | binPath...     |
|  MyApi       | Running | Auto | .       | 8080   | db-prod     | info        | 9090        | C:\svc\my...   |
|  MyWorker    | Running | Auto | .       | 8081   | db-prod     | debug       | 9091        | C:\svc\my...   |
|  OtherSvc    | Stopped | Man  | .       | --     | --          | --          | --          | C:\svc\ot...   |
|  ...         |         |      |         |        |             |             |             |                |
+--------------------------------------------------------------------------------------------------------------+
|  Docker Containers                                                                         [filter] [+]      |
|  ----------------------------------------------------------------------------------------------------------- |
|  Name        | Status  | Image        | Ports          | GPUs | Mounts | Network                             |
|  redis-1     | Running | redis:7      | 6379->6379     | --   | 1      | bridge                              |
|  app-web     | Exited  | myapp:latest | --             | all  | 3      | host                                |
|  ...         |         |              |                |      |        |                                     |
+--------------------------------------------------------------------------------------------------------------+
```

- Default: horizontal split (top/bottom), resizable splitter
- Each pane has its own filter/search bar
- Context menus share common operations (Start, Stop, Restart, Refresh) but each pane also exposes type-specific actions (e.g., "View Logs" and "Shell" for Docker; "Set Startup Type" for Windows Services)

### Dynamic Flag Columns (Windows Services)

Windows Services for Go microservices encode their entire runtime configuration in the binPath via Go flags (e.g., `C:\svc\myapi.exe --port 8080 --db-host db-prod --log-level debug --grpc-port 9090`). Today, this information is buried in the binPath string and you have to mentally parse it or run `sc qc` to see it.

**Solution:** Parse the binPath of each service, extract the flags, and promote them to dynamic columns in the Services grid.

#### How it works

1. **On load/refresh**, query each service's binPath via `sc qc <service>` or the Win32 `QueryServiceConfig` API
2. **Parse the binPath** to extract the executable path and all `--flag value` / `-flag value` pairs
3. **Build a union of all flags** seen across all displayed services -- each unique flag name becomes a column
4. **Populate cells** -- if a service has `--port 8080`, the `--port` column shows `8080`. If a service doesn't use that flag, the cell shows `--` (empty)

#### Column behavior

- **Dynamic**: columns appear/disappear based on what flags exist across the current set of services. No hardcoded flag list.
- **Sortable**: click a flag column header to sort (e.g., sort by `--port` to see port allocation at a glance)
- **Filterable**: the existing filter bar should work across flag values too (e.g., type "debug" to find all services running with `--log-level debug`)
- **Sticky columns**: the static columns (Name, Status, Type, Machine) are always pinned left. Dynamic flag columns scroll horizontally if there are many.
- **Full binPath**: keep a `binPath` column (truncated) at the far right, with tooltip showing the full string. Or accessible via double-click / context menu "View Full binPath".

#### Parsing rules

```
binPath = <executable-path> [flags...]
flag    = --<name> <value> | -<name> <value> | --<name>=<value>

Examples:
  "C:\services\myapi.exe" --port 8080 --db-host db-prod --log-level debug
  C:\services\myworker.exe --port=8081 --grpc-port 9091 --enable-metrics
```

- Handle quoted executable paths (spaces in path)
- Handle `--flag=value` and `--flag value` styles (Go `flag` package supports both)
- Boolean flags with no value (e.g., `--enable-metrics`) show as `true` in the column
- The executable path itself is not a flag -- strip it before parsing

#### Parallel with Docker

This is the same insight as the Docker Inspect detail panel: **show how the thing was configured to run, not just that it's running.** For Docker containers, the configuration comes from `docker inspect`. For Windows Services, it comes from the binPath flags. Both are currently opaque strings that require manual effort to read.

| Aspect              | Windows Services (binPath flags) | Docker Containers (inspect)     |
|---------------------|----------------------------------|---------------------------------|
| Port                | `--port 8080`                    | `-p 8080:8080`                  |
| Database            | `--db-host db-prod`              | `-e DB_HOST=db-prod`            |
| Log level           | `--log-level debug`              | `-e LOG_LEVEL=debug`            |
| GPU                 | N/A                              | `--gpus all`                    |
| Storage             | `--data-dir C:\data`             | `-v /data:/data`                |

### Shared Concepts

| Concept            | Windows Services            | Docker Containers               |
|--------------------|-----------------------------|----------------------------------|
| Start              | `sc start`                  | `docker start`                   |
| Stop               | `sc stop`                   | `docker stop`                    |
| Restart            | Stop + Start                | `docker restart`                 |
| Status             | Running / Stopped / Paused  | Running / Exited / Paused / Created |
| Logs               | Event Viewer (stretch)      | `docker logs`                    |
| Remove / Delete    | `sc delete`                 | `docker rm`                      |
| Configuration      | binPath, startup type       | Compose file, env vars           |

### Plugin Architecture

Extend the existing plugin capability system:

- `[ServiceContext]` -- existing, for Windows Services
- `[DockerContext]` -- new, for Docker containers
- `[DeploymentContext]` -- optional unified context for actions that span both (e.g., "Restart All" for a linked deployment profile)

### Data Model

```csharp
// Existing
public class ServiceInfo { /* Name, Status, SystemName, Type, Machine, CanStop, CanPause */ }

// New
public class DockerContainerInfo
{
    public string ContainerId { get; set; }
    public string Name { get; set; }
    public string Image { get; set; }
    public string Status { get; set; }   // Running, Exited, Paused, Created
    public string Ports { get; set; }
    public DateTime Created { get; set; }
}
```

### Docker Operations (Priority Order)

These are the actual day-to-day Docker operations the user performs, ranked by frequency:

#### Daily / High Frequency

1. **Start / Stop / Restart** -- `docker start <name>`, `docker stop <name>`, `docker restart <name>`. Context menu actions on selected container(s).

2. **Logs (tail)** -- `docker logs --tail 500 <name>`. Primary troubleshooting tool, typically used right after starting/restarting a container. Should be accessible via context menu and ideally auto-offered after a start/restart action. Opens in a scrollable log viewer panel (not a separate window). Default tail: 500 lines. Option to follow (`-f`) for live streaming.

3. **Inspect / View Run Configuration** -- `docker inspect <name>`. **Critical**: need to see how a container was originally started -- GPU flags (`--gpus`), port mappings (`-p`), volume mounts (`-v`), environment variables (`-e`), restart policy, network mode, entrypoint/cmd overrides. This is not just metadata; it's the operational "what is this thing doing and how was it set up" view. Display as a readable summary (not raw JSON), with a "Show Raw JSON" toggle for the full inspect output.

4. **Shell into container** -- `docker exec -it <name> /bin/bash` (fallback to `/bin/sh`). Context menu: "Shell". Opens a terminal tab within KExplorer connected to the container's shell. This is a very frequent operation -- the user shells in to do ad-hoc debugging, check files, run commands inside the container.

5. **Remove container** -- `docker rm <name>` (with confirmation prompt). For stopped containers. Option to force-remove running containers (`docker rm -f`).

#### Moderate Frequency

6. **Stats** -- `docker stats`. Live resource usage (CPU, memory, network I/O) across containers. Could be a toggle-on overlay or a separate sub-panel within the Docker pane. Useful for spotting runaway containers.

7. **Image build** -- `docker build -t <tag> .` or `docker build -f <Dockerfile> .`. Less frequent, but useful when iterating on container definitions. Could be triggered from the file explorer context menu when right-clicking a Dockerfile.

#### Container Grid Columns

Based on the above workflows, the Docker container grid should show:

| Column       | Source                        | Notes                                    |
|--------------|-------------------------------|------------------------------------------|
| Name         | `docker ps --format`          |                                          |
| Status       | `docker ps --format`          | Running / Exited / Paused + uptime       |
| Image        | `docker ps --format`          | image:tag                                |
| Ports        | `docker ps --format`          | host->container mappings                 |
| GPUs         | `docker inspect` → DeviceRequests | Show "Yes"/"No" or device list       |
| Mounts       | `docker inspect` → Mounts    | Abbreviated (count or first mount path)  |
| Created      | `docker ps --format`          | Relative time (e.g., "2 hours ago")      |

#### Inspect Detail Panel

When a container is selected (or via double-click / context menu "Inspect"), show a detail panel with:

```
+----------------------------------------------------------+
| Container: redis-1                                        |
+----------------------------------------------------------+
| Image:      redis:7.2                                     |
| Status:     Running (Up 3 hours)                          |
| Created:    2026-04-03 09:15:00                           |
|                                                           |
| Port Mappings:                                            |
|   6379/tcp -> 0.0.0.0:6379                                |
|                                                           |
| Volumes:                                                  |
|   /data -> /mnt/c/redis-data (bind)                      |
|                                                           |
| Environment:                                              |
|   REDIS_PASSWORD=****  [show]                             |
|                                                           |
| GPUs:       all                                           |
| Restart:    unless-stopped                                |
| Network:    bridge                                        |
| Entrypoint: docker-entrypoint.sh                          |
| Cmd:        redis-server --appendonly yes                  |
|                                                           |
| [Show Raw JSON]  [Copy docker run command]                |
+----------------------------------------------------------+
```

Key feature: **"Copy docker run command"** -- reconstruct the equivalent `docker run` command from the inspect data. This answers "how do I recreate this container?" which is a common need.

### WSL Integration (Docker runs in WSL, not Docker Desktop)

Docker Desktop is **not used**. Docker runs natively inside WSL2. KExplorer is a Windows WPF app, so it cannot call `docker` directly -- it must go through WSL.

#### Approach: Shell via `wsl` command

All Docker commands are executed by prefixing with `wsl`:

```
wsl docker ps --all --format '{{json .}}'
wsl docker inspect <name>
wsl docker start <name>
wsl docker stop <name>
wsl docker rm <name>
wsl docker logs --tail 500 <name>
wsl docker stats --no-stream
wsl docker build -t <tag> -f <Dockerfile> .
```

This is the simplest approach and mirrors what the user already does manually.

#### WSL session considerations

- **No persistent session needed** -- each `wsl` invocation opens a transient session, runs the command, and exits. This is fine for one-shot commands (`docker ps`, `docker start`, etc.). WSL handles this efficiently.
- **Interactive shell (`docker exec -it bash`)** -- this requires a persistent TTY. KExplorer should launch `wsl docker exec -it <name> /bin/bash` in a terminal tab (same mechanism used for the existing terminal/shell feature). This is a long-running process, not a one-shot command.
- **Log following (`docker logs -f`)** -- also a long-running streaming process. Should be launched as a background stream that feeds into the log viewer panel, with a cancel/stop button.
- **Stats streaming (`docker stats`)** -- same pattern as log following. Use `wsl docker stats --format '{{json .}}'` for structured output, parse and update the UI on each line.

#### WSL distro targeting

- Default: `wsl` uses the default distro. This is usually correct.
- If the user has multiple WSL distros, may need a config option: `wsl -d <distro> docker ...`
- Detect available distros via `wsl --list --quiet` on startup.

#### Path translation

When Docker commands reference host paths (e.g., volume mounts, Dockerfile locations), Windows paths must be translated to WSL paths:

```
C:\Users\ksampson\project  →  /mnt/c/Users/ksampson/project
```

Use `wsl wslpath -u '<windows-path>'` or apply the `/mnt/<drive-letter>/...` convention directly.

#### Future optimization: Docker API over TCP

If performance becomes an issue (unlikely for typical workflows), the WSL Docker daemon can be configured to listen on TCP:

```json
// /etc/docker/daemon.json (inside WSL)
{ "hosts": ["unix:///var/run/docker.sock", "tcp://127.0.0.1:2375"] }
```

Then KExplorer can call the Docker Engine REST API directly from Windows via `http://localhost:2375` without the `wsl` process overhead. This is optional and not needed for v1.

#### Graceful degradation

- On startup / tab open, run `wsl docker info` to check if Docker is available
- If WSL is not installed → show "WSL not available" in the Docker pane
- If WSL is installed but Docker is not running → show "Docker daemon not running in WSL" with a "Start Docker" button (`wsl sudo service docker start`)
- The Windows Services pane always works regardless of Docker availability

### Open Questions

1. **Compose awareness** -- Should the Docker pane group containers by Compose project? Likely yes.
2. **Split orientation** -- Top/bottom (default) vs left/right. Should it be user-configurable?
3. **Linked deployment profiles** -- From 01-Modern-Refresh section 4.8. A profile ties together Windows Services + Docker Compose stacks for one-click deploy/teardown. Keep this as a Phase 3 stretch goal?
4. **Multiple WSL distros** -- Should KExplorer support selecting which WSL distro to target, or just use the default?
5. **Remote machines** -- Windows Services already supports remote machines. Should Docker support remote Docker hosts (via `DOCKER_HOST`)?

### Implementation Strategy: Copy-and-Extend

**Do not modify the existing Services panel.** Preserve the current implementation as a working fallback and build the hybrid view as a separate, copied codebase.

#### Steps

1. **Copy** the existing Services files to new "HybridServices" files:
   - `ServicesPanel.xaml` → `HybridServicesPanel.xaml`
   - `ServicesPanel.xaml.cs` → `HybridServicesPanel.xaml.cs`
   - `ServiceLoaderWorkItem.cs` → keep as-is (reused by both)
   - `ServicePlugins.cs` → `HybridServicePlugins.cs` (copy, then extend with Docker actions)

2. **Wire up** the new panel as a separate tab type in `MainWindow.xaml` / shell:
   - File > New Tab > "Services" -- opens the **original** `ServicesPanel` (unchanged)
   - File > New Tab > "Hybrid Services" -- opens the **new** `HybridServicesPanel`
   - Both coexist; user picks which they want

3. **Build the hybrid view** in the copied files:
   - Add the split-pane layout (Windows Services top, Docker bottom)
   - Add dynamic flag column parsing to the Services grid
   - Add the Docker container grid and all Docker operations
   - Add the inspect detail panel for both panes

4. **Once stable**, the original Services tab can optionally be retired -- but only after the hybrid view has proven itself in daily use.

#### Why this approach

- **Zero risk** to existing functionality -- the original Services panel is untouched
- **Easy rollback** -- if the hybrid view has issues, the original is always one tab away
- **Incremental adoption** -- use the hybrid view when you want it, fall back to the original when you don't
- **Clean diff** -- code review can compare the original vs hybrid files side-by-side to see exactly what changed

#### Files affected (new only, no modifications to existing)

```
Kexplorer.UI/
  HybridServicesPanel.xaml          (copied from ServicesPanel.xaml)
  HybridServicesPanel.xaml.cs       (copied from ServicesPanel.xaml.cs)

Kexplorer.Core/
  Plugins/DockerContainerInfo.cs    (new model)
  Work/DockerContainerLoaderWorkItem.cs  (new, follows ServiceLoaderWorkItem pattern)

Kexplorer.Plugins/
  BuiltIn/HybridServicePlugins.cs   (copied from ServicePlugins.cs, extended)
  BuiltIn/DockerPlugins.cs          (new Docker-specific context menu actions)
```

### Implementation Notes

- `ServiceLoaderWorkItem` pattern is reused for a `DockerContainerLoaderWorkItem`
- Refresh cadence: Windows Services are polled on demand (F5). Docker containers could auto-refresh on a timer since `docker ps` is fast.
- The shell/tab system in `MainWindow.xaml` needs a new tab type entry for "Hybrid Services" alongside the existing "Services" entry.
