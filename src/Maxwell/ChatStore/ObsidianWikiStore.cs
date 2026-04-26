using System.Text;

namespace Maxwell;
/*
/// <summary>
/// Stores the wiki as an Obsidian-style folder tree rooted at <paramref name="wikiRoot"/>.
///
/// wikis/
///   index.md
///   [topic]/
///     index.md
///     [subtopic].md
///   log.md
/// </summary>
public sealed class ObsidianWikiStore //: IWikiStore
    private readonly string _root;
    private readonly string _logFile;

    public ObsidianWikiStore(string wikiRoot)
    {
        _root    = wikiRoot;
        _logFile = Path.Combine(_root, "log.md");
        Directory.CreateDirectory(_root);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private string IndexPath()                       => Path.Combine(_root, "index.md");
    private string TopicDir(string topic)            => Path.Combine(_root, Slug(topic));
    private string TopicIndexPath(string topic)      => Path.Combine(TopicDir(topic), "index.md");
    private string SubtopicPath(string t, string s)  => Path.Combine(TopicDir(t), $"{Slug(s)}.md");

    /// <summary>Converts a free-text name to a safe folder/file slug.</summary>
    private static string Slug(string name) =>
        name.Trim()
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('/', '-')
            .Replace('\\', '-');

    private static async ValueTask<string> ReadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return string.Empty;
        return await File.ReadAllTextAsync(path, ct);
    }

    private static async ValueTask WriteAsync(string path, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content, ct);
    }

    // ── IWikiStore: Index ────────────────────────────────────────────────────

    public ValueTask<string> LoadIndexAsync(CancellationToken ct = default)
        => ReadAsync(IndexPath(), ct);

    public ValueTask SaveIndexAsync(string content, CancellationToken ct = default)
        => WriteAsync(IndexPath(), content, ct);

    // ── IWikiStore: Topic index ──────────────────────────────────────────────

    public ValueTask<string> LoadTopicIndexAsync(string topic, CancellationToken ct = default)
        => ReadAsync(TopicIndexPath(topic), ct);

    public ValueTask SaveTopicIndexAsync(string topic, string content, CancellationToken ct = default)
        => WriteAsync(TopicIndexPath(topic), content, ct);

    // ── IWikiStore: Subtopics ────────────────────────────────────────────────

    public ValueTask<string> LoadSubtopicAsync(string topic, string subtopic, CancellationToken ct = default)
        => ReadAsync(SubtopicPath(topic, subtopic), ct);

    public ValueTask SaveSubtopicAsync(string topic, string subtopic, string content, CancellationToken ct = default)
        => WriteAsync(SubtopicPath(topic, subtopic), content, ct);

    // ── IWikiStore: Discovery ────────────────────────────────────────────────

    public ValueTask<IReadOnlyList<string>> ListTopicsAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_root))
            return ValueTask.FromResult<IReadOnlyList<string>>([]);

        var topics = Directory
            .EnumerateDirectories(_root)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();

        return ValueTask.FromResult<IReadOnlyList<string>>(topics);
    }

    public ValueTask<IReadOnlyList<string>> ListSubtopicsAsync(string topic, CancellationToken ct = default)
    {
        string dir = TopicDir(topic);
        if (!Directory.Exists(dir))
            return ValueTask.FromResult<IReadOnlyList<string>>([]);

        var subtopics = Directory
            .EnumerateFiles(dir, "*.md")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => n is not null && n != "index")
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();

        return ValueTask.FromResult<IReadOnlyList<string>>(subtopics);
    }

    // ── IWikiStore: Log ──────────────────────────────────────────────────────

    public async ValueTask AppendLogAsync(WikiLogEntry entry, CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        // First write: create the header
        if (!File.Exists(_logFile))
        {
            sb.AppendLine("# Wiki Update Log");
            sb.AppendLine();
            sb.AppendLine("| Timestamp | Status | Topics Affected | User Message | Error |");
            sb.AppendLine("|-----------|--------|-----------------|--------------|-------|");
        }

        string topics = entry.TopicsAffected.Count > 0
            ? string.Join(", ", entry.TopicsAffected.Select(t => $"`{t}`"))
            : "—";

        // Sanitize table cells (newlines and pipes break markdown tables)
        string msg   = Sanitize(entry.UserMessage, maxLen: 80);
        string error = Sanitize(entry.ErrorMessage ?? "—", maxLen: 120);

        string statusIcon = entry.Status switch
        {
            WikiLogStatus.Success => "✅ Success",
            WikiLogStatus.Failed  => "❌ Failed",
            WikiLogStatus.Skipped => "⏭ Skipped",
            _                     => entry.Status.ToString()
        };

        sb.AppendLine(
            $"| {entry.Timestamp:yyyy-MM-dd HH:mm:ss zzz} | {statusIcon} | {topics} | {msg} | {error} |");

        await File.AppendAllTextAsync(_logFile, sb.ToString(), ct);
    }

    private static string Sanitize(string? value, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(value)) return "—";
        string s = value.Replace('\n', ' ').Replace('\r', ' ').Replace('|', '⏐');
        return s.Length > maxLen ? s[..maxLen] + "…" : s;
    }
}
*/