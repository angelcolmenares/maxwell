using System.Text.Json;
using static System.Text.Json.JsonSerializer;

namespace Maxwell;

public class JsonChatStore(string jsonFile) : IChatStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    public async Task<IReadOnlyList<ChatDefinition>> GetAsync(CancellationToken cancellationToken=default)
    {
        await using var fs = File.OpenRead(jsonFile);
        var payload = await DeserializeAsync<List<ChatDefinition>>(
            fs, Options, cancellationToken) ?? [];

        return payload.AsReadOnly();
    }

    public async Task<ChatDefinition?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definitions = await GetAsync(cancellationToken);
        return definitions.FirstOrDefault(f=> f.Id== id);

    }
}