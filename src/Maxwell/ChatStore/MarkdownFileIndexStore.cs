namespace Maxwell;

/// <summary>
/// Appends rows to index.md using the pipe-table format visible in the existing file.
/// Creates the file with a header row on first write.
/// </summary>
public class MarkdownFileIndexStore : IIndexStore
{
    private readonly string _filePath;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    private const string Header =
        "| Timestamp                  | Status    | User Message" +
        "                                                                                     | Summary" +
        "                                                                 | Details" +
        "                                       | References                                                 |\n" +
        "| -------------------------- | --------- | ------------------------------------------------------------------------------------------------" +
        " | ----------------------------------------------------------------------- | --------------------------------------------- | ---------------------------------------------------------- |";

    public MarkdownFileIndexStore(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public async ValueTask<string> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath)) return string.Empty;
        return await File.ReadAllTextAsync(_filePath, ct);
    }

    public async ValueTask AppendAsync(IndexEntry entry, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            bool isNew = !File.Exists(_filePath) || new FileInfo(_filePath).Length == 0;

            string status = entry.Success ? "✅ Success" : "❌ Failed";
            string timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz");
            // Wikipeida-style wikilink to the detail file (no extension in the link text)
            string detailLink = $"[[{entry.DetailFileName}]]";
            string reference = entry.Reference ?? string.Empty;

            string row = $"| {timestamp} | {status} | {Escape(entry.UserMessage)} | {Escape(entry.Summary)} | {detailLink} | {Escape(reference)} |";

            await using var sw = new StreamWriter(_filePath, append: true);
            if (isNew) await sw.WriteLineAsync(Header);
            await sw.WriteLineAsync(row);
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string Escape(string s) => s.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "");
}