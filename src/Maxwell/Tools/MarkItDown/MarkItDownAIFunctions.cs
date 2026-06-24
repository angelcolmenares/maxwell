using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace Maxwell;

/// <summary>
/// Exposes <c>markitdown</c> document-conversion operations as
/// <see cref="AIFunction"/> instances, each guarded by an
/// <see cref="IFileSystemAccessValidator"/>.
/// </summary>
/// <remarks>
/// markitdown (https://github.com/microsoft/markitdown) is a Python CLI tool
/// that converts virtually any document format (PDF, DOCX, PPTX, XLSX, images,
/// HTML, audio, YouTube URLs …) into clean Markdown suitable for LLM pipelines.
///
/// Install it once with:
///   pip install 'markitdown[all]'
///
/// All functions that operate on local files validate the path with the
/// supplied <see cref="IFileSystemAccessValidator"/> before touching disk.
/// URL-based conversions skip file-system validation (no local path involved).
///
/// All functions   : <see cref="GetAllFunctions"/>
/// </remarks>
public sealed class MarkItDownAIFunctions
{
    private readonly IFileSystemAccessValidator _validator;
    private readonly MarkItDownCliRunner        _runner;

    /// <param name="validator">
    /// Validates that local file/directory paths are within the allow-list.
    /// </param>
    /// <param name="runner">
    /// CLI runner to use. A default instance is created when null.
    /// Supply a custom runner to override the executable path or timeout.
    /// </param>
    public MarkItDownAIFunctions(
        IFileSystemAccessValidator validator,
        MarkItDownCliRunner?       runner = null)
    {
        ArgumentNullException.ThrowIfNull(validator);
        _validator = validator;
        _runner    = runner ?? new MarkItDownCliRunner();
    }

    // =========================================================================
    // Conversion functions
    // =========================================================================

    /// <summary>
    /// Converts a local file to Markdown and returns the content as a string.
    /// </summary>
    public AIFunction ConvertFile() =>
        AIFunctionFactory.Create(
            async ([Description("Full path of the file to convert to Markdown.")] string filePath) =>
            {
                if (!await _validator.ValidateAccessAsync(filePath))
                    return AccessDenied(filePath);

                if (!File.Exists(filePath))
                    return $"Error: file not found at '{filePath}'.";

                // No -o flag → markitdown writes Markdown to stdout
                var result = await _runner.RunAsync($"\"{filePath}\"");
                return result.ToAgentString();
            },
            name: "convert_file",
            description: "Converts a local file (PDF, DOCX, PPTX, XLSX, image, audio, HTML, …) to Markdown and returns the content.");

