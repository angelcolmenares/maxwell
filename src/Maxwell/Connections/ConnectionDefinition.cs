using System.Text.Json;

namespace Maxwell;

public record ConnectionDefinition
{
    public required string Name { get; init; }
    public required string ClientType { get; init; }
    public IReadOnlyDictionary<string, JsonElement> Options { get; init; }
        = new Dictionary<string, JsonElement>();
}


