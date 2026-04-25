using System.ServiceProcess;
using System.Text.RegularExpressions;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Shell;

namespace Kexplorer.Core.Work;

/// <summary>
/// Loads Windows services into the service grid.
/// Replaces legacy ServiceMgrWorkUnit's DoJob() method.
/// The UI binding is done via IKexplorerShell callbacks — no direct DataGrid reference.
/// </summary>
public sealed class ServiceLoaderWorkItem : IWorkItem
{
    private readonly List<string>? _visibleServices;
    private readonly string _machineName;
    private readonly string? _searchPattern;

    public ServiceLoaderWorkItem(List<string>? visibleServices = null, string? machineName = null, string? searchPattern = null)
    {
        _visibleServices = visibleServices;
        _machineName = string.IsNullOrEmpty(machineName) ? "." : machineName;
        _searchPattern = searchPattern;
    }

    public string Name => $"LoadServices({_machineName})";

    public async Task ExecuteAsync(IKexplorerShell shell, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await shell.ReportStatusAsync($"Loading services from {_machineName}...", cancellationToken);

        var services = LoadServices(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // Use a service-specific shell callback if available
        if (shell is IServiceShell serviceShell)
        {
            await serviceShell.SetServiceListAsync(services, cancellationToken);
        }

        await shell.ReportStatusAsync($"Loaded {services.Count} services from {_machineName}.", cancellationToken);
    }

    private List<ServiceInfo> LoadServices(CancellationToken cancellationToken)
    {
        var result = new List<ServiceInfo>();

        if (_visibleServices is null or { Count: 0 })
        {
            // Load all services from the machine, optionally filtered by pattern
            var controllers = ServiceController.GetServices(_machineName)
                .Where(sc => string.IsNullOrEmpty(_searchPattern)
                             || Regex.IsMatch(sc.DisplayName, _searchPattern, RegexOptions.IgnoreCase))
                .OrderBy(s => s.ServiceName);

            foreach (var sc in controllers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(MapToServiceInfo(sc));
            }
        }
        else
        {
            // Load only the named services
            var parsed = _visibleServices
                .Select(s =>
                {
                    var idx = s.IndexOf("//", StringComparison.Ordinal);
                    return idx >= 0
                        ? (DisplayName: s[..idx], Machine: s[(idx + 2)..])
                        : (DisplayName: s, Machine: ".");
                })
                .Distinct()
                .ToList();

            var machines = parsed.Select(p => p.Machine).Distinct();

            foreach (var machine in machines)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var machineServices = parsed.Where(p => p.Machine == machine).Select(p => p.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var controllers = ServiceController.GetServices(machine)
                    .Where(sc => machineServices.Contains(sc.DisplayName));

                foreach (var sc in controllers)
                {
                    result.Add(MapToServiceInfo(sc));
                }
            }
        }

        return result;
    }

    private static ServiceInfo MapToServiceInfo(ServiceController sc)
    {
        return new ServiceInfo
        {
            ServiceName = sc.ServiceName,
            DisplayName = sc.DisplayName,
            MachineName = sc.MachineName,
            Status = MapStatus(sc.Status),
            ServiceType = sc.ServiceType.ToString(),
            CanStop = TryGetBool(() => sc.CanStop),
            CanPauseAndContinue = TryGetBool(() => sc.CanPauseAndContinue),
            CanShutdown = TryGetBool(() => sc.CanShutdown)
        };
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

/// <summary>
/// Extension of IKexplorerShell for service-specific callbacks.
/// </summary>
public interface IServiceShell : IKexplorerShell
{
    Task SetServiceListAsync(IReadOnlyList<ServiceInfo> services, CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh the status of specific services in-place without reloading the full list.
    /// </summary>
    Task RefreshServiceStatusAsync(IReadOnlyList<ServiceInfo> services, CancellationToken cancellationToken = default);

    /// <summary>The visible-services filter passed at initialization (null = all).</summary>
    List<string>? VisibleServices { get; }

    /// <summary>The target machine name (defaults to ".").</summary>
    string MachineName { get; }

    /// <summary>The search pattern filter (null = none).</summary>
    string? SearchPattern { get; }
}
