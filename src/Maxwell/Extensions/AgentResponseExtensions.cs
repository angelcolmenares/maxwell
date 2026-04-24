using System.Text.Json;
using Microsoft.Agents.AI;

namespace Maxwell;

public static class AgentResponseExtensions
{
    static readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };
    extension(AgentResponse agentResponse)
    {
        public AgentMessage? ToAgentMessage(string sender)
        {
            string text = agentResponse.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(text)) return null;

            if (text.StartsWith("```"))
            {
                int firstNewline = text.IndexOf('\n');
                int lastFence = text.LastIndexOf("```");
                if (firstNewline > 0 && lastFence > firstNewline)
                    text = text[(firstNewline + 1)..lastFence].Trim();
            }

            try
            {
               AgentMessage? agentMessage = JsonSerializer.Deserialize<AgentMessage>(text, jsonSerializerOptions);
               agentMessage?.Sender= sender;
               return agentMessage;
            }
            catch (Exception exception )
            {
                Console.WriteLine(exception);
                Console.WriteLine("XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX");
                Console.WriteLine(text);
                return null; 
            }
        }
    }
}