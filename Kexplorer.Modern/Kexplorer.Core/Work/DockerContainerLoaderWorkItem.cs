using Kexplorer.Core.Docker;
using Kexplorer.Core.Shell;

namespace Kexplorer.Core.Work;

/// <summary>
/// Loads Docker containers via WSL docker commands.
/// Follows ServiceLoaderWorkItem pattern.
/// </summary>
public sealed class DockerContainerLoaderWorkItem : IWorkItem
{
    private readonly WslDockerService _dockerService;

    public DockerContainerLoaderWorkItem(WslDockerService? dockerService = null)
    {
        _dockerService = dockerService ?? new WslDockerService();
    }

    public string Name => "LoadDockerContainers";

    public async Task ExecuteAsync(IKexplorerShell shell, CancellationToken cancellationToken)
    {
        if (shell is not IHybridServiceShell hybridShell)
            return;

        await shell.ReportStatusAsync("Checking Docker availability...", cancellationToken);

        var (available, message) = await _dockerService.CheckAvailabilityAsync(cancellationToken);

        if (!available)
        {
            await hybridShell.SetDockerStatusAsync($"Docker not available: {message}", cancellationToken);
            await shell.ReportStatusAsync("Docker not available.", cancellationToken);
            return;
        }

        await hybridShell.SetDockerStatusAsync("Loading containers...", cancellationToken);
        await shell.ReportStatusAsync("Loading Docker containers...", cancellationToken);

        var containers = await _dockerService.ListContainersAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        await hybridShell.SetDockerContainerListAsync(containers, cancellationToken);
        await hybridShell.SetDockerStatusAsync($"Docker: {containers.Count} containers", cancellationToken);
        await shell.ReportStatusAsync($"Loaded {containers.Count} Docker containers.", cancellationToken);
    }
}
