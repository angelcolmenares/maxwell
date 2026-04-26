namespace Maxwell;

/// <summary>
/// Persists the conversation index table (index.md).
/// Each row represents one Q/A cycle with status, summary, and a link to the detail file.
/// </summary>
public interface IIndexStore
{
    ValueTask AppendAsync(IndexEntry entry, CancellationToken ct = default);
    /// <summary>Returns the full raw markdown content of index.md, or empty string if not yet created.</summary>
    ValueTask<string> LoadAsync(CancellationToken ct = default);
}

public record IndexEntry(
    DateTimeOffset Timestamp,
    bool Success,
    string UserMessage,
    string Summary,
    string DetailFileName,   // e.g. "ciudad_6.jpeg Description 20260425_155530"
    string? Reference = null // optional raw path/url referenced in the exchange
);
