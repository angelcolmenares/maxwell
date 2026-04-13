using System.Diagnostics;
using System.Text;

namespace Maxwell;

/// <summary>
/// Executes the <c>markitdown</c> CLI as a child process and captures its output.
/// </summary>
/// <remarks>
/// markitdown must be installed and accessible on the system PATH (or at the
/// path supplied to the constructor).
///
/// Install with:
///   pip install 'markitdown[all]'
///
/// Every execution returns a <see cref="MarkItDownCommandResult"/> containing
/// stdout (the Markdown), stderr, and the exit code.
/// </remarks>
public sealed class MarkItDownCliRunner
{
    /// <summary>Default maximum time to wait for a conversion to finish.</summary>
    /// <remarks>
    /// Large files (e.g. multi-hundred-page PDFs) can take longer than the git
    /// default; 5 minutes is a reasonable ceiling for most documents.
    /// </remarks>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(5);

    private readonly string   _executable;
    private readonly TimeSpan _timeout;

    /// <param name="executable">
    /// Path to the markitdown executable. Defaults to <c>"markitdown"</c>
    /// (resolved via PATH). Pass a full path when using a virtual environment,
    /// e.g. <c>"/home/user/.venv/bin/markitdown"</c>.
    /// </param>
    /// <param name="timeout">
    /// Maximum time allowed for a single conversion. Defaults to 5 minutes.
    /// </param>
    public MarkItDownCliRunner(
        string    executable = "markitdown",
        TimeSpan? timeout    = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);
        _executable = executable;
        _timeout    = timeout ?? DefaultTimeout;
    }

    /// <summary>
    /// Runs markitdown with the supplied arguments and returns the result.
    /// </summary>
    /// <param name="arguments">
    /// Arguments passed verbatim, e.g. <c>"report.pdf"</c> or
    /// <c>"report.pdf -o report.md"</c>.
    /// </param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public async Task<MarkItDownCommandResult> RunAsync(
        string            arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName               = _executable,
            Arguments              = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8,
        };

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
            return MarkItDownCommandResult.Timeout(arguments, _timeout);
        }

        return new MarkItDownCommandResult(
            arguments: arguments,
            exitCode:  process.ExitCode,
            stdout:    stdoutBuilder.ToString().TrimEnd(),
            stderr:    stderrBuilder.ToString().TrimEnd());
    }

    /// <summary>
    /// Verifies that the markitdown executable is reachable and returns its
    /// version string, or an error message if it cannot be found.
    /// </summary>
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await RunAsync("--version", cancellationToken);
            return result.IsSuccess
                ? result.Stdout.Trim()
                : $"markitdown not found or returned error: {result.Stderr}";
        }
        catch (Exception ex)
        {
            return $"markitdown executable not reachable: {ex.Message}";
        }
    }
}