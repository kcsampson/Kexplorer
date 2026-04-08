namespace Kexplorer.Core.Network;

public sealed class NetworkConnection
{
    public string Protocol { get; set; } = "";
    public string LocalAddress { get; set; } = "";
    public int LocalPort { get; set; }
    public string RemoteAddress { get; set; } = "";
    public int? RemotePort { get; set; }
    public string State { get; set; } = "";
    public int Pid { get; set; }
    public string ProcessName { get; set; } = "";
    public string ProcessPath { get; set; } = "";

    public string LocalEndpoint => $"{LocalAddress}:{LocalPort}";
    public string RemoteEndpoint => RemotePort.HasValue ? $"{RemoteAddress}:{RemotePort}" : RemoteAddress;
}
