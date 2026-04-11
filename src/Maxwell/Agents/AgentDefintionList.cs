using System.Text.Json.Serialization;

namespace Maxwell;


[method: JsonConstructor]
public sealed class AgentDefintionList(IEnumerable<AgentDefinition> definitions)
{
    public static readonly AgentDefintionList Empty = new([]);

    public IReadOnlyList<AgentDefinition> Definitions { get; } = definitions.ToList().AsReadOnly();

    public AgentDefinition? FirstOrDefaultByRole(string role)
        => Definitions.FirstOrDefault(c => c.Role.Equals(role, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<AgentDefinition> FindByRole(string role)
        => Definitions.Where(c => c.Role.Equals(role, StringComparison.OrdinalIgnoreCase))
        .ToList()
        .AsReadOnly();

    public IReadOnlyList<AgentFrontmatter> AgentFrontmatters
        => Definitions.Select(f => new AgentFrontmatter
        {
            Name = f.Name,
            Description = f.Description,
            Model = f.Model,
            Role = f.Role
        })
        .ToList()
        .AsReadOnly();
}
