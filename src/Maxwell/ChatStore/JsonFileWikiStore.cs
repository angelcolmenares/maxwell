namespace Maxwell;

/// <summary>
/// Stores the compressed knowledge wiki as a plain markdown file on disk.
/// </summary>
public class JsonFileWikiStore : IWikiStore
{
    private readonly string _filePath;

    public JsonFileWikiStore(string filePath)
    {
        _filePath = filePath;
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
    }

    public async ValueTask<string> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath)) return string.Empty;
        return await File.ReadAllTextAsync(_filePath, ct);
    }

    public async ValueTask SaveAsync(string wikiContent, CancellationToken ct = default)
    {
        await File.WriteAllTextAsync(_filePath, wikiContent, ct);
    }
}