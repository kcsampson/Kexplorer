# Kexplorer.Modern

Kexplorer.Modern is the new generation of Kexplorer, introduced in the first half of 2026.

It keeps the original productivity-first explorer workflow while modernizing the architecture, UI, and hybrid environment support for developers who work across Windows and Linux tooling.

![Kexplorer screenshot](../kexplorer-screenshot.png)

## Highlights

- Tighter integration with WSL and Docker for mixed-environment workflows
- Services experience split into dedicated views, including Docker container status
- Completed terminal session tabs, including PowerShell, Cmd, and Linux Bash
- Multi-tab explorer experience with async work queues and responsive UI

## What Is New vs Legacy

- Core rewritten around async work items and queue-based execution
- Modern WPF shell with tabbed explorer, services, and terminal workflows
- Launcher and session state moved to JSON-backed configuration
- Plugin capability model with file, folder, service, and global contexts

## Hybrid Environment Support

Kexplorer.Modern is designed for developers that move across local Windows tools and Linux/containerized workloads in the same session.

- WSL-aware navigation and path handling
- Docker-aware status surfaces in the services area
- Windows service management retained and modernized
- Terminal sessions across PowerShell, Cmd, and Linux Bash

## Key Panels and Workflows

- Explorer panel: tree + file grid with lazy loading and explicit refresh
- Services + Docker status: operational visibility and quick control surface
- Terminal sessions: open shell contexts without leaving the app
- Persistent tabs and window state restored at startup

## .NET Requirements

- .NET 8 SDK (required to build)
- `net8.0-windows` support for `Kexplorer.UI`
- Windows 10 or later for the WPF desktop application

Current project target frameworks:

- `Kexplorer.Core`: `net8.0`
- `Kexplorer.Plugins`: `net8.0`
- `Kexplorer.MCP`: `net8.0`
- `Kexplorer.AI`: `net8.0`
- `Kexplorer.Core.Tests`: `net8.0`
- `Kexplorer.UI`: `net8.0-windows`

Recommended environment:

- .NET SDK 8.0.419 or later
- Windows Terminal (optional, improves terminal integration)
- WSL 2 (optional, for Linux filesystem and shell workflows)
- Docker Desktop (optional, for container status integration)

## Quick Start

```powershell
cd Kexplorer.Modern
dotnet build Kexplorer.Modern.sln
dotnet run --project Kexplorer.UI
```

## Build, Test, and Publish

```powershell
cd Kexplorer.Modern
dotnet build Kexplorer.Modern.sln
dotnet test Kexplorer.Core.Tests
dotnet publish Kexplorer.Modern.sln
```

## Status Snapshot

- Phase 1 complete: core explorer, plugins, services, shell contracts, state persistence
- Phase 2 in progress: expanded service registry and Docker dashboard depth
- Phase 3 planned: AI command panel and MCP integrations

## Additional Documentation

- UI details: `Kexplorer.UI/README.md`
- Migration status: `MIGRATION_NOTES.md`
- Project specification: `../specifications/01-Modern-Refresh.md`
