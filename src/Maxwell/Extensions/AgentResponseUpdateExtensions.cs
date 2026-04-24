using Microsoft.Agents.AI;

namespace Maxwell;

public static class AgentResponseUpdateExtensions
{
    extension(IEnumerable<AgentResponseUpdate> source)
    {
        public AgentMessage? ToAgentMessage(string sender)
        {
            AgentResponse response = source.ToAgentResponse();
            return response.ToAgentMessage(sender);
        }
    }
}