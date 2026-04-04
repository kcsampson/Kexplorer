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
        await ReloadServices(context, cancellationToken);
    }

    private static async Task ReloadServices(IPluginContext context, CancellationToken cancellationToken)
    {
        if (context.Shell is IServiceShell serviceShell)
        {
            await context.WorkQueue.EnqueueAsync(new ServiceLoaderWorkItem(), cancellationToken);
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
        await context.WorkQueue.EnqueueAsync(new ServiceLoaderWorkItem(), cancellationToken);
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

        await context.WorkQueue.EnqueueAsync(new ServiceLoaderWorkItem(), cancellationToken);
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
        await context.WorkQueue.EnqueueAsync(new ServiceLoaderWorkItem(), cancellationToken);
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
        // This is handled by the ServicesPanel directly — remove selected items from the observable collection.
        // The plugin signals the shell to handle it.
        foreach (var svc in selectedServices)
        {
            await context.Shell.ReportStatusAsync($"Hidden: {svc.DisplayName}", cancellationToken);
        }
    }
}
