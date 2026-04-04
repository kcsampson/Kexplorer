namespace Kexplorer.Core.Plugins;

/// <summary>
/// Lightweight Docker container info model passed to Docker plugins.
/// Mirrors ServiceInfo pattern for Docker containers.
/// </summary>
public sealed class DockerContainerInfo
{
    public string ContainerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Image { get; set; } = "";
    public string Status { get; set; } = "";
    public string Ports { get; set; } = "";
    public string GPUs { get; set; } = "";
    public int MountCount { get; set; }
    public string Network { get; set; } = "";
    public string Created { get; set; } = "";
}
