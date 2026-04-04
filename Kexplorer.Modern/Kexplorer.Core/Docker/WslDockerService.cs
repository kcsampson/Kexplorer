using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Kexplorer.Core.Plugins;

namespace Kexplorer.Core.Docker;

/// <summary>
/// Encapsulates all wsl docker command execution.
/// Handles graceful degradation when wsl or docker is not available.
/// </summary>
public sealed class WslDockerService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<DockerContainerInfo>> ListContainersAsync(CancellationToken cancellationToken = default)
    {
        var (exitCode, output, error) = await RunWslDockerAsync(
            "ps --all --no-trunc --format \"{{json .}}\"", cancellationToken);

        if (exitCode != 0)
            return new List<DockerContainerInfo>();

        var containers = new List<DockerContainerInfo>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var json = JsonDocument.Parse(line.Trim());
                var root = json.RootElement;

                containers.Add(new DockerContainerInfo
                {
                    ContainerId = GetString(root, "ID"),
                    Name = GetString(root, "Names"),
                    Image = GetString(root, "Image"),
                    Status = GetString(root, "Status"),
                    Ports = GetString(root, "Ports"),
                    Network = GetString(root, "Networks"),
                    Created = GetString(root, "CreatedAt"),
                    MountCount = GetString(root, "Mounts").Split(',', StringSplitOptions.RemoveEmptyEntries).Length
                });
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }
        }

        return containers;
    }

    public async Task<string> InspectContainerAsync(string name, CancellationToken cancellationToken = default)
    {
        var (exitCode, output, _) = await RunWslDockerAsync($"inspect {EscapeArg(name)}", cancellationToken);
        return exitCode == 0 ? output : "";
    }

    public async Task<(bool Success, string Output)> StartAsync(string name, CancellationToken cancellationToken = default)
    {
        var (exitCode, output, error) = await RunWslDockerAsync($"start {EscapeArg(name)}", cancellationToken);
        return (exitCode == 0, exitCode == 0 ? output : error);
    }

    public async Task<(bool Success, string Output)> StopAsync(string name, CancellationToken cancellationToken = default)
    {
        var (exitCode, output, error) = await RunWslDockerAsync($"stop {EscapeArg(name)}", cancellationToken);
        return (exitCode == 0, exitCode == 0 ? output : error);
    }

    public async Task<(bool Success, string Output)> RestartAsync(string name, CancellationToken cancellationToken = default)
    {
        var (exitCode, output, error) = await RunWslDockerAsync($"restart {EscapeArg(name)}", cancellationToken);
        return (exitCode == 0, exitCode == 0 ? output : error);
    }

    public async Task<(bool Success, string Output)> RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        var (exitCode, output, error) = await RunWslDockerAsync($"rm {EscapeArg(name)}", cancellationToken);
        return (exitCode == 0, exitCode == 0 ? output : error);
    }

    public async Task<string> GetLogsAsync(string name, int tailLines = 500, CancellationToken cancellationToken = default)
    {
        var (exitCode, output, error) = await RunWslDockerAsync(
            $"logs --tail {tailLines} {EscapeArg(name)}", cancellationToken);
        // docker logs sends container stdout to stdout and container stderr to stderr,
        // so combine both streams for a complete view
        var combined = (output + "\n" + error).Trim();
        var raw = exitCode == 0 || !string.IsNullOrEmpty(combined) ? combined : error;
        return NormalizeLogLines(raw);
    }

    /// <summary>
    /// Docker json-file logging driver wraps each line in JSON like:
    /// {"log":"actual text\n","stream":"stdout","time":"..."}
    /// Detect and unwrap these so the user sees plain text.
    /// </summary>
    private static string NormalizeLogLines(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;

        var lines = raw.Split('\n');

        // Quick check: if the first non-empty line doesn't look like JSON, return as-is
        var firstNonEmpty = Array.Find(lines, l => l.TrimStart().StartsWith("{"));
        if (firstNonEmpty is null)
            return raw;

        var sb = new StringBuilder(raw.Length);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(trimmed);
                    if (doc.RootElement.TryGetProperty("log", out var logProp))
                    {
                        var logText = logProp.GetString() ?? "";
                        sb.Append(logText.TrimEnd('\n'));
                        sb.Append('\n');
                        continue;
                    }
                }
                catch (JsonException)
                {
                    // Not valid JSON — fall through to append raw
                }
            }
            sb.Append(line);
            sb.Append('\n');
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<(bool Available, string Message)> CheckAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (exitCode, output, error) = await RunWslDockerAsync("info --format \"{{.ServerVersion}}\"", cancellationToken);
            if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
                return (true, $"Docker {output.Trim()}");

            return (false, !string.IsNullOrWhiteSpace(error) ? error.Trim() : "Docker daemon not running");
        }
        catch (Exception ex)
        {
            return (false, $"WSL/Docker not available: {ex.Message}");
        }
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunWslDockerAsync(
        string dockerArgs, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "wsl",
            Arguments = $"docker {dockerArgs}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        }
        catch (Exception)
        {
            return (-1, "", "wsl not available");
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        return (process.ExitCode, stdOut, stdErr);
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) ? value.GetString() ?? "" : "";
    }

    /// <summary>
    /// Sanitize container name argument to prevent command injection.
    /// Only allows alphanumeric, dash, underscore, dot, and slash characters.
    /// </summary>
    private static string EscapeArg(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Container name cannot be empty.", nameof(name));

        // Only allow safe characters for a Docker container name/ID
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.' && c != '/')
                throw new ArgumentException($"Invalid character '{c}' in container name.", nameof(name));
        }

        return name;
    }
}
