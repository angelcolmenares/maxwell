namespace Maxwell;

/// <summary>
/// Immutable result of a single git CLI command execution.
/// </summary>
public sealed class GitCommandResult
{
    /// <summary>The arguments that were passed to git.</summary>
    public string Arguments { get; }

    /// <summary>Process exit code. 0 means success.</summary>
    public int ExitCode { get; }

    /// <summary>Content written to standard output.</summary>
    public string Stdout { get; }

    /// <summary>Content written to standard error.</summary>
    public string Stderr { get; }

    /// <summary>True when the process exited with code 0.</summary>
    public bool IsSuccess => ExitCode == 0;

    /// <summary>True when the command was killed because it exceeded the timeout.</summary>
    public bool TimedOut { get; }

    public GitCommandResult(
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
    internal static GitCommandResult Timeout(string arguments, TimeSpan limit) =>
        new(arguments, exitCode: -1, stdout: string.Empty,
            stderr: $"Command timed out after {limit.TotalSeconds:0}s: git {arguments}",
            timedOut: true);

    /// <summary>
    /// Returns a single string that is useful as an AI function return value.
    /// Combines stdout and stderr so the model has full context.
    /// </summary>
    public string ToAgentString()
    {
        if (TimedOut)
            return $"Error: {Stderr}";

        var parts = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(Stdout))
            parts.Add(Stdout);

        if (!string.IsNullOrWhiteSpace(Stderr))
            parts.Add($"[stderr]\n{Stderr}");

        if (parts.Count == 0)
            parts.Add(IsSuccess ? "(no output)" : $"git exited with code {ExitCode}");

        return string.Join("\n", parts);
    }

    public override string ToString() =>
        $"git {Arguments} → exit {ExitCode}";
}