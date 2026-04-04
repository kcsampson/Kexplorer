namespace Kexplorer.Core.Plugins;

/// <summary>
/// Lightweight service info model passed to service plugins.
/// Decouples plugins from System.ServiceProcess.ServiceController.
/// </summary>
public sealed class ServiceInfo
{
    public string ServiceName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string MachineName { get; set; } = ".";
    public ServiceRunningStatus Status { get; set; }
    public string ServiceType { get; set; } = "";
    public bool CanStop { get; set; }
    public bool CanPauseAndContinue { get; set; }
    public bool CanShutdown { get; set; }
}

public enum ServiceRunningStatus
{
    Unknown,
    Stopped,
    StartPending,
    StopPending,
    Running,
    ContinuePending,
    PausePending,
    Paused
}
