namespace Maxwell;

/// <summary>
/// Immutable result of a single markitdown CLI command execution.
/// Mirrors the same shape as <see cref="GitCommandResult"/> so the two
/// runners feel identical to consumers.
/// </summary>
public sealed class MarkItDownCommandResult
{
    /// <summary>The arguments that were passed to markitdown.</summary>
    public string Arguments { get; }

    /// <summary>Process exit code. 0 means success.</summary>
    public int ExitCode { get; }

    /// <summary>Markdown content written to standard output (or empty when -o was used).</summary>
    public string Stdout { get; }

    /// <summary>Content written to standard error (warnings, errors).</summary>
    public string Stderr { get; }

    /// <summary>True when the process exited with code 0.</summary>
    public bool IsSuccess => ExitCode == 0;

    /// <summary>True when the command was killed because it exceeded the timeout.</summary>
    public bool TimedOut { get; }

    public MarkItDownCommandResult(
        string arguments,
        int    exitCode,
        string stdout,
        string stderr,
        bool   timedOut = false)
    {
        Arguments = arguments;
        ExitCode  = exitCode;
        Stdout    = stdout;
        Stderr    = stderr;
        TimedOut  = timedOut;
    }

    /// <summary>Creates a synthetic result representing a timeout.</summary>
    internal static MarkItDownCommandResult Timeout(string arguments, TimeSpan limit) =>
        new(arguments, exitCode: -1, stdout: string.Empty,
            stderr: $"Command timed out after {limit.TotalSeconds:0}s: markitdown {arguments}",
            timedOut: true);

    /// <summary>
    /// Returns a string suitable as an <see cref="Microsoft.Extensions.AI.AIFunction"/> return value.
    /// On success returns the Markdown content; on failure combines stderr and exit code.
    /// </summary>
    public string ToAgentString()
    {
        if (TimedOut)
            return $"Error: {Stderr}";

        if (IsSuccess)
        {
            return string.IsNullOrWhiteSpace(Stdout)
                ? "(conversion succeeded — output was written to file)"
                : Stdout;
        }

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Stderr))
            parts.Add(Stderr);
        parts.Add($"markitdown exited with code {ExitCode}.");
        return string.Join("\n", parts);
    }

    public override string ToString() =>
        $"markitdown {Arguments} → exit {ExitCode}";
}