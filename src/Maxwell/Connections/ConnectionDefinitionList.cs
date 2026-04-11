using System.Text.Json.Serialization;

namespace Maxwell;

[method: JsonConstructor]
public sealed class ConnectionDefinitionList(IReadOnlyList<ConnectionDefinition> definitions)
{
    public static readonly ConnectionDefinitionList Empty = new([]);

    public IReadOnlyList<ConnectionDefinition> Definitions { get; } = definitions;

    public ConnectionDefinition? FindByName(string name)
        => Definitions.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}