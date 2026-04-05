using System.Diagnostics;
using Kexplorer.Core.Plugins;
using Kexplorer.Core.Work;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// View Logs for Windows services. Resolves log file location from binPath,
/// reads the last 500 lines, and displays in the shell's log viewer.
/// </summary>
[ServiceContext]
public sealed class ViewLogsPlugin : IServicePlugin
{
    public string Name => "View Logs";
    public string Description => "View the log file for the selected service";
    public bool IsActive => true;
    public PluginShortcut? Shortcut => null;

    private static readonly IServiceLogResolver[] Resolvers =
    {
        new Enable2020LogResolver(),
        new EwGraphqlMcpLogResolver()
    };

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task ExecuteAsync(IReadOnlyList<ServiceInfo> selectedServices, IPluginContext context, CancellationToken cancellationToken = default)
    {
        var service = selectedServices.FirstOrDefault();
        if (service is null) return;

        // Query binPath
        var binPath = await Task.Run(() => QueryServiceBinPath(service.ServiceName, service.MachineName), cancellationToken);
        if (string.IsNullOrWhiteSpace(binPath))
        {
            await context.Shell.ReportStatusAsync($"No binPath available for {service.DisplayName}", cancellationToken);
            return;
        }

        var (exePath, flags) = BinPathParser.Parse(binPath);

        // Try each resolver
        foreach (var resolver in Resolvers)
        {
            if (!resolver.CanResolve(service, exePath, flags))
                continue;

            var logPaths = resolver.ResolveLogPaths(service, exePath, flags);
            if (logPaths.Count == 0)
            {
                await context.Shell.ReportStatusAsync($"Log file not found for {service.DisplayName}", cancellationToken);
                return;
            }

            var logPath = logPaths[0];
            var logContent = await Task.Run(() => ReadTail(logPath, 500), cancellationToken);

            if (context.Shell is IHybridServiceShell hybridShell)
            {
                await hybridShell.ShowServiceLogsAsync(service.DisplayName, logPath, logContent, cancellationToken);
            }
            else
            {
                await context.Shell.ReportStatusAsync($"Log: {logPath}", cancellationToken);
            }
            return;
        }

        await context.Shell.ReportStatusAsync($"No log location configured for this service type.", cancellationToken);
    }

    private static string ReadTail(string filePath, int lineCount)
    {
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs);
            var lines = new List<string>();
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line is not null)
                    lines.Add(line);
            }

            var start = Math.Max(0, lines.Count - lineCount);
            return string.Join(Environment.NewLine, lines.Skip(start));
        }
        catch (Exception ex)
        {
            return $"(Could not read log file: {ex.Message})";
        }
    }

    private static string QueryServiceBinPath(string serviceName, string machineName)
    {
        var args = machineName is "." or ""
            ? $"qc \"{serviceName}\""
            : $"\\\\{machineName} qc \"{serviceName}\"";

        var psi = new ProcessStartInfo
        {
            FileName = "sc",
            Arguments = args,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process is null) return "";

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);

        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("BINARY_PATH_NAME", StringComparison.OrdinalIgnoreCase))
            {
                var colonIdx = trimmed.IndexOf(':');
                if (colonIdx >= 0)
                    return trimmed[(colonIdx + 1)..].Trim();
            }
        }

        return "";
    }
}
