namespace Maxwell;

public class FileAgentInstructions(string instructionsDirectory) : IAgentInstructions
{
    public async Task<string> ReadAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken=default)
    {
        var mdFile = Path.Combine(instructionsDirectory, agentDefinition.Name+".md");
        if( !Path.Exists(mdFile)) return string.Empty;
        return await File.ReadAllTextAsync(mdFile, cancellationToken);
    }
}