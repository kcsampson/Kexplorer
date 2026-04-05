using Kexplorer.Core.Plugins;
using Kexplorer.Core.Work;

namespace Kexplorer.Plugins.BuiltIn;

/// <summary>
/// Resolves logs for enable2020-* services.
/// Log folder = {-logDir}\{-componentName}\ → most recent .log file.
/// </summary>
public sealed class Enable2020LogResolver : IServiceLogResolver
{
    public bool CanResolve(ServiceInfo service, string exePath, Dictionary<string, string> binPathFlags)
    {
        var exeName = Path.GetFileName(exePath);
        return exeName.StartsWith("enable2020-", StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> ResolveLogPaths(ServiceInfo service, string exePath, Dictionary<string, string> binPathFlags)
    {
        if (!binPathFlags.TryGetValue("logDir", out var logDir) ||
            !binPathFlags.TryGetValue("componentName", out var componentName))
            return Array.Empty<string>();

        var logFolder = Path.Combine(logDir, componentName);
        if (!Directory.Exists(logFolder))
            return Array.Empty<string>();

        // Find most recent .log file
        var mostRecent = Directory.GetFiles(logFolder, "*.log")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .FirstOrDefault();

        return mostRecent is not null ? new[] { mostRecent.FullName } : Array.Empty<string>();
    }
}

/// <summary>
/// Resolves logs for ew-graphql-mcp.exe.
/// Log file = {exe directory}\logs\ew-graphql-mcp.log
/// </summary>
public sealed class EwGraphqlMcpLogResolver : IServiceLogResolver
{
    public bool CanResolve(ServiceInfo service, string exePath, Dictionary<string, string> binPathFlags)
    {
        var exeName = Path.GetFileName(exePath);
        return string.Equals(exeName, "ew-graphql-mcp.exe", StringComparison.OrdinalIgnoreCase);
    }

    public IReadOnlyList<string> ResolveLogPaths(ServiceInfo service, string exePath, Dictionary<string, string> binPathFlags)
    {
        var exeDir = Path.GetDirectoryName(exePath);
        if (string.IsNullOrEmpty(exeDir))
            return Array.Empty<string>();

        var logPath = Path.Combine(exeDir, "logs", "ew-graphql-mcp.log");
        return File.Exists(logPath) ? new[] { logPath } : Array.Empty<string>();
    }
}
