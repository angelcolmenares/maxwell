using System.Diagnostics;
using System.Text;

namespace Maxwell;

/// <summary>
/// Executes git CLI commands as child processes and captures their output.
/// </summary>
/// <remarks>
/// Every execution returns a <see cref="GitCommandResult"/> that contains
/// stdout, stderr, and the exit code so callers can decide how to surface
/// the information to the AI agent without throwing exceptions.
/// </remarks>
public sealed class GitCliRunner
{
    /// <summary>Default maximum time to wait for a git command to finish.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(60);

    private readonly string _gitExecutable;
    private readonly TimeSpan _timeout;

    /// <param name="gitExecutable">
    /// Path to the git executable. Defaults to <c>"git"</c> (resolved via PATH).
    /// </param>
    /// <param name="timeout">
    /// Maximum time allowed for a single command. Defaults to 60 seconds.
    /// </param>
    public GitCliRunner(
        string gitExecutable = "git",
        TimeSpan? timeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gitExecutable);
        _gitExecutable = gitExecutable;
        _timeout = timeout ?? DefaultTimeout;
    }

    /// <summary>
    /// Runs a git command inside the specified working directory.
    /// </summary>
    /// <param name="workingDirectory">Repository root (or any sub-directory).</param>
    /// <param name="arguments">
    /// Arguments passed verbatim to git, e.g. <c>"status --short"</c>.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A <see cref="GitCommandResult"/> containing stdout, stderr, and exit code.
    /// </returns>
    public async Task<GitCommandResult> RunAsync(
        string workingDirectory,
        string arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName               = _gitExecutable,
            Arguments              = arguments,
            WorkingDirectory       = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };

        // Ensure git never launches interactive prompts
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var process = new Process { StartInfo = startInfo };

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdoutBuilder.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderrBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_timeout);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            return GitCommandResult.Timeout(arguments, _timeout);
        }

        return new GitCommandResult(
            arguments:  arguments,
            exitCode:   process.ExitCode,
            stdout:     stdoutBuilder.ToString().TrimEnd(),
            stderr:     stderrBuilder.ToString().TrimEnd());
    }
}