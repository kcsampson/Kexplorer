# Migration Notes

## Phase 1 Status — COMPLETE ✅

### Completed

#### Core Domain Models (`Kexplorer.Core`)
- **`FileSystemNode`** — Platform-independent tree node model replacing legacy `KExplorerNode` (which inherited WinForms `TreeNode`). Supports stale-cascading, lazy load tracking, drive letter extraction.
- **`FileEntry`** — File grid row model replacing the legacy `DataRow`-based approach. Created via `FileEntry.FromFileInfo()`.
- **`DirectoryLoader`** — Static helper for loading subdirectories and files. Replaces scattered logic from `DriveLoaderWorkUnit`, `FolderWorkUnit`, `FileListWorkUnit`.
- **`LauncherService`** — JSON-based launcher configuration replacing legacy XML/XPath `Launcher` class. Supports extension→program mappings with pre-options and post-options.
- **`SessionState` / `SessionStateManager`** — JSON-based state persistence replacing legacy `TreeViewPersistState` + `KexplorerStateSave.xml`. Saves/restores tabs, window dimensions, splitter position.

#### Expanded Shell Contract
- **`IKexplorerShell`** expanded with `SetTreeChildrenAsync`, `SetFileListAsync`, `NavigateToPathAsync` — enabling work items to push tree/grid content without holding direct UI references.
- **`IServiceShell`** — extension interface for service-specific `SetServiceListAsync` callback.

#### Modern Work Items (`Kexplorer.Core.Work`)
- **`DriveLoaderWorkItem`** — Loads drive root subdirectories (2 levels deep). Replaces legacy `DriveLoaderWorkUnit`.
- **`FolderLoaderWorkItem`** — Loads folder subdirectories on expand/stale refresh. Replaces legacy `FolderWorkUnit`.
- **`FileListWorkItem`** — Populates the file grid for a directory. Replaces legacy `FileListWorkUnit`.
- **`ServiceLoaderWorkItem`** — Loads Windows services with machine/pattern filtering. Replaces legacy `ServiceMgrWorkUnit.DoJob()`.

#### Plugin System (`Kexplorer.Core.Plugins`)
- **`IKexplorerPlugin`** — Unified plugin interface replacing the legacy `IScript`/`IFileScript`/`IFolderScript`/`IServiceScript`/`IMixedScript` hierarchy.
- **Capability attributes**: `[FileContext]`, `[FolderContext]`, `[ServiceContext]`, `[GlobalContext]` — plugins declare what contexts they support.
- **`IFilePlugin`**, **`IFolderPlugin`**, **`IServicePlugin`** — Context-specific execution interfaces.
- **`IPluginContext`** — Modernized replacement for `ScriptHelper`. Provides shell, work queue, launcher, clipboard, prompt, confirm, run-program, and refresh APIs.
- **`ServiceInfo`** — Lightweight service model decoupling plugins from `System.ServiceProcess.ServiceController`.
- **`PluginManager`** — Plugin discovery/registration via assembly scanning. Replaces legacy reflection-based `ScriptMgr`.

#### Built-in Plugins (`Kexplorer.Plugins`)
10 file/folder plugins ported from legacy scripts:

| Plugin | Legacy Script | Features |
|---|---|---|
| `RefreshPlugin` | `RefreshScript` | F5 refresh, works on files and folders |
| `CopyPlugin` | `CopyScript` | Clipboard file copy + inter-plugin variable sharing |
| `CutPlugin` | `CutScript` | Clipboard file cut |
| `PastePlugin` | `PasteScript` | Paste with copy/move support, recursive directory copy |
| `DeletePlugin` | `DeleteScript` | File/folder delete with confirmation |
| `RenamePlugin` | `RenameScript` | Rename file or folder via prompt dialog |
| `MakeDirectoryPlugin` | `MakeDirectoryScript` | Create subfolder |
| `OpenInEditorPlugin` | `TextPadFileScript` | Open files via launcher service |
| `CopyFullNamePlugin` | `CopyToClipboardFullNameScript` | Copy full path(s) to clipboard |
| `OpenTerminalPlugin` | `CommandPromptScript` | Open Windows Terminal (falls back to cmd) |
| `OpenInProjectEditorPlugin` | _(new, per spec §4.5)_ | Open file/folder in configured project editor (Zed, Notepad++, VS Code, etc.) |

5 service plugins ported:

| Plugin | Legacy Script | Features |
|---|---|---|
| `StartServicePlugin` | `StartServiceScript` | Start services, auto-escalates to admin |
| `StopServicePlugin` | `StopServiceScript` | Stop services, auto-escalates to admin |
| `RestartServicePlugin` | `RestartServiceScript` | Stop/wait/start with admin fallback |
| `RefreshServicesPlugin` | `RefreshServicesScript` | Re-enqueues service loading |
| `HideServicePlugin` | `HideServiceScript` | Remove services from visible list |

#### WPF UI (`Kexplorer.UI`)
- **`MainWindow`** — Tab-based shell with status bar, session state save/restore on startup/close, plugin manager initialization.
- **`ExplorerPanel`** — TreeView + DataGrid split panel implementing `IKexplorerShell`. Per-tab work queues (main + per-drive). Context menu building from plugin registry.
- **`ServicesPanel`** — DataGrid-based service list implementing `IServiceShell`. Service context menu from plugin registry.
- **`PluginContextAdapter`** — Bridges `IPluginContext` to WPF (clipboard, dialogs, process launching).
- **`PromptDialog`** — Simple input dialog for rename/mkdir operations.

#### Test Coverage (32 tests, all passing)

