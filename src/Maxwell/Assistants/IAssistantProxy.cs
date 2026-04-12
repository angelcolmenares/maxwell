namespace Maxwell;

public interface IAssistantProxy
{
    Task<string> InvokeAssistant(string assistantName, string agentName, AssistantMessage message, CancellationToken cancellationToken = default);
    Task<IList<AgentFrontmatter>> FindAssistants(string query, string agentName, CancellationToken cancellationToken = default);
    Task<Assistants> GetAssistants(CancellationToken cancellationToken = default);

    Workspace Workspace { get; }
}