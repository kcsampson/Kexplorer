using Kexplorer.Core.Plugins;
using Kexplorer.Core.Shell;

namespace Kexplorer.Core.Work;

/// <summary>
/// Extension of IServiceShell with Docker container callbacks.
/// </summary>
public interface IHybridServiceShell : IServiceShell
{
    Task SetDockerContainerListAsync(List<DockerContainerInfo> containers, CancellationToken cancellationToken = default);
    Task SetDockerStatusAsync(string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove the specified services from the visible list ("Hide from View").
    /// </summary>
    Task RemoveServicesAsync(IReadOnlyList<ServiceInfo> services, CancellationToken cancellationToken = default);

    /// <summary>
    /// Display log content for a Windows service in the log viewer panel.
    /// </summary>
    Task ShowServiceLogsAsync(string serviceName, string logPath, string logContent, CancellationToken cancellationToken = default);
}
