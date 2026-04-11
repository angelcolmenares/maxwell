// --- AgentDefinition con Options dinámicas ---
// Resuelve TODO: "add dynamic properties so they can be read from json file
//                and mapped to different AgentOptions (OpenAI, GitHub, etc)"
using System.Text.Json;

namespace Maxwell;

public record AgentDefinition
{
    public required string Name { get; init; }
    public required string Connection { get; init; }
    public required string Model { get; init; }
    public string Description { get; init; } = "";
    public string Role {get;init;} ="";    

    // Propiedades arbitrarias por proveedor: temperature, maxTokens, topP, etc.
    // Se deserializan desde el JSON sin necesidad de subclases.
    public IReadOnlyDictionary<string, JsonElement> Options { get; init; }
        = new Dictionary<string, JsonElement>();

    public static implicit operator AgentFrontmatter(AgentDefinition agentDefinition)
    {
        return new()
        {
            Name= agentDefinition.Name,
            Description= agentDefinition.Description, 
            Model = agentDefinition.Model,
            Role = agentDefinition.Role
        };
    }
}