| Test Class | Count | Verifies |
|---|---|---|
| `WorkQueueTests` | 12 | Queue enqueue/execute, error handling, stop/dispose, bounded capacity, worker config |
| `FileSystemNodeTests` | 4 | Constructor, drive letter extraction, stale cascading |
| `FileEntryTests` | 2 | Constructor, FromFileInfo factory |
| `DirectoryLoaderTests` | 6 | Load children, recursive depth, load files, missing directory, drive nodes |
| `SessionStateTests` | 3 | Save/load round-trip, missing file default, invalid JSON default |
| `LauncherServiceTests` | 2 | Load/save round-trip, missing file |
| `PluginManagerTests` | 3 | Register folder/file plugins, assembly scanning discovers built-ins |

---

---

## Legacy → Modern Adapter Mapping

### `ISimpleKexplorerGUI` → `IKexplorerShell`

| Legacy member | Modern equivalent | Notes |
|---|---|---|
| `MainForm` | _(removed)_ | Work items must never hold a direct Form reference in the modern model. UI dispatch is the shell's responsibility. |
| `TreeView1` | _(removed)_ | Work items do not access the tree directly. Call `shell.RefreshPathAsync(path)` to signal the UI to update a folder node. |
| `DataGridView1` | _(removed)_ | Same pattern as `TreeView1`. |
| `DirTreeMenu` | _(removed)_ | Context menus are a pure UI concern and belong in `Kexplorer.UI`, not in work items. |
| `FileGridMenuStrip` | _(removed)_ | Same as above. |
| `WatchingForFolder` | `shell.RefreshPathAsync(path)` | The legacy field tracked which folder the pipeline was currently populating. The modern idiom is for a work item to call `RefreshPathAsync` when it has results ready. |

### `IWorkGUIFlagger` → (no equivalent needed)

| Legacy member | Modern equivalent | Notes |
|---|---|---|
| `SignalBeginGUI()` | _(removed)_ | WinForms required manual `Invoke`/`BeginInvoke` thread marshaling. With `async/await` on a WPF dispatcher the shell handles marshaling internally; work items need not signal anything. |
| `SignalEndGUI()` | _(removed)_ | Same as above. |

### `IWorkUnit` → `IWorkItem` / `DelegateWorkItem`

| Legacy member | Modern equivalent | Notes |
|---|---|---|
| `IWorkUnit DoJob()` | `Task ExecuteAsync(IKexplorerShell, CancellationToken)` | `DoJob` returned a chained work unit synchronously. The modern pattern uses async execution; chaining is done by enqueuing additional items inside `ExecuteAsync`. |
| `void Abort()` | `CancellationToken` | Cooperative cancellation via token replaces the explicit `Abort/StopThread` pattern. |
| `void YouWereAborted()` | _(merged into `ExecuteAsync`)_ | Handle `OperationCanceledException` inside `ExecuteAsync` to run any cleanup when a work item is cancelled. |

### `Pipeline` → `WorkQueue`

| Legacy behavior | Modern equivalent | Notes |
|---|---|---|
| Background `Thread` + `Queue` | `Channel<IWorkItem>` + `Task.Run` workers | `Channel<T>` gives thread-safe, async-friendly producer/consumer without manual locking. |
| `AddJob` (normal priority) | `queue.EnqueueAsync(item)` | Direct async enqueue; no lock required. |
| `AddPriorityJob` (high-priority, pre-empts current) | Phase 1: dedicated priority channel + `WorkQueueOptions.PriorityWorkerCount` | Not implemented in Phase 0 — the pre-emption logic tied strongly to WinForms UI state. Evaluate need during Phase 1. |
| "Wait for `MainForm.Visible` before starting" | `queue.StartAsync()` called from `App.OnStartup` after window is shown | The startup gate is now owned by the host, not the queue. |
| Thread.Abort (worker cleanup) | `CancellationToken` + graceful drain on `StopAsync` | `Thread.Abort` is removed in .NET 5+; cooperative cancellation is the default. |

---

## WorkQueue Test Coverage

| Test | Verifies |
|---|---|
| `EnqueuedItem_IsExecuted` | Basic enqueue → execute path |
| `MultipleItems_AllExecuted` | Multi-worker queue drains all items |
| `FailingItem_ReportsErrorToShell_AndContinues` | Error isolation and `ReportErrorAsync` callback |
| `StopAsync_CompletesGracefully` | `StopAsync` / `DisposeAsync` are re-entrant-safe |
| `StartAsync_CalledTwice_IsIdempotent` | Double-start does not spawn extra workers |
| `StopAsync_WithoutStart_DoesNotThrow` | Stop before start is safe |
| `BoundedQueue_BlocksWhenFull_ThenDrains` | Bounded capacity back-pressure |
| `WorkerCount_CanBeConfigured` | Multiple parallel workers |
| `Constructor_ThrowsOnZeroWorkerCount` | Guard rail on invalid options |
| `DelegateWorkItem_ExecutesDelegate` | Delegate adapter invocation |
| `DelegateWorkItem_ThrowsOnNullName` / `NullDelegate` | Guard rails on `DelegateWorkItem` |

---

## What Moves to Phase 2

- Retire `ConsoleManager` / `KexplorerConsole`; remove from build.
- **Service Deployment Registry (§4.7)** — service groups, deployment profiles, apply/teardown/update.
- Service profile clone, compare, export/import.
- **Docker Dashboard (§4.8)** — container list, log viewer, Compose stack management.
- Linked deployment profiles (Windows services + Docker Compose as one logical environment).
- Git status integration on tree nodes.
- SSH/SFTP replacing FTP.
- Command palette (`Ctrl+Shift+P`) and quick-open (`Ctrl+P`).
- Light/dark theming engine.
- Port remaining ~30 file/folder scripts as plugins.
- Add priority queue support to `WorkQueue` (legacy `AddPriorityJob` pre-emption).
