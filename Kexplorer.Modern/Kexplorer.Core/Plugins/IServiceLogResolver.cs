namespace Kexplorer.Core.Plugins;

/// <summary>
/// Strategy for resolving log file paths for a Windows service based on its binPath.
/// Implementations are tried in order until one matches.
/// </summary>
public interface IServiceLogResolver
{
    /// <summary>
    /// Returns true if this resolver knows how to find logs for the given service.
    /// </summary>
    bool CanResolve(ServiceInfo service, string exePath, Dictionary<string, string> binPathFlags);

    /// <summary>
    /// Returns the full path(s) to the log file(s) for the service.
    /// </summary>
    IReadOnlyList<string> ResolveLogPaths(ServiceInfo service, string exePath, Dictionary<string, string> binPathFlags);
}

/// <summary>
/// Utility for parsing a service binPath into an executable path and flag dictionary.
/// </summary>
public static class BinPathParser
{
    /// <summary>
    /// Parse a binPath string into executable path and flags.
    /// Handles quoted exe paths and -flag value / --flag value style arguments.
    /// </summary>
    public static (string exePath, Dictionary<string, string> flags) Parse(string binPath)
    {
        var flags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(binPath))
            return ("", flags);

        var tokens = Tokenize(binPath);
        if (tokens.Count == 0)
            return ("", flags);

        var exePath = tokens[0];

        for (int i = 1; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (token.StartsWith('-'))
            {
                var raw = token.TrimStart('-');
                // Handle -flag=value syntax
                var eqIdx = raw.IndexOf('=');
                if (eqIdx >= 0)
                {
                    var key = raw[..eqIdx];
                    var value = raw[(eqIdx + 1)..];
                    flags[key] = value;
                }
                // Handle -flag value syntax (space-separated)
                else if (i + 1 < tokens.Count && !tokens[i + 1].StartsWith('-'))
                {
                    flags[raw] = tokens[i + 1];
                    i++;
                }
                else
                {
                    flags[raw] = "true";
                }
            }
        }

        return (exePath, flags);
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < input.Length)
        {
            // Skip whitespace
            while (i < input.Length && char.IsWhiteSpace(input[i]))
                i++;

            if (i >= input.Length) break;

            if (input[i] == '"')
            {
                // Quoted token starting at quote
                i++; // skip opening quote
                int start = i;
                while (i < input.Length && input[i] != '"')
                    i++;
                tokens.Add(input[start..i]);
                if (i < input.Length) i++; // skip closing quote
            }
            else
            {
                // Unquoted token — but may contain embedded quotes (e.g., -flag="value")
                int start = i;
                var sb = new System.Text.StringBuilder();
                while (i < input.Length && !char.IsWhiteSpace(input[i]))
                {
                    if (input[i] == '"')
                    {
                        // Append everything before the quote
                        sb.Append(input[start..i]);
                        i++; // skip opening quote
                        int qStart = i;
                        while (i < input.Length && input[i] != '"')
                            i++;
                        sb.Append(input[qStart..i]);
                        if (i < input.Length) i++; // skip closing quote
                        start = i;
                    }
                    else
                    {
                        i++;
                    }
                }
                sb.Append(input[start..i]);
                tokens.Add(sb.ToString());
            }
        }
        return tokens;
    }
}
