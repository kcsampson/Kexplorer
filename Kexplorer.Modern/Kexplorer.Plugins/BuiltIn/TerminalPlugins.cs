using System.Diagnostics;
using System.Runtime.InteropServices;
using Kexplorer.Core.FileSystem;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Helper for Windows Terminal detection and WSL path translation.
/// </summary>
public static class WindowsTerminalHelper
{
    private static bool? _wtAvailable;

    /// <summary>
    /// Check whether wt.exe is available on this system.
    /// Result is cached after the first call.
    /// </summary>
    public static bool IsWindowsTerminalAvailable()
    {
        if (_wtAvailable.HasValue) return _wtAvailable.Value;

        // Check the standard WindowsApps location
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var wtPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "wt.exe");
        _wtAvailable = File.Exists(wtPath);
        return _wtAvailable.Value;
    }

    /// <summary>
    /// Convert a Windows path to a WSL /mnt/ path.
    /// E.g. C:\Users\dev → /mnt/c/Users/dev
    /// </summary>
    public static string ToWslPath(string windowsPath)
    {
        if (string.IsNullOrEmpty(windowsPath) || windowsPath.Length < 2 || windowsPath[1] != ':')
            return windowsPath;

        var driveLetter = char.ToLowerInvariant(windowsPath[0]);
        var rest = windowsPath.Substring(2).Replace('\\', '/');
        return $"/mnt/{driveLetter}{rest}";
    }

    /// <summary>
    /// Detect whether any running Windows Terminal instance is elevated (admin).
    /// When Kexplorer is non-elevated and the terminal is elevated, wt -w 0 cannot
    /// attach a tab to the elevated window (UIPI). Launching wt elevated (runas)
    /// lets the new tab join the elevated instance.
    /// </summary>
    public static bool IsElevatedTerminalRunning()
    {
        try
        {
            var terminals = Process.GetProcessesByName("WindowsTerminal");
            foreach (var proc in terminals)
            {
                try
                {
                    // If we can read the process handle, it's at our level or below.
                    // If OpenProcess / handle access throws, the process is elevated
                    // and we are not — meaning there's an elevation mismatch.
                    _ = proc.Handle;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // Access denied → the terminal process is elevated
                    return true;
                }
                catch (InvalidOperationException)
                {
                    // Process already exited
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch
        {
            // If we can't enumerate processes at all, assume non-elevated
        }
        return false;
    }
}

/// <summary>
/// Opens a new tab in Windows Terminal (default profile) at the current folder.
/// Uses -w 0 to target the MRU window instead of creating a new window.
/// Falls back to standalone PowerShell if Windows Terminal is not installed.
/// </summary>
[FolderContext]
public sealed class OpenTerminalPlugin : IFolderPlugin, IMenuGroupPlugin
{
    public string Name => "Open Terminal Here";
    public string Description => "Open a new terminal tab at the current folder (default profile)";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;
    public string MenuGroup => "Open Terminal Here";

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (WindowsTerminalHelper.IsWindowsTerminalAvailable())
        {
            var elevated = WindowsTerminalHelper.IsElevatedTerminalRunning();
            context.RunProgram("wt", $"-w 0 new-tab -d \"{folderPath}\"", asAdmin: elevated);
        }
        else
        {
            context.RunProgram("powershell", $"-NoExit -Command \"Set-Location '{folderPath}'\"");
            _ = context.Shell.ReportStatusAsync("Windows Terminal not found — falling back to PowerShell.");
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Opens a new PowerShell tab in Windows Terminal at the current folder.
/// </summary>
[FolderContext]
public sealed class OpenTerminalPowerShellPlugin : IFolderPlugin, IMenuGroupPlugin
{
    public string Name => "Open Terminal Here — PowerShell";
    public string Description => "Open a PowerShell tab in Windows Terminal at the current folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;
    public string MenuGroup => "Open Terminal Here";

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsValidForFolder(string folderPath) => WindowsTerminalHelper.IsWindowsTerminalAvailable();

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var elevated = WindowsTerminalHelper.IsElevatedTerminalRunning();
        context.RunProgram("wt", $"-w 0 new-tab -p \"Windows PowerShell\" -d \"{folderPath}\"", asAdmin: elevated);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Opens a new Command Prompt tab in Windows Terminal at the current folder.
/// </summary>
[FolderContext]
public sealed class OpenTerminalCmdPlugin : IFolderPlugin, IMenuGroupPlugin
{
    public string Name => "Open Terminal Here — Command Prompt";
    public string Description => "Open a Command Prompt tab in Windows Terminal at the current folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;
    public string MenuGroup => "Open Terminal Here";

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsValidForFolder(string folderPath) => WindowsTerminalHelper.IsWindowsTerminalAvailable();

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var elevated = WindowsTerminalHelper.IsElevatedTerminalRunning();
        context.RunProgram("wt", $"-w 0 new-tab -p \"Command Prompt\" -d \"{folderPath}\"", asAdmin: elevated);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Opens a new Ubuntu (WSL) tab in Windows Terminal at the current folder.
/// Translates the Windows path to a WSL /mnt/ path.
/// </summary>
[FolderContext]
public sealed class OpenTerminalWslPlugin : IFolderPlugin, IMenuGroupPlugin
{
    public string Name => "Open Terminal Here — Ubuntu (WSL)";
    public string Description => "Open an Ubuntu (WSL) tab in Windows Terminal at the current folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;
    public string MenuGroup => "Open Terminal Here";

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsValidForFolder(string folderPath) => WindowsTerminalHelper.IsWindowsTerminalAvailable();

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var wslPath = WindowsTerminalHelper.ToWslPath(folderPath);
        var elevated = WindowsTerminalHelper.IsElevatedTerminalRunning();
        context.RunProgram("wt", $"-w 0 new-tab -p \"Ubuntu\" -d \"{wslPath}\"", asAdmin: elevated);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Opens Windows File Explorer with the navigation pane tree expanded to the specified folder.
/// Ensures the "Expand to open folder" Explorer setting is enabled (via registry) so
/// that the left-hand tree (This PC > C: > ...) is fully expanded to the target folder.
/// Uses ShellExecute with the "explore" verb which tells Windows to open the folder
/// with the tree view expanded.
/// </summary>
[FolderContext]
public sealed class OpenFileExplorerHerePlugin : IFolderPlugin
{
    public string Name => "Open File Explorer Here";
    public string Description => "Open Windows File Explorer at this folder";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    private const string ExplorerAdvancedKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ExpandValue = "NavPaneExpandToCurrentFolder";

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task ExecuteAsync(string folderPath, IPluginContext context, CancellationToken cancellationToken = default)
    {
        // Ensure "Expand to open folder" is enabled so the nav pane tree expands
        EnsureNavPaneExpandEnabled();

        // Use the "explore" verb via ShellExecute — this tells Windows to open
        // the folder with the Explorer tree pane expanded to the location
        Process.Start(new ProcessStartInfo
        {
            FileName = folderPath,
            Verb = "explore",
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }

    private static void EnsureNavPaneExpandEnabled()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ExplorerAdvancedKey, writable: true);
            if (key is null) return;
            var current = key.GetValue(ExpandValue);
            if (current is not int val || val != 1)
            {
                key.SetValue(ExpandValue, 1, Microsoft.Win32.RegistryValueKind.DWord);
            }
        }
        catch
        {
            // Silently ignore if registry access fails
        }
    }
}
