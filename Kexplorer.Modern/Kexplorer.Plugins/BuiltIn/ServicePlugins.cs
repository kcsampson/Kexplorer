using System.Diagnostics;
using System.ServiceProcess;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Work;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Start selected Windows services. Port of legacy StartServiceScript.
/// </summary>
[ServiceContext]
public sealed class StartServicePlugin : IServicePlugin
{
    public string Name => "Start";
    public string Description => "Start the selected services";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;
    private bool _needsRunAs;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<ServiceInfo> selectedServices, IPluginContext context, CancellationToken cancellationToken = default)
    {
        foreach (var svc in selectedServices.Where(s => s.Status == ServiceRunningStatus.Stopped))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_needsRunAs)
            {
                try
                {
                    using var sc = new ServiceController(svc.ServiceName, svc.MachineName);
                    sc.Start();
                }
                catch
                {
                    _needsRunAs = true;
                }
            }

            if (_needsRunAs)
            {
                context.RunProgram("net", $"start {svc.ServiceName}", null, asAdmin: true);
            }
        }

        await Task.Delay(500, cancellationToken);
        await RefreshSelectedServices(selectedServices, context, cancellationToken);
    }

    private static async Task RefreshSelectedServices(IReadOnlyList<ServiceInfo> services, IPluginContext context, CancellationToken cancellationToken)
    {
        var refreshed = ServiceStatusHelper.QueryCurrentStatus(services);
        if (context.Shell is IServiceShell serviceShell)
        {
            await serviceShell.RefreshServiceStatusAsync(refreshed, cancellationToken);
        }
    }
}

/// <summary>
/// Stop selected Windows services. Port of legacy StopServiceScript.
/// </summary>
[ServiceContext]
public sealed class StopServicePlugin : IServicePlugin
{
    public string Name => "Stop";
    public string Description => "Stop the selected services";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;
    private bool _needsRunAs;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<ServiceInfo> selectedServices, IPluginContext context, CancellationToken cancellationToken = default)
    {
        foreach (var svc in selectedServices.Where(s => s.CanStop))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_needsRunAs)
            {
                try
                {
                    using var sc = new ServiceController(svc.ServiceName, svc.MachineName);
                    sc.Stop();
                }
                catch
                {
                    _needsRunAs = true;
                }
            }

            if (_needsRunAs)
            {
                context.RunProgram("net", $"stop {svc.ServiceName}", null, asAdmin: true);
            }
        }

        await Task.Delay(500, cancellationToken);
        var refreshed = ServiceStatusHelper.QueryCurrentStatus(selectedServices);
        if (context.Shell is IServiceShell serviceShell)
        {
            await serviceShell.RefreshServiceStatusAsync(refreshed, cancellationToken);
        }
    }
}

/// <summary>
/// Restart selected Windows services. Port of legacy RestartServiceScript.
/// </summary>
[ServiceContext]
public sealed class RestartServicePlugin : IServicePlugin
{
    public string Name => "Restart";
    public string Description => "Restart the selected services";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;
    private bool _needsRunAs;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<ServiceInfo> selectedServices, IPluginContext context, CancellationToken cancellationToken = default)
    {
        foreach (var svc in selectedServices.Where(s => s.CanStop))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_needsRunAs)
            {
                try
                {
                    using var sc = new ServiceController(svc.ServiceName, svc.MachineName);
                    sc.Stop();
                    sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                }
                catch
                {
                    _needsRunAs = true;
                }
            }

            if (_needsRunAs)
            {
                context.RunProgram("net", $"stop {svc.ServiceName}", null, asAdmin: true);
                using var sc2 = new ServiceController(svc.ServiceName, svc.MachineName);
                sc2.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                await Task.Delay(500, cancellationToken);
                context.RunProgram("net", $"start {svc.ServiceName}", null, asAdmin: true);
                sc2.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
            }
        }

        var refreshed = ServiceStatusHelper.QueryCurrentStatus(selectedServices);
        if (context.Shell is IServiceShell serviceShell)
        {
            await serviceShell.RefreshServiceStatusAsync(refreshed, cancellationToken);
        }
    }
}

/// <summary>
/// Refresh the services list. Port of legacy RefreshServicesScript.
/// </summary>
[ServiceContext]
public sealed class RefreshServicesPlugin : IServicePlugin
{
    public string Name => "Refresh";
    public string Description => "Refresh the service list";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => new(PluginKey.F5);

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<ServiceInfo> selectedServices, IPluginContext context, CancellationToken cancellationToken = default)
    {
        List<string>? visibleServices = null;
        string? machineName = null;
        string? searchPattern = null;

        if (context.Shell is IServiceShell serviceShell)
        {
            visibleServices = serviceShell.VisibleServices;
            machineName = serviceShell.MachineName;
            searchPattern = serviceShell.SearchPattern;
        }

        await context.WorkQueue.EnqueueAsync(
            new ServiceLoaderWorkItem(visibleServices, machineName, searchPattern), cancellationToken);
    }
}

/// <summary>
/// Hide selected services from the grid. Port of legacy HideServiceScript.
/// </summary>
[ServiceContext]
public sealed class HideServicePlugin : IServicePlugin
{
    public string Name => "Hide from View";
    public string Description => "Remove selected services from the visible list";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<ServiceInfo> selectedServices, IPluginContext context, CancellationToken cancellationToken = default)
    {
        if (context.Shell is IHybridServiceShell hybridShell)
        {
            await hybridShell.RemoveServicesAsync(selectedServices, cancellationToken);
        }

        var names = string.Join(", ", selectedServices.Select(s => s.DisplayName));
        await context.Shell.ReportStatusAsync($"Hidden: {names}", cancellationToken);
    }
}

/// <summary>
/// Helper to re-query current status for a set of services without reloading the full list.
/// </summary>
internal static class ServiceStatusHelper
{
    public static List<ServiceInfo> QueryCurrentStatus(IReadOnlyList<ServiceInfo> services)
    {
        var result = new List<ServiceInfo>(services.Count);
        foreach (var svc in services)
        {
            try
            {
                using var sc = new ServiceController(svc.ServiceName, svc.MachineName);
                result.Add(new ServiceInfo
                {
                    ServiceName = sc.ServiceName,
                    DisplayName = sc.DisplayName,
                    MachineName = sc.MachineName,
                    Status = MapStatus(sc.Status),
                    ServiceType = sc.ServiceType.ToString(),
                    CanStop = TryGetBool(() => sc.CanStop),
                    CanPauseAndContinue = TryGetBool(() => sc.CanPauseAndContinue),
                    CanShutdown = TryGetBool(() => sc.CanShutdown)
                });
            }
            catch
            {
                result.Add(svc);
            }
        }
        return result;
    }

    private static ServiceRunningStatus MapStatus(ServiceControllerStatus status) => status switch
    {
        ServiceControllerStatus.Stopped => ServiceRunningStatus.Stopped,
        ServiceControllerStatus.StartPending => ServiceRunningStatus.StartPending,
        ServiceControllerStatus.StopPending => ServiceRunningStatus.StopPending,
        ServiceControllerStatus.Running => ServiceRunningStatus.Running,
        ServiceControllerStatus.ContinuePending => ServiceRunningStatus.ContinuePending,
        ServiceControllerStatus.PausePending => ServiceRunningStatus.PausePending,
        ServiceControllerStatus.Paused => ServiceRunningStatus.Paused,
        _ => ServiceRunningStatus.Unknown
    };

    private static bool TryGetBool(Func<bool> getter)
    {
        try { return getter(); }
        catch { return false; }
    }
}
