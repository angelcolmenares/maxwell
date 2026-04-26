namespace Maxwell;

/// <summary>
/// Writes a single markdown detail file for one Q/A exchange.
/// The file name is derived from the user message + timestamp so it is
/// human-readable and collision-free (matches the wikilink in index.md).
/// </summary>
public interface IDetailStore
{
    /// <summary>
    /// Writes <paramref name="content"/> to a new file and returns
    /// the base name (without extension) used, so it can be linked from index.md.
    /// </summary>
    ValueTask<string> WriteAsync(string baseName, string content, CancellationToken ct = default);
}

public class MarkdownFileDetailStore : IDetailStore
{
    private readonly string _directory;

    public MarkdownFileDetailStore(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async ValueTask<string> WriteAsync(string baseName, string content, CancellationToken ct = default)
    {
        // Sanitize so it is safe as a file name
        string safeName = SanitizeFileName(baseName);
        string filePath = Path.Combine(_directory, safeName + ".md");
        await File.WriteAllTextAsync(filePath, content, ct);
        return safeName;
    }

    /// <summary>
    /// Strips or replaces characters that are illegal in file names on Windows/Linux.
    /// Keeps the name readable (e.g. "ciudad_6.jpeg Description 20260425_155530").
    /// </summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        // Replace path separators with underscores, remove other illegal chars
        var sb = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (c is '/' or '\\') sb.Append('_');
            else if (Array.IndexOf(invalid, c) < 0) sb.Append(c);
        }
        // Collapse multiple spaces/underscores
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString().Trim(), @"\s{2,}", " ");
    }
}
