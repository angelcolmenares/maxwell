using System.Text.Json;

namespace Maxwell;

/// <summary>
/// Reads a list of allowed directories from a JSON configuration file
/// and validates path access for an AI agent against that list.
/// </summary>
/// <remarks>
/// Expected JSON structure:
/// <code>
/// {
///   "allowedDirectories": [
///     "/data/safe",
///     "/tmp/agent",
///     "C:\\AgentWorkspace"
///   ]
/// }
/// </code>
/// Path comparison is performed in an OS-aware manner:
/// case-insensitive on Windows, case-sensitive on Unix-like systems.
/// Both the candidate path and the configured directories are
/// normalised via <see cref="Path.GetFullPath"/> before comparison,
/// so trailing separators and relative segments are handled correctly.
/// </remarks>
public sealed class JsonFileSystemAccessValidator : IFileSystemAccessValidator
{
    private IReadOnlyList<string>? _allowedDirectories;
    private readonly string _jsonFilePath ;

    /// <summary>
    /// Initialises a new instance of <see cref="JsonFileSystemAccessValidator"/>
    // </summary>
    /// <param name="jsonFilePath">
    /// The fully-qualified path to the JSON configuration file.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="jsonFilePath"/> is null or whitespace.
    /// </exception>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the file does not exist at the given path.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the JSON is malformed or the expected property is missing.
    /// </exception>
    public JsonFileSystemAccessValidator(string jsonFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonFilePath);

        if (!File.Exists(jsonFilePath))
            throw new FileNotFoundException(
                $"The access-control configuration file was not found: '{jsonFilePath}'",
                jsonFilePath);
        _jsonFilePath= jsonFilePath;
    }

    /// <inheritdoc />
    public async Task<bool> ValidateAccessAsync(string path, CancellationToken cancellationToken =default)
    {
        _allowedDirectories??= await LoadAllowedDirectories(_jsonFilePath,cancellationToken);

        if (string.IsNullOrWhiteSpace(path))
            return false;

        string normalised = NormalisePath(path);

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        bool permitted = _allowedDirectories.Any(allowed =>
            normalised.StartsWith(allowed, comparison));

        return permitted;
    }

    public async Task Reload(CancellationToken cancellationToken = default)
    => _allowedDirectories= await LoadAllowedDirectories(_jsonFilePath, cancellationToken);

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async static Task<IReadOnlyList<string>> LoadAllowedDirectories(string jsonFilePath, CancellationToken cancellationToken)
    {
        using FileStream stream = File.OpenRead(jsonFilePath);

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(stream, cancellationToken:cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse the access-control configuration file: '{jsonFilePath}'.", ex);
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("allowedDirectories", out JsonElement element)
                || element.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    "The JSON configuration file must contain an 'allowedDirectories' array.");
            }

            List<string> directories = [];

            foreach (JsonElement entry in element.EnumerateArray())
            {
                string? raw = entry.GetString();

                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                directories.Add(NormalisePath(raw));
            }

            return directories.AsReadOnly();
        }
    }

    /// <summary>
    /// Resolves the absolute path and appends a trailing directory separator
    /// so that "/data/safe" never accidentally matches "/data/safe-extra".
    /// </summary>
    private static string NormalisePath(string path)
    {
        string full = Path.GetFullPath(path);

        return full.EndsWith(Path.DirectorySeparatorChar)
            ? full
            : full + Path.DirectorySeparatorChar;
    }
}