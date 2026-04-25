namespace Maxwell;
 
/// <summary>
/// Persistent compressed knowledge wiki, updated after each Q/A cycle.
/// Inspired by Karpathy-style context compression.
/// </summary>
public interface IWikiStore
{
    ValueTask<string> LoadAsync(CancellationToken ct = default);
    ValueTask SaveAsync(string wikiContent, CancellationToken ct = default);
}