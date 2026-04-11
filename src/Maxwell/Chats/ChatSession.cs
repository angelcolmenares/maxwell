using Microsoft.Agents.AI;

namespace Maxwell;
public class ChatSession(ChatDefinition chatDefinition, LeaderAgent leader, AssistantsDelegate assistantsDelegate)
{
    public ChatDefinition Chat { get; } = chatDefinition;
    public AIAgent Leader { get; } = leader.Agent;
    public async Task<Assistants> GetAssistants(CancellationToken cancellationToken=default) 
    => await assistantsDelegate(cancellationToken);

    public LeaderAgent LeaderAgent {get;} = leader;

}
