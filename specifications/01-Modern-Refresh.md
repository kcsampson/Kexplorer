# KExplorer Modern Refresh — Specification

**Version:** 0.1 — Draft  
**Date:** 2026-03-31  
**Author:** (owner) + AI assistant  
**Status:** Ideation / Discovery

---

## 1. Background & Motivation

KExplorer has been a personal power-user productivity tool since 2004 — a multi-tabbed, multi-threaded Windows Forms file explorer built in C# / .NET Framework. Its design philosophy is opinionated and deliberate:

| Principle | Rationale |
|---|---|
| **Multi-tab UI** | Work across many file-system locations simultaneously without opening N windows. |
| **Background threading** | Keep the UI responsive while drives and folders load. |
| **Custom context menus** | Avoid the sluggish Windows shell context-menu pipeline; add personal scripts. |
| **Scripting framework** | Pluggable `IScript`/`IFileScript`/`IFolderScript` system drives zip, clipboard, XML/XSLT, WinGrep, Beyond Compare, and other power-user workflows. |
| **Services tab** _(critical)_ | Start/stop Windows services without leaving the tool. One of the most important features — managing multiple conceptual deployments of ~10 services is a daily workflow. Currently limited to start/stop/restart; service _creation_ and _binPath configuration_ still done via painful `sc` commands in `.cmd` files. **Needs a major upgrade — see §4.7.** |
| **Embedded console** _(retired)_ | Run `cmd.exe` from within KExplorer. Never reached its potential — the UX wasn't good enough to compete with a real terminal. **Superseded by the AI-Powered Command Panel (§4.3.2).** |
| **FTP browsing** | Treat remote FTP sites as first-class tree nodes. |
| **Passive refresh (F5)** | No file-system watchers — predictable performance, no surprise reloads. |
| **No drag & drop** | Prevent accidental mass-moves. |

### What has changed since 2004

| Era | Primary workflow |
|---|---|
| 2004–2010 | Windows desktop dev, control-plane machines with Windows front-ends. |
| 2010–2024 | Browser-based software development (web stacks, cloud). |
| 2025→ | AI-augmented development — agents, copilots, MCP servers, prompt engineering, multi-repo orchestration. |

The tool needs a **Modern Refresh** that preserves the things that made it "fit like a glove" while evolving to support the latest developer workflows.

---

## 2. Goals

1. **Preserve core identity** — fast, tabbed file explorer with scriptable context menus, passive refresh, and no drag-and-drop. The workflow is primarily **point-and-click** (right-click context menus are the main interaction model), with a handful of function-key shortcuts for the top 3–4 most frequent actions.
2. **Modernize the platform** — move from .NET Framework / WinForms to a modern .NET (8+) stack while keeping the Windows-native feel.
3. **First-class AI agent integration** — expose KExplorer's file-system context and scripting power to AI agents (local and remote).
4. **Refreshed script/plugin model** — make it trivial to author, share, and discover new scripts/plugins.
5. **Better developer-workflow integration** — deep-link into VS Code, terminals, Git, Docker, cloud CLIs, and browser DevTools.
6. **Modern UX polish** — light/dark theme, DPI awareness, search/filter everywhere, better keyboard navigation.

---

## 3. Architecture Options (to be decided)

| Option | Stack | Pros | Cons |
|---|---|---|---|
| **A — WinForms on .NET 8** | C#, .NET 8, WinForms | Minimal rewrite; keep existing controls | WinForms aging; limited modern UI |
| **B — WPF / WinUI 3** | C#, .NET 8, WPF or WinUI 3 | Native Windows; rich UI; MVVM | Larger rewrite; WPF learning curve |
| **C — Hybrid (Electron / Tauri + web UI)** | C# backend ↔ TypeScript/React frontend | Cross-platform possible; modern web UI | Heavier runtime (Electron); two tech stacks |
| **D — .NET MAUI Blazor Hybrid** | C#, .NET 8, Blazor | Single codebase, web + native | MAUI maturity on Windows desktop |

> **Recommendation:** Start with **Option B (WPF on .NET 8)** — stays in a single C# codebase, keeps Windows-native performance, and opens the door to rich theming and modern controls. Evaluate WinUI 3 if packaging via MSIX is desirable.

---

## 4. Feature Areas

### 4.1 Core File Explorer (Retain & Enhance)

| Feature | Current | Refreshed |
|---|---|---|
| Multi-tab browsing | ✅ `TabControl` + `KexplorerPanel` | Tear-off tabs, tab groups (horizontal/vertical split), pinned tabs |
| TreeView + file grid | ✅ `TreeView` + `DataGridView` | Virtualised tree & grid for huge directories; column sorting; inline rename |
| Background loading | ✅ `Pipeline` + `IWorkUnit` | Modernise with `async/await`, `Channel<T>`, cancellation tokens |
| State persistence | ✅ XML file (`KexplorerStateSave.xml`) | Migrate to JSON; save/restore full workspace (all tabs, scroll positions, column widths) |
| Custom launchers | ✅ `Launchers.xml` | Keep XML or move to JSON; add "Open in Project Editor" (configurable: Zed, Notepad++, VS Code, etc.), "Open in Terminal", "Open in Browser" |
| Passive refresh (F5) | ✅ | Keep. Optionally allow opt-in file watcher per tab. |
| No drag-and-drop | ✅ | Keep as default. Add a "careful mode" toggle if ever needed. |
| Folder size rollup | _(none)_ | Background-calculate total size (all nested files) per visible folder; display in-panel — see §4.1.1 |

#### 4.1.1 Folder Size Rollup (New)

**User Story:** _"Often I need to clean up my disk or find interesting things on disk. The interesting spots are typically where there is more content — more files and larger sizes — underneath a folder. I want to see that at a glance without manually drilling in."_

**Behaviour:**

| Aspect | Detail |
|---|---|
| **Trigger** | When a folder's children are displayed in the tree, a background work item is enqueued to walk each visible child folder recursively and sum the total size of all contained files. |
| **Display** | The calculated size is shown to the right of the folder name in the tree panel (e.g., `Documents  [12.4 GB]`). Use a subtle/secondary style so it doesn't compete with the folder name. |
| **Progressive update** | Sizes appear incrementally as each folder completes — no need to wait for all folders. |
| **Cancellation** | If the user navigates away or expands a different node before calculation finishes, outstanding calculations for the previous view are cancelled. |
| **Caching** | Computed sizes are cached on the `FileSystemNode`. Invalidated when the node is marked stale (F5 refresh). |
| **Performance** | Runs on a low-priority background thread. Must not block tree navigation or file-list loading. Large trees (e.g., `C:\`) should use bounded parallelism to avoid disk thrashing. |
| **Sort support** | Optionally allow sorting child folders by total size (largest first) to quickly find disk-space hogs. |

### 4.2 Scripting & Plugin System (Modernize)

| Current | Refreshed |
|---|---|
| `IScript`, `IFileScript`, `IFolderScript`, `IServiceScript`, `IMixedScript`, `IFTPFileScript` hierarchy | Unified `IKexplorerPlugin` interface with capability attributes (`[FileContext]`, `[FolderContext]`, `[ServiceContext]`, `[GlobalContext]`) |
| Scripts compiled into main assembly | Load plugins from a `plugins/` directory (assembly scanning) or NuGet packages |
| `ScriptHelper` provides clipboard, paths, form refs | Modernised `IPluginContext` providing file-system, clipboard, notification, AI-agent, and terminal APIs |
| ~45 built-in scripts (copy, paste, zip, Beyond Compare, WinGrep, ~~XSLT~~, etc.) | Port all as built-in plugins; add new ones (see §4.5). **XML/XSLT scripts deprecated** — no longer a daily workflow. |

### 4.3 AI Agent Integration (New)

This is the headline addition. KExplorer becomes both a **tool for the developer** and a **tool surface for AI agents**.

#### 4.3.1 Model Context Protocol (MCP) Server

Expose an MCP server that lets AI agents (GitHub Copilot, Claude, custom agents) interact with KExplorer:

| MCP Tool | Description |
|---|---|
| `kexplorer.listDirectory` | List files/folders at a path |
| `kexplorer.readFile` | Read file contents (with line ranges) |
| `kexplorer.writeFile` | Write / patch a file |
| `kexplorer.search` | Full-text search (grep) across a directory tree |
| `kexplorer.runScript` | Invoke any KExplorer plugin by name with parameters |
| `kexplorer.getOpenTabs` | Return the paths currently open in KExplorer tabs |
| `kexplorer.openTab` | Open a new tab at a given path |
| `kexplorer.executeCommand` | Run a shell command in KExplorer's embedded terminal |
| `kexplorer.getServices` | List Windows services and their state |
| `kexplorer.controlService` | Start / stop / restart a service |
| `kexplorer.listDeploymentProfiles` | List all deployment profiles and their status |
| `kexplorer.applyProfile` | Apply (create/update) all services in a deployment profile |
| `kexplorer.teardownProfile` | Stop and delete all services in a deployment profile |
| `kexplorer.updateProfileVariable` | Change a variable (e.g., `basePath`) in a profile and re-apply |
| `kexplorer.listContainers` | List Docker containers with status, ports, image |
| `kexplorer.controlContainer` | Start / stop / restart / remove a Docker container |
| `kexplorer.containerLogs` | Retrieve logs from a container (with tail/follow options) |
| `kexplorer.composeUp` | Start a registered Compose stack |
| `kexplorer.composeDown` | Stop a registered Compose stack |

This lets an AI agent say _"open a tab at `C:\repos\myproject`, search for TODO comments, then zip the results"_ — all through KExplorer.

#### 4.3.2 AI-Powered Command Panel (replaces Embedded Console)

The original embedded console (`ConsoleManager` / `KexplorerConsole`) never reached its potential — it was too bare-bones to replace a real terminal, so it went unused. Rather than trying to build _another_ terminal emulator, we replace the console with something uniquely valuable: an **AI-assisted command & chat panel** that makes the user dramatically more efficient at the command line _and_ at ad-hoc tasks.

##### Design Philosophy

> _"I still want to do things from a command line, but I want AI riding shotgun — auto-completing, suggesting, explaining, and executing on my behalf."_

This is a **watered-down agentic experience** — not a full autonomous agent, but a smart copilot embedded in KExplorer that sits between the user and the shell.

##### Core Capabilities

| Capability | Description |
|---|---|
| **Smart command input** | A single input bar (like a terminal prompt) where the user types commands. AI provides real-time inline ghost-text completions (like GitHub Copilot in the terminal). Press `Tab` to accept, keep typing to ignore. |
| **Natural-language → command translation** | Type a plain-English intent (e.g., _"find all .json files modified today"_) and the AI translates it to the correct PowerShell/bash/cmd command. The translated command is shown for review; press `Enter` to run. |
| **Context-aware suggestions** | The AI knows the current KExplorer tab path, selected files, recent commands, and OS environment. Suggestions are grounded in what's actually on disk. |
| **Command explanation** | Highlight any command (yours or AI-suggested) and press `Ctrl+E` → the AI explains what it does, flag-by-flag. |
| **Error recovery** | When a command fails, the AI reads stderr and suggests a fix or an alternative command. |
| **Multi-step recipes** | Ask for a multi-step task (e.g., _"back up this folder, then deploy"_). The AI proposes a sequence of commands. User approves each step, or approves all at once. |
| **Conversational follow-ups** | After a command runs, ask follow-up questions: _"why did that fail?"_, _"now do the same for the other folder"_, _"can you make that a script?"_. Chat history is preserved per tab. |
| **Plugin invocation** | The AI can suggest or invoke KExplorer plugins from the command panel (with user confirmation). E.g., _"zip these results"_ triggers `ZipScript`. |
| **Clipboard integration** | `Ctrl+Shift+V` pastes clipboard contents and asks the AI to interpret them (decode JWT, format JSON, explain a stack trace, etc.). |

##### UI Layout

```
┌─────────────────────────────────────────────────────────┐
│  [Tab: C:\repos\myproject]  [Tab: Services]  [+]       │
├──────────────┬──────────────────────────────────────────┤
│  TreeView    │  File Grid                               │
│              │                                          │
│              │                                          │
│              ├──────────────────────────────────────────┤
│              │  AI Command Panel                        │
│              │  ┌─ chat history / output ─────────────┐ │
│              │  │ > dir *.json /s                      │ │
│              │  │ (3 files found)                      │ │
│              │  │                                      │ │
│              │  │ You: find large files over 100MB     │ │
│              │  │ AI:  Get-ChildItem -Recurse |        │ │
│              │  │      Where Length -gt 100MB          │ │
│              │  │      [Enter to run] [Edit] [Explain] │ │
│              │  └────────────────────────────────────┘ │
│              │  ❯ _                        [⌘ AI ◉ On] │
├──────────────┴──────────────────────────────────────────┤
│  Status: C:\repos\myproject  |  main  |  3 items       │
└─────────────────────────────────────────────────────────┘
```

- **Dockable** — can be bottom panel (default), side panel, or popped out as a floating window.
- **Resizable** — drag the splitter; collapse to just the input bar for minimal footprint.
- **Toggle AI** — a button/shortcut (`Ctrl+\``) toggles between raw shell mode (plain PowerShell/cmd, no AI) and AI-assisted mode, for users who sometimes just want a terminal.

##### AI Behaviour Guardrails

| Rule | Detail |
|---|---|
| **Never auto-execute** | AI _suggests_ commands; the user must press `Enter` or click "Run" to execute. No silent side-effects. |
| **Destructive-command warnings** | If the AI detects a destructive pattern (`rm -rf`, `del /s`, `format`, `DROP TABLE`), it highlights the command in red and asks for explicit confirmation. |
| **Scope awareness** | By default the AI operates in the context of the current tab's directory. It must ask before touching paths outside that scope. |
| **Token / cost controls** | Configurable max tokens per request. Show estimated cost per query when using paid APIs. |
| **Offline fallback** | When no AI backend is available, the panel degrades gracefully to a plain command prompt with local history and tab-completion only (still better than the old console). |

##### Configurable Backends

- OpenAI (GPT-4o, etc.)
- Azure OpenAI
- Anthropic (Claude)
- Ollama / LM Studio (local models — fully offline)
- Custom OpenAI-compatible endpoint

Configured via `~/.kexplorer/ai.json`. The panel shows which backend is active.

#### 4.3.3 Agent-Authored Scripts

- AI can generate new KExplorer plugins on the fly (C# source → Roslyn compile → load).
- Saved to `plugins/ai-generated/` for review and reuse.

### 4.4 Developer Workflow Integration (New & Enhanced)

| Feature | Detail |
|---|---|
| **Git integration** | Show git status icons on tree nodes; context-menu: diff, log, blame, stage, commit, push |
| **Terminal tabs** | The old `ConsoleManager` is retired (see §4.3.2 — AI Command Panel). For users who still want a standalone terminal, add "Open in Windows Terminal" context-menu action rather than embedding another terminal emulator. |
| **Editor integration** | "Open in Project Editor" for files/folders using a configurable editor command (`launchers.json` → `projectEditor`). Supports any CLI-launchable editor: Zed (`zed`), Notepad++ (`notepad++`), VS Code (`code`), Sublime (`subl`), Cursor (`cursor`), etc. |
| **Docker / Container awareness** | Same "repeated tedious commands" problem as Windows services — `docker container ls -a`, `docker logs`, `docker compose up`, etc. typed over and over. Elevated to a full **Docker Dashboard (§4.8)**. |
| **Cloud CLI shortcuts** | Context-menu hooks for `az`, `aws`, `gcloud`, `kubectl` at the current path |
| **HTTP / API testing** | Right-click a `.http` or `.rest` file → execute request and show response |
| **Markdown preview** | Preview `.md` files in an embedded panel |
| **SSH / SFTP** | Replace legacy FTP with SSH/SFTP (FTP is mostly obsolete); key-based auth |

### 4.5 New Built-in Plugins

| Plugin | Description |
|---|---|
| `GitStatusScript` | Show git status for selected folder |
| `GitDiffScript` | Open diff for selected file |
| `OpenInProjectEditor` | Open file/folder in the configured project editor (Zed, Notepad++, VS Code, etc.) |
| `OpenTerminalHere` | Open Windows Terminal / PowerShell at path |
| `DockerLogsScript` | Tail logs of a running container |
| `JsonPrettyPrint` | Format JSON files _(future — use case TBD; may expand to validate, diff, query with jq/JSONPath)_ |
| `JwtDecode` | Decode a JWT from clipboard |
| `Base64EncodeDecode` | Encode/decode base64 |
| `HashFileScript` | Compute MD5/SHA256 of selected files |
| `AskAIAboutFile` | Send file contents to AI chat with a prompt |
| `SearchWithAI` | Semantic search across a folder tree using embeddings |

### 4.6 UX Modernization

| Area | Detail |
|---|---|
| **Theming** | Light and dark mode; accent colour picker; follows Windows system theme |
| **DPI / scaling** | Per-monitor DPI aware; sharp on 4K displays |
| **Command palette** | `Ctrl+Shift+P` opens a fuzzy-searchable command palette (like VS Code) |
| **Keyboard-first _where it counts_** | Function-key shortcuts for the top actions (F5 refresh, etc.); the rest stays point-and-click via context menus — don't over-rotate on hotkeys |
| **Quick-open** | `Ctrl+P` fuzzy file finder across all open tabs / recent paths |
| **Breadcrumb bar** | Clickable path breadcrumbs above the file grid |
| **Status bar** | Show background job progress, current path, file count, git branch |
| **Notifications** | Toast-style notifications for long-running operations completing |

### 4.7 Service Deployment Registry (Major Enhancement)

#### The Problem Today

The current Services tab (`ServiceMgrWorkUnit`) can list services from local/remote machines, start/stop/restart them, filter by regex, and persist a visible-services list to `KexplorerStateSave.xml`. That's useful for _runtime control_.

But the **hard part** — actually _creating_ and _configuring_ those services — lives outside KExplorer in a scattered collection of `.cmd` files full of `sc create` / `sc config` commands with difficult escaping:

```cmd
sc create MyService.Api binPath= "C:\deploys\release-2.1\MyService.Api.exe --urls=http://+:5010" start= auto obj= "NT AUTHORITY\NETWORK SERVICE"
```

The only thing that changes between conceptual deployments is usually the `binPath` (and sometimes the port or service account). Yet you end up maintaining **N copies** of nearly identical `.cmd` files — one per deployment scenario — and editing them by hand is tedious and error-prone.

The enterprise software has a **semi-microservices architecture**: roughly **10 Windows services** that comprise the full system, but you might be working with **3–6 conceptual deployments** at any given time (e.g., `dev-local`, `dev-shared`, `feature-branch-X`, `staging`, `qa-hotfix`, `production-mirror`).

#### The Solution: A Service Deployment Registry

KExplorer should have an internal **registry** that models this complexity and eliminates the `.cmd` file sprawl.

##### Core Concepts

| Concept | Description |
|---|---|
| **Service Definition** | A template describing one Windows service: service name pattern, display name, description, executable path (with placeholders), start type, service account, dependencies, recovery options. |
| **Service Group** | A named collection of Service Definitions that together form a complete deployment (e.g., "Our Platform" = Api + Worker + Scheduler + Gateway + …). The ~10 services. |
| **Deployment Profile** | A named configuration that binds a Service Group to concrete values: base path, port range, service name prefix/suffix, service account, machine name. This is the thing that varies. |
| **Deployment Instance** | A Deployment Profile that has been _applied_ — real Windows services exist on the machine. KExplorer tracks what it deployed so it can update or tear it down. |

##### Example

```
Service Group: "Acme Platform"
  ├─ Acme.Api           → {basePath}\Acme.Api.exe --urls=http://+:{apiPort}
  ├─ Acme.Worker        → {basePath}\Acme.Worker.exe
  ├─ Acme.Scheduler     → {basePath}\Acme.Scheduler.exe
  ├─ Acme.Gateway       → {basePath}\Acme.Gateway.exe --urls=http://+:{gatewayPort}
  └─ ... (10 total)

Deployment Profile: "dev-local"
  basePath    = C:\repos\acme\src\bin\Debug\net8.0
  apiPort     = 5010
  gatewayPort = 5020
  namePrefix  = DevLocal.
  machine     = .  (local)

Deployment Profile: "staging-mirror"
  basePath    = C:\deploys\staging-2.1
  apiPort     = 6010
  gatewayPort = 6020
  namePrefix  = Staging.
  machine     = .  (local)
```

Applying "dev-local" creates: `DevLocal.Acme.Api`, `DevLocal.Acme.Worker`, etc. — all with the correct `binPath`, correct ports, correct everything. **One click, ten services, no `.cmd` files.**

##### UI — Service Registry Tab

```
┌─────────────────────────────────────────────────────────────────┐
│  [Tab: Files]  [Tab: ★ Service Registry]  [Tab: Services]  [+] │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Service Groups                Deployment Profiles              │
│  ┌───────────────────────┐     ┌──────────────────────────────┐ │
│  │ ▶ Acme Platform (10)  │     │  dev-local      [✅ Applied] │ │
│  │   ├ Acme.Api          │     │  dev-shared     [✅ Applied] │ │
│  │   ├ Acme.Worker       │     │  feature-auth   [⬜ Ready  ] │ │
│  │   ├ Acme.Scheduler    │     │  staging-mirror [✅ Applied] │ │
│  │   ├ Acme.Gateway      │     │  qa-hotfix      [⬜ Ready  ] │ │
│  │   └ ...               │     │                              │ │
│  └───────────────────────┘     └──────────────────────────────┘ │
│                                                                 │
│  Profile: "dev-local"                        [Apply] [Teardown] │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │  basePath:    C:\repos\acme\src\bin\Debug\net8.0           │ │
│  │  apiPort:     5010                                         │ │
│  │  gatewayPort: 5020                                         │ │
│  │  namePrefix:  DevLocal.                                    │ │
│  │  machine:     .                                            │ │
│  │                                                            │ │
│  │  Services:          Status:         binPath:               │ │
│  │  DevLocal.Acme.Api  ● Running       C:\repos\...\Api.exe  │ │
│  │  DevLocal.Acme.Work ● Running       C:\repos\...\Work.exe │ │
│  │  DevLocal.Acme.Schd ○ Stopped       C:\repos\...\Schd.exe │ │
│  │                                                            │ │
│  │           [Start All] [Stop All] [Restart All]             │ │
│  └────────────────────────────────────────────────────────────┘ │
│                                                                 │
│  ❯ AI: "switch dev-local to the release-2.2 build"  [⌘ AI ◉]  │
└─────────────────────────────────────────────────────────────────┘
```

##### Key Operations

| Operation | What it does |
|---|---|
| **Create Service Group** | Define the ~10 services as templates with `{placeholder}` variables for the parts that change. |
| **Create Deployment Profile** | Fill in concrete values for each placeholder. Can clone an existing profile and change just the `basePath`. |
| **Apply Profile** | Runs `sc create` / `sc config` for each service in the group using the profile's values. KExplorer generates the correct commands internally — **no manual escaping, no `.cmd` files**. Requires elevation (prompts for admin). |
| **Update Profile** | Change a value (e.g., point `basePath` to a new build folder) → KExplorer runs `sc config` to update only the services that changed. |
| **Teardown Profile** | Stops all services in the profile, then runs `sc delete` for each. Clean removal. |
| **Start / Stop / Restart All** | Bulk-control all services in a profile. Respects dependency order if defined. |
| **Compare Profiles** | Side-by-side diff of two profiles to see what's different (useful when debugging "why does staging work but dev doesn't?"). |
| **Export / Import** | Export a Service Group + Profile as a portable JSON file to share with teammates or transfer between machines. Replaces the `.cmd` file exchange pattern. |
| **Clone Profile** | Duplicate an existing profile, change the `basePath` or `namePrefix`, apply — instant new deployment. |
| **Audit Log** | Record every `sc` command KExplorer has executed, when, and the result. Useful for troubleshooting. |

##### Integration with Existing Services Tab

The current Services tab (flat list of services on a machine with start/stop/restart) **stays as-is** for ad-hoc service management. The new **Service Registry tab** is a higher-level view organised by deployment profiles. They complement each other:

- **Services tab** → "show me all services on this machine, let me poke at them"  
- **Service Registry tab** → "show me my deployments, let me manage them as logical units"

Services created via the Registry also appear in the Services tab automatically.

##### AI Command Panel Integration

The AI Command Panel (§4.3.2) understands the Service Registry:

- _"switch dev-local to the release-2.2 build"_ → updates `basePath`, runs `sc config` for all 10 services, restarts them.
- _"clone staging-mirror as qa-hotfix on port range 7000"_ → creates a new profile, applies it.
- _"why is DevLocal.Acme.Gateway failing to start?"_ → checks Windows Event Log, reads service config, suggests fix.
- _"show me the difference between dev-local and staging-mirror"_ → opens profile comparison.

##### Data Model

Stored in `~/.kexplorer/service-registry.json`:

```json
{
  "serviceGroups": [
    {
      "name": "Acme Platform",
      "services": [
        {
          "templateName": "Acme.Api",
          "displayNameTemplate": "{namePrefix}Acme API Service",
          "serviceNameTemplate": "{namePrefix}Acme.Api",
          "binPathTemplate": "\"{basePath}\\Acme.Api.exe\" --urls=http://+:{apiPort}",
          "startType": "auto",
          "serviceAccount": "NT AUTHORITY\\NETWORK SERVICE",
          "description": "Acme API gateway service",
          "dependencies": [],
          "recoveryActions": { "firstFailure": "restart", "secondFailure": "restart", "subsequentFailures": "restart", "resetPeriodDays": 1 }
        }
      ]
    }
  ],
  "deploymentProfiles": [
    {
      "name": "dev-local",
      "serviceGroup": "Acme Platform",
      "machine": ".",
      "variables": {
        "basePath": "C:\\repos\\acme\\src\\bin\\Debug\\net8.0",
        "apiPort": "5010",
        "gatewayPort": "5020",
        "namePrefix": "DevLocal."
      },
      "applied": true,
      "lastApplied": "2026-03-28T14:30:00Z"
    }
  ],  "auditLog": []
}
```

### 4.8 Docker Dashboard (New)

#### The Same Problem, Different Runtime

The Service Deployment Registry (§4.7) solves the "repeated tedious commands" problem for Windows services. Docker containers have the **exact same problem** — the same handful of `docker` commands typed over and over, day after day:

```powershell
docker container ls -a                           # what's running?
docker logs my-api --tail 200 -f                 # why did it crash?
docker compose -f .\dev-local.yml up -d          # start the stack
docker compose -f .\dev-local.yml down           # stop the stack
docker exec -it my-api /bin/sh                   # get a shell
docker cp my-api:/app/logs/error.log .           # grab a file
docker image ls                                  # what images do I have?
docker system prune -f                           # reclaim disk space
```

None of these are _hard_, but they're tedious, repetitive, and easy to fat-finger a container name. When you're juggling 3–6 deployment contexts that also include Docker Compose stacks alongside Windows services, this friction adds up fast.

#### The Solution: A Docker Panel in KExplorer

A dedicated **Docker Dashboard tab** that provides a point-and-click UI over the Docker CLI, with the AI Command Panel available for anything more complex.

##### UI — Docker Dashboard Tab

```
┌──────────────────────────────────────────────────────────────────┐
│  [Tab: Files]  [Tab: Service Registry]  [Tab: 🐳 Docker]  [+]  │
├──────────────────────────────────────────────────────────────────┤
│                                                                  │
│  Compose Stacks               Containers                         │
│  ┌──────────────────────┐     ┌────────────────────────────────┐ │
│  │ ▶ dev-local (3/3 ●)  │     │  Name            Status   CPU  │ │
│  │ ▶ staging   (2/3 ●○) │     │  devlocal-api    ● Up     2%   │ │
│  │   orphaned  (1   ○)  │     │  devlocal-db     ● Up     5%   │ │
│  └──────────────────────┘     │  devlocal-redis  ● Up     1%   │ │
│                               │  staging-api     ● Up     3%   │ │
│  Images          Volumes      │  staging-worker  ○ Exited  —   │ │
│  ┌────────┐  ┌──────────┐    │  old-test        ○ Exited  —   │ │
│  │ 12 imgs│  │  8 vols  │    └────────────────────────────────┘ │
│  └────────┘  └──────────┘                                        │
│                                                                  │
│  Container: "devlocal-api"      [▶ Start] [■ Stop] [↻ Restart]  │
│  ┌──────────────────────────────────────────────────────────────┐│
│  │  Image:   acme/api:dev-latest                                ││
│  │  Ports:   5010:80, 5011:443                                  ││
│  │  Mounts:  C:\repos\acme\src → /app                           ││
│  │  Status:  Up 2 hours (healthy)                               ││
│  │                                                              ││
│  │  [View Logs]  [Shell]  [Inspect]  [Copy File From...]        ││
│  └──────────────────────────────────────────────────────────────┘│
│                                                                  │
│  ❯ AI: "why did staging-worker exit?"                [⌘ AI ◉]   │
└──────────────────────────────────────────────────────────────────┘
```

##### Core Features

| Feature | Description |
|---|---|
| **Container list** | Live table of all containers (running + stopped) with name, image, status, port mappings, CPU/memory. Auto-refreshes on a timer (configurable, e.g., every 5s) or on-demand (F5). |
| **Right-click context menu** | Point-and-click for the everyday actions: Start, Stop, Restart, Remove, View Logs, Shell (exec), Inspect, Copy File From/To. No more typing `docker logs <name>`. |
| **Log viewer** | Built-in scrollable log panel with tail/follow mode, search/filter, and timestamp highlighting. Replaces `docker logs -f <name>` in a terminal window you keep losing. |
| **Compose stack grouping** | Containers are grouped by their Compose project. Start/stop/restart an entire stack with one click. Supports multiple Compose files. |
| **Compose file management** | Register Compose files (like deployment profiles in §4.7). Quick-switch between `dev-local.yml`, `staging.yml`, etc. `docker compose up -d` / `down` with one click. |
| **Image list** | View local images, tags, sizes. Right-click to remove, re-tag, or push. |
| **Volume list** | View volumes and their mount points. Right-click to inspect or remove. |
| **System stats** | Disk usage, total containers, total images. One-click prune (with confirmation). |
| **Exec / Shell** | Open an interactive shell into a running container — embedded in the AI Command Panel or launched in Windows Terminal. |
| **Copy files** | Browse a container's filesystem (via `docker cp`) from KExplorer's tree view paradigm — or right-click → "Copy from container" / "Copy to container". |

##### Integration with Service Registry (§4.7)

For setups that use _both_ Windows services and Docker containers (e.g., the API runs as a Windows service but the database runs in Docker), the **Deployment Profile** concept from §4.7 should be extensible:

| Deployment: "dev-local" |
|---|
| Windows Services: DevLocal.Acme.Api, DevLocal.Acme.Worker, … (via §4.7) |
| Docker Compose: `C:\repos\acme\docker\dev-local.yml` (via §4.8) |
| **[Start All]** = apply service profile + `docker compose up -d` |
| **[Stop All]** = stop all services + `docker compose down` |

This gives a **unified "start my whole dev environment" button** across both runtimes.

##### AI Command Panel Integration

The AI Command Panel (§4.3.2) understands Docker:

- _"what's using the most memory?"_ → sorts containers by memory, shows top offenders.
- _"show me the last 50 lines of the staging-api logs"_ → `docker logs staging-api --tail 50` and displays inline.
- _"why did staging-worker exit?"_ → reads `docker inspect` exit code + last logs, explains the failure.
- _"restart the whole dev-local stack"_ → `docker compose -f dev-local.yml restart`.
- _"clean up all stopped containers and dangling images"_ → generates `docker system prune` with appropriate flags, asks for confirmation.
- _"exec into devlocal-api and check the /app/config folder"_ → opens shell, runs `ls /app/config`, shows results.

##### Data Model

Registered Compose stacks stored in `~/.kexplorer/docker-registry.json`:

```json
{
  "composeStacks": [
    {
      "name": "dev-local",
      "composeFile": "C:\\repos\\acme\\docker\\dev-local.yml",
      "envFile": "C:\\repos\\acme\\docker\\.env.dev",
      "linkedDeploymentProfile": "dev-local",
      "autoStart": false
    },
    {
      "name": "staging",
      "composeFile": "C:\\repos\\acme\\docker\\staging.yml",
      "envFile": null,
      "linkedDeploymentProfile": "staging-mirror",
      "autoStart": false
    }
  ],
  "settings": {
    "refreshIntervalSeconds": 5,
    "dockerHost": "npipe:////./pipe/docker_engine",
    "showStoppedContainers": true
  }
}
```

---

## 5. Data & Configuration

| Item | Current | Refreshed |
|---|---|---|
| State save | `KexplorerStateSave.xml` | `~/.kexplorer/state.json` |
| Launchers | `Launchers.xml` | `~/.kexplorer/launchers.json` |
| Script config | `scripthelper.xml` | `~/.kexplorer/plugins.json` |
| Service registry | — (scattered `.cmd` files) | `~/.kexplorer/service-registry.json` (service groups, deployment profiles, audit log — see §4.7) |
| Docker registry | — (scattered `docker` commands) | `~/.kexplorer/docker-registry.json` (compose stacks, linked profiles — see §4.8) |
| AI config | — | `~/.kexplorer/ai.json` (API keys, model prefs, MCP config) |
| Settings | Scattered | Unified `~/.kexplorer/settings.json` with schema for intellisense |

---

## 6. Migration Strategy

### Phase 0 — Foundation (this phase)
- [x] Write this specification.
- [x] Create a new .NET 8 solution alongside the existing one.
- [x] Set up project structure: `Kexplorer.Core`, `Kexplorer.UI`, `Kexplorer.Plugins`, `Kexplorer.MCP`, `Kexplorer.AI`.
- [x] Initial port of `ISimpleKexplorerGUI` contract shape → `IKexplorerShell` abstraction (adapter mapping pending).
- [x] Initial port of `Pipeline` / `IWorkUnit` concept → async `WorkQueue` with `Channel<T>` (tests and legacy adapters pending).

#### Phase 0 Execution Plan

1. **Baseline and branch hygiene**
  - Confirm current legacy projects still build as-is.
  - Create a dedicated migration branch.
  - Capture current behavior notes for the existing `Pipeline` and GUI callback contracts.

2. **Create parallel modern solution**
  - Add `Kexplorer.Modern.sln` at repo root (do not replace legacy `.sln` files yet).
  - Target `net8.0-windows` for UI and `net8.0` for non-UI libraries.
  - Keep legacy and modern solutions buildable side-by-side.

3. **Scaffold projects and references**
  - `Kexplorer.Modern/Kexplorer.Core` (class library): domain models, filesystem abstractions, work queue contracts.
  - `Kexplorer.Modern/Kexplorer.UI` (WPF app): shell host, tab shell, explorer surface placeholders.
  - `Kexplorer.Modern/Kexplorer.Plugins` (class library): plugin contracts and built-in plugin registration.
  - `Kexplorer.Modern/Kexplorer.MCP` (class library): MCP tool contract surface.
  - `Kexplorer.Modern/Kexplorer.AI` (class library): AI provider abstractions and command-panel service contracts.
  - Add strict project reference direction:
    - `UI` -> `Core`, `Plugins`, `AI`, `MCP`
    - `Plugins` -> `Core`
    - `MCP` -> `Core`, `Plugins`
    - `AI` -> `Core`, `Plugins`
    - `Core` has no dependency on UI.

4. **Port shell abstraction first**
  - Extract the minimum behavior currently represented by `ISimpleKexplorerGUI` into `IKexplorerShell` in `Kexplorer.Core`.
  - Keep methods task-based where asynchronous work is expected.
  - Add adapter notes mapping old WinForms interaction points to new shell callbacks.

5. **Port work engine to async queue**
  - Introduce `IWorkItem`, `IWorkQueue`, and `WorkQueue` using `Channel<T>`.
  - Add cancellation support, bounded/unbounded mode, and graceful shutdown semantics.
  - Keep compatibility adapters so legacy `IWorkUnit` logic can be wrapped incrementally.

6. **Validation and checkpoints**
  - Build check: modern solution builds cleanly.
  - Smoke check: enqueue/dequeue/complete/error flow proven in tests.
  - Contract check: at least one legacy interaction path mapped to `IKexplorerShell` without UI deadlock.

#### Phase 0 Deliverables

- `Kexplorer.Modern.sln` added.
- Five modern projects created with compile-clean references.
- Initial `IKexplorerShell` contract and legacy mapping notes.
- Initial `WorkQueue` implementation with cancellation and tests.
- `MIGRATION_NOTES.md` documenting what is complete and what moves to Phase 1.

#### Phase 0 Definition of Done

- Legacy solution remains untouched and buildable.
- Modern solution builds on a clean machine with .NET 8 SDK.
- Core queue architecture exists and is test-backed.
- No blocking architectural unknowns remain for starting Phase 1 shell work.

### Phase 1 — Core Explorer
- [ ] WPF shell with tab control, tree view, file grid.
- [ ] Port `KExplorerControl`, `KExplorerNode`, `Launcher`.
- [ ] Port state persistence (JSON).
- [ ] Port top-10 most-used scripts as plugins.
- [ ] Port existing Services tab (start/stop/restart, add machine, hide services).

### Phase 2 — Modern Workflows
- [ ] Retire `ConsoleManager` / `KexplorerConsole`; remove from build.
- [ ] **Service Deployment Registry (§4.7)** — service groups, deployment profiles, apply/teardown/update.
- [ ] Service profile clone, compare, export/import.
- [ ] **Docker Dashboard (§4.8)** — container list, log viewer, Compose stack management.
- [ ] Linked deployment profiles (Windows services + Docker Compose as one logical environment).
- [ ] Git status integration.
- [ ] VS Code / editor launch integration.
- [ ] SSH/SFTP replacing FTP.
- [ ] Command palette and quick-open.

### Phase 3 — AI Integration
- [ ] AI-Powered Command Panel (§4.3.2) — replaces embedded console.
- [ ] AI inline completions and natural-language → command translation.
- [ ] MCP server implementation.
- [ ] Agent-authored plugin generation (Roslyn).
- [ ] Semantic search plugin.

### Phase 4 — Polish & Ecosystem
- [ ] Theming engine (light/dark/custom).
- [ ] Plugin marketplace / sharing.
- [ ] Documentation & onboarding.
- [ ] Telemetry (opt-in) for self-improvement.

---

## 7. Open Questions

1. **WPF vs WinUI 3?** — WPF is battle-tested; WinUI 3 is the "future" but still maturing. Decision needed.
2. **Plugin language** — C# only, or also support Python/TypeScript plugins via process isolation?
3. **MCP transport** — stdio (for local agents) vs SSE/HTTP (for remote agents) vs both?
4. **Licensing** — Keep personal-use, or open-source?
5. **Naming** — Keep "KExplorer" or rebrand for the new era?
6. **Config location** — `~/.kexplorer/` (Linux convention) vs `%APPDATA%\KExplorer\` (Windows convention)?
7. **Backward compatibility** — Should the new app be able to import the old `KexplorerStateSave.xml` and `Launchers.xml`?
8. **Service Registry scope** — Should the registry support remote machines (apply profiles over the network via `sc \\MACHINE`), or start local-only?
9. **Service Registry sharing** — Should profiles be exportable as standalone JSON that teammates can import, or also as generated `.cmd` files for environments where KExplorer isn't installed?
10. **Docker API vs CLI** — Should the Docker Dashboard use the Docker Engine API (via REST over named pipe) for speed and richer data, or shell out to `docker` CLI for simplicity? Or start with CLI and migrate to API later?

---

## 8. Success Criteria

- [ ] All current daily-use workflows achievable in the new version.
- [ ] AI agent can perform a multi-step file-system task via MCP without the user touching the mouse.
- [ ] "Start my whole dev environment" (Windows services + Docker containers) in one click.
- [ ] Cold start under 2 seconds.
- [ ] Plugin authoring: new script from idea to working context-menu item in < 5 minutes.
- [ ] The tool still "fits like a glove."

---

## 9. Scope Discipline

KExplorer started as a file explorer and has grown organically — services management, Docker, AI. Each addition was justified because it eliminates real daily friction. But there's a risk of becoming "a tool for everyone" that does nothing well.

**Guiding principle:** KExplorer is a **personal developer productivity cockpit for a Windows power-user**. Every feature must pass this test: _"Is this something I do repetitively, multiple times a day, that currently involves tedious context-switching or command-line typing?"_ If yes, it belongs. If it's a nice-to-have used once a week, it probably doesn't.

The features in §4.1–4.8 pass that test. The following ideas are **real pain points** but are parked here for future consideration.

### 9.1 Future Scope: Remote Environment Manager (RDP / EC2)

#### The Pain

Managing several customer-replication environments across Windows RDP connections is another dimension of the same "deployment juggling" problem. High-value customers paying significant money need their environments replicated in EC2 instances (or similar). Today this means:

- A mental map (or a spreadsheet / sticky note) of which RDP host corresponds to which customer.
- Opening Remote Desktop Connection manually, typing hostnames, managing credentials.
- Repeating the same "connect → deploy services → verify" dance for each customer environment.
- Losing track of which EC2 instances are running, who they're for, and what version is deployed there.

This is clearly in the spirit of KExplorer — tedious, repetitive, context-switching-heavy work that a registry + point-and-click UI could eliminate.

#### What It Might Look Like

| Concept | Description |
|---|---|
| **Environment Registry** | A named list of remote environments: customer name, RDP hostname/IP, EC2 instance ID, credentials (stored securely), deployment profile (links to §4.7), notes. |
| **One-click RDP** | Right-click an environment → "Connect (RDP)" → launches `mstsc.exe` with the correct hostname and saved credentials. No more typing. |
| **EC2 integration** | Start/stop EC2 instances from KExplorer (via AWS CLI or SDK). See running state, uptime, cost estimate. |
| **Remote service control** | Extend the Service Registry (§4.7) to apply/update deployment profiles on remote machines over the network (`sc \\HOSTNAME`). |
| **Environment dashboard** | A tab showing all customer environments at a glance: which are running, which version is deployed, last connected, health status. |
| **AI integration** | _"spin up the Contoso environment and deploy the 2.3 hotfix"_ → starts EC2, waits for RDP-ready, applies service profile remotely. |

#### Why It's Future Scope (Not Now)

1. **Security complexity** — credential storage, network access, AWS IAM permissions. Gets serious fast.
2. **Scope creep risk** — this is closer to an "infrastructure management tool" than a "developer productivity tool." The line blurs.
3. **Dependencies** — needs the Service Registry (§4.7) and Docker Dashboard (§4.8) to be solid first, since remote deployment builds on top of them.
4. **The tool shouldn't try to be everything** — tools like AWS Console, Royal TS, mRemoteNG, and Terraform already exist for infrastructure management. KExplorer's value-add would be the _unified view_ tying customer → environment → deployment profile → services, not replacing those tools.

#### Decision Gate

Revisit this after Phase 2 (§6) is complete. If the Service Registry and Docker Dashboard prove their value for local development, extending them to remote environments is a natural next step. If the complexity feels like it's pulling the tool away from its core identity, leave it out and instead add a simple "bookmarks" panel for RDP connections (hostname + one-click launch) without the full infrastructure management layer.

---

*This is a living document. Update as decisions are made and phases are completed.*
