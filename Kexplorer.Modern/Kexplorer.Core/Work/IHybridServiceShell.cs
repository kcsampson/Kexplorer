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
}
