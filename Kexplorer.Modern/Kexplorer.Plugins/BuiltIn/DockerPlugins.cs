using Kexplorer.Core.Docker;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Work;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Start selected Docker containers.
/// </summary>
[DockerContext]
public sealed class StartContainerPlugin : IDockerPlugin
{
    public string Name => "Start";
    public string Description => "Start the selected containers";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<DockerContainerInfo> selectedContainers, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var docker = new WslDockerService();
        foreach (var container in selectedContainers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (success, output) = await docker.StartAsync(container.Name, cancellationToken);
            await context.Shell.ReportStatusAsync(
                success ? $"Started: {container.Name}" : $"Failed to start {container.Name}: {output}",
                cancellationToken);
        }

        await ReloadContainers(context, cancellationToken);
    }

    private static async Task ReloadContainers(IPluginContext context, CancellationToken cancellationToken)
    {
        await context.WorkQueue.EnqueueAsync(new DockerContainerLoaderWorkItem(), cancellationToken);
    }
}

/// <summary>
/// Stop selected Docker containers.
/// </summary>
[DockerContext]
public sealed class StopContainerPlugin : IDockerPlugin
{
    public string Name => "Stop";
    public string Description => "Stop the selected containers";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<DockerContainerInfo> selectedContainers, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var docker = new WslDockerService();
        foreach (var container in selectedContainers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (success, output) = await docker.StopAsync(container.Name, cancellationToken);
            await context.Shell.ReportStatusAsync(
                success ? $"Stopped: {container.Name}" : $"Failed to stop {container.Name}: {output}",
                cancellationToken);
        }

        await context.WorkQueue.EnqueueAsync(new DockerContainerLoaderWorkItem(), cancellationToken);
    }
}

/// <summary>
/// Restart selected Docker containers.
/// </summary>
[DockerContext]
public sealed class RestartContainerPlugin : IDockerPlugin
{
    public string Name => "Restart";
    public string Description => "Restart the selected containers";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<DockerContainerInfo> selectedContainers, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var docker = new WslDockerService();
        foreach (var container in selectedContainers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (success, output) = await docker.RestartAsync(container.Name, cancellationToken);
            await context.Shell.ReportStatusAsync(
                success ? $"Restarted: {container.Name}" : $"Failed to restart {container.Name}: {output}",
                cancellationToken);
        }

        await context.WorkQueue.EnqueueAsync(new DockerContainerLoaderWorkItem(), cancellationToken);
    }
}

/// <summary>
/// Remove selected Docker containers (with confirmation).
/// </summary>
[DockerContext]
public sealed class RemoveContainerPlugin : IDockerPlugin
{
    public string Name => "Remove";
    public string Description => "Remove the selected containers";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<DockerContainerInfo> selectedContainers, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var names = string.Join(", ", selectedContainers.Select(c => c.Name));
        var confirmed = await context.ConfirmAsync("Remove Containers",
            $"Remove {selectedContainers.Count} container(s)?\n{names}", cancellationToken);

        if (!confirmed) return;

        var docker = new WslDockerService();
        foreach (var container in selectedContainers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (success, output) = await docker.RemoveAsync(container.Name, cancellationToken);
            await context.Shell.ReportStatusAsync(
                success ? $"Removed: {container.Name}" : $"Failed to remove {container.Name}: {output}",
                cancellationToken);
        }

        await context.WorkQueue.EnqueueAsync(new DockerContainerLoaderWorkItem(), cancellationToken);
    }
}

/// <summary>
/// Refresh the Docker container list.
/// </summary>
[DockerContext]
public sealed class RefreshDockerPlugin : IDockerPlugin
{
    public string Name => "Refresh";
    public string Description => "Refresh the Docker container list";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => new(PluginKey.F5);

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<DockerContainerInfo> selectedContainers, IPluginContext context, CancellationToken cancellationToken = default)
    {
        await context.WorkQueue.EnqueueAsync(new DockerContainerLoaderWorkItem(), cancellationToken);
    }
}
