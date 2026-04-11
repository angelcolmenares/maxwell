using System.Text.Json;
using static System.Text.Json.JsonSerializer;
namespace Maxwell;

public sealed class JsonConnectionDefinitionProvider(string jsonFile) : IConnectionDefinitionProvider
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<ConnectionDefinitionList> BuildAsync(CancellationToken cancellationToken = default)
    {
        await using var fs = File.OpenRead(jsonFile);
        var payload = await DeserializeAsync<List<ConnectionDefinition>>(
            fs, Options, cancellationToken);

        return payload is null ? ConnectionDefinitionList.Empty : new ConnectionDefinitionList(payload);
    }

}