    /// <summary>
    /// Converts a local file to Markdown and saves the result to an output file.
    /// </summary>
    public AIFunction ConvertFileToFile() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the source file to convert.")] string filePath,
                [Description("Full path where the resulting Markdown file should be written.")] string outputPath) =>
            {
                if (!await _validator.ValidateAccessAsync(filePath))
                    return AccessDenied(filePath);

                if (!await _validator.ValidateAccessAsync(outputPath))
                    return AccessDenied(outputPath);

                if (!File.Exists(filePath))
                    return $"Error: source file not found at '{filePath}'.";

                // Ensure the output directory exists
                string? outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                    Directory.CreateDirectory(outputDir);

                var result = await _runner.RunAsync($"\"{filePath}\" -o \"{outputPath}\"");

                return result.IsSuccess
                    ? $"Converted '{filePath}' → '{outputPath}' successfully."
                    : result.ToAgentString();
            },
            name: "convert_file_to_file",
            description: "Converts a local file to Markdown and saves the output to the specified path.");

    /// <summary>
    /// Converts an online URL (web page, YouTube video, etc.) to Markdown
    /// and returns the content as a string.
    /// </summary>
    public AIFunction ConvertUrl() =>
        AIFunctionFactory.Create(
            async ([Description("The URL to convert to Markdown (web page, YouTube link, etc.).")] string url) =>
            {
                if (string.IsNullOrWhiteSpace(url))
                    return "Error: URL cannot be empty.";

                var result = await _runner.RunAsync($"\"{url}\"");
                return result.ToAgentString();
            },
            name: "convert_url",
            description: "Fetches a URL (web page, YouTube video, …) and converts its content to Markdown.");

    /// <summary>
    /// Converts an online URL to Markdown and saves the result to a local file.
    /// </summary>
    public AIFunction ConvertUrlToFile() =>
        AIFunctionFactory.Create(
            async (
                [Description("The URL to convert to Markdown.")] string url,
                [Description("Full path where the resulting Markdown file should be written.")] string outputPath) =>
            {
                if (string.IsNullOrWhiteSpace(url))
                    return "Error: URL cannot be empty.";

                if (!await _validator.ValidateAccessAsync(outputPath))
                    return AccessDenied(outputPath);

                string? outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                    Directory.CreateDirectory(outputDir);

                var result = await _runner.RunAsync($"\"{url}\" -o \"{outputPath}\"");

                return result.IsSuccess
                    ? $"Converted '{url}' → '{outputPath}' successfully."
                    : result.ToAgentString();
            },
            name: "convert_url_to_file",
            description: "Fetches a URL and saves the converted Markdown to a local file.");

    /// <summary>
    /// Converts all files in a directory to individual Markdown files, saved
    /// alongside the originals (or in a separate output directory).
    /// </summary>
    public AIFunction ConvertFolder() =>
        AIFunctionFactory.Create(
            async (
                [Description("Full path of the directory containing files to convert.")] string directoryPath,
                [Description("Optional output directory for the .md files. Defaults to the same directory as the source files.")] string? outputDirectory = null,
                [Description("File extension filter, e.g. '.pdf'. Leave empty to attempt conversion of all files.")] string? extension = null,
                [Description("When true, also processes files in sub-directories.")] bool recursive = false) =>
            {
                if (!await _validator.ValidateAccessAsync(directoryPath))
                    return AccessDenied(directoryPath);

                if (!Directory.Exists(directoryPath))
                    return $"Error: directory not found at '{directoryPath}'.";

                string effectiveOutputDir = outputDirectory ?? directoryPath;

                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    if (!await _validator.ValidateAccessAsync(outputDirectory))
                        return AccessDenied(outputDirectory);

                    Directory.CreateDirectory(outputDirectory);
                }

                SearchOption searchOption = recursive
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                string searchPattern = string.IsNullOrWhiteSpace(extension)
                    ? "*.*"
                    : $"*{extension}";

                string[] files = Directory.GetFiles(directoryPath, searchPattern, searchOption);

                if (files.Length == 0)
                    return "No files found matching the criteria.";

                var results = new List<string>(files.Length);
                int succeeded = 0, failed = 0;

                foreach (string file in files)
                {
                    // Skip files that are already Markdown
                    if (file.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string relativeName    = Path.GetFileNameWithoutExtension(file);
                    string outputPath      = Path.Combine(effectiveOutputDir, relativeName + ".md");

                    var result = await _runner.RunAsync($"\"{file}\" -o \"{outputPath}\"");

                    if (result.IsSuccess)
                    {
                        results.Add($"✓ {Path.GetFileName(file)} → {Path.GetFileName(outputPath)}");
                        succeeded++;
                    }
                    else
                    {
                        results.Add($"✗ {Path.GetFileName(file)}: {result.Stderr}");
                        failed++;
                    }
                }

                results.Insert(0, $"Conversion complete: {succeeded} succeeded, {failed} failed.");
                return string.Join("\n", results);
            },
            name: "convert_folder",
            description: "Converts all files in a directory to Markdown. Saves each result as a .md file alongside (or in a specified output directory).");

    /// <summary>
    /// Checks that markitdown is installed and returns its version.
    /// Useful for the agent to self-diagnose before attempting conversions.
    /// </summary>
    public AIFunction CheckInstallation() =>
        AIFunctionFactory.Create(
            async () =>
            {
                string version = await _runner.GetVersionAsync();
                return version;
            },
            name: "check_markitdown_installation",
            description: "Verifies that markitdown is installed and reachable, and returns its version string.");

    // =========================================================================
    // Function-set accessor
    // =========================================================================

    /// <summary>
    /// Returns all available markitdown functions.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="FileSystemAIFunctions"/> and <see cref="GitAIFunctions"/>,
    /// markitdown is read-only with respect to the source documents — it never
    /// modifies the originals — so there is no meaningful read/write split.
    /// </remarks>
    public List<AIFunction> GetAllFunctions() =>
    [
        CheckInstallation(),
        ConvertFile(),
        ConvertFileToFile(),
        ConvertUrl(),
        ConvertUrlToFile(),
        ConvertFolder(),
    ];

    // =========================================================================
    // Private helpers
    // =========================================================================

    private static string AccessDenied(string path) =>
        $"Access denied: the path '{path}' is not within an allowed directory.";
}