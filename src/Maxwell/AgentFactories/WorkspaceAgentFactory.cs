namespace Maxwell;

public class WorkspaceAgentFactory()
{
    private readonly Dictionary<Guid, AgentFactory> wsFactory = [];

    public async Task<AgentFactory> Create(
        Guid workspaceId,
        IConnectionDefinitionProvider connectionDefinitionProvider,
        CancellationToken cancellationToken=default)
    {
        if( wsFactory.TryGetValue(workspaceId, out var agentFactory))
        {
            return agentFactory;
        }
        
        ConnectionDefinitionList connections = await connectionDefinitionProvider.BuildAsync(cancellationToken);
        agentFactory = new(connections);
        wsFactory.Add(workspaceId, agentFactory);
        return agentFactory;
    }
}