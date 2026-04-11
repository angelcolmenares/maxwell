namespace Maxwell;

public interface IAgentInstructions
{
    Task<string> ReadAsync(AgentDefinition agentDefinition, CancellationToken cancellationToken =default);
}