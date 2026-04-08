using System.Diagnostics;

namespace Kexplorer.Core.Network;

public static class NetworkInfoService
{
    public static async Task<List<NetworkConnection>> GetConnectionsAsync(CancellationToken cancellationToken = default)
    {
        var output = await RunNetstatAsync(cancellationToken);
        var connections = ParseNetstatOutput(output);
        ResolveProcessNames(connections);
        return connections;
    }

    private static async Task<string> RunNetstatAsync(CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output;
    }

    private static List<NetworkConnection> ParseNetstatOutput(string output)
    {
        var connections = new List<NetworkConnection>();
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("TCP", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("UDP", StringComparison.OrdinalIgnoreCase))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
                continue;

            var proto = parts[0];
            var local = parts[1];
            var foreign = parts[2];

            string state;
            int pid;

            if (proto.StartsWith("UDP", StringComparison.OrdinalIgnoreCase))
            {
                // UDP lines: Proto LocalAddr ForeignAddr PID
                state = "";
                if (!int.TryParse(parts.Length >= 4 ? parts[^1] : "0", out pid))
                    pid = 0;
            }
            else
            {
                // TCP lines: Proto LocalAddr ForeignAddr State PID
                state = parts.Length >= 5 ? parts[3] : "";
                if (!int.TryParse(parts[^1], out pid))
                    pid = 0;
            }

            var (localAddr, localPort) = ParseEndpoint(local);
            var (remoteAddr, remotePort) = ParseEndpoint(foreign);

            connections.Add(new NetworkConnection
            {
                Protocol = proto,
                LocalAddress = localAddr,
                LocalPort = localPort,
                RemoteAddress = remoteAddr,
                RemotePort = remotePort == 0 && foreign == "*:*" ? null : remotePort,
                State = state,
                Pid = pid
            });
        }

        return connections;
    }

    private static (string address, int port) ParseEndpoint(string endpoint)
    {
        if (string.IsNullOrEmpty(endpoint) || endpoint == "*:*")
            return ("*", 0);

        // Handle IPv6 bracketed addresses like [::1]:port
        if (endpoint.StartsWith('['))
        {
            var closeBracket = endpoint.LastIndexOf(']');
            if (closeBracket >= 0 && closeBracket + 1 < endpoint.Length && endpoint[closeBracket + 1] == ':')
            {
                var addr = endpoint[..(closeBracket + 1)];
                var portStr = endpoint[(closeBracket + 2)..];
                int.TryParse(portStr, out var port);
                return (addr, port);
            }
            return (endpoint, 0);
        }

        // IPv4: find last colon
        var lastColon = endpoint.LastIndexOf(':');
        if (lastColon > 0)
        {
            var addr = endpoint[..lastColon];
            var portStr = endpoint[(lastColon + 1)..];
            int.TryParse(portStr, out var port);
            return (addr, port);
        }

        return (endpoint, 0);
    }

    private static void ResolveProcessNames(List<NetworkConnection> connections)
    {
        // Cache PID -> (name, path) to avoid repeated lookups
        var cache = new Dictionary<int, (string name, string path)>();

        foreach (var conn in connections)
        {
            if (conn.Pid == 0)
            {
                conn.ProcessName = conn.Pid == 0 ? "System Idle" : "";
                continue;
            }

            if (!cache.TryGetValue(conn.Pid, out var info))
            {
                try
                {
                    using var proc = Process.GetProcessById(conn.Pid);
                    var name = proc.ProcessName;
                    string path;
                    try
                    {
                        path = proc.MainModule?.FileName ?? "";
                    }
                    catch
                    {
                        // Access denied for system/elevated processes
                        path = "";
                    }
                    info = (name, path);
                }
                catch
                {
                    // Process may have exited
                    info = ($"[PID {conn.Pid}]", "");
                }
                cache[conn.Pid] = info;
            }

            conn.ProcessName = info.name;
            conn.ProcessPath = info.path;
        }
    }
}
