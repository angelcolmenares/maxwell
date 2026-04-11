using Microsoft.Extensions.AI;

namespace Maxwell;
public interface IAssistantProxy
{
    AIFunction FindAssistantsDelegate {get;}
    AIFunction InvokeAssistantDelegate {get;}
}

public interface IRealAssistantProxy
{    
    Task<string> InvokeAssistant(string assistantName, string agentName, AssistantMessage message, CancellationToken cancellationToken = default);
    Task<IList<AgentFrontmatter>> FindAssistants(string query, string agentName, CancellationToken cancellationToken = default);
    Task<Assistants> GetAssistants(CancellationToken cancellationToken =default);
}