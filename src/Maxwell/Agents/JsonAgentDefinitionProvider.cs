
using System.Text.Json;
using static System.Text.Json.JsonSerializer;

namespace Maxwell;

public sealed class JsonAgentDefinitionProvider(string jsonFile) : IAgentDefinitionProvider
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<AgentDefintionList> BuildAsync(CancellationToken cancellationToken = default)
    {
        await using var fs = File.OpenRead(jsonFile);
        var payload = await DeserializeAsync<List<AgentDefinition>>(
            fs, Options, cancellationToken);

        return payload is null ? AgentDefintionList.Empty : new AgentDefintionList(payload);
    }

    public async Task<AgentDefintionList> FindByRole(string role, CancellationToken cancellationToken=default)=>
    new((await BuildAsync(cancellationToken)).FindByRole(role));

    
}
