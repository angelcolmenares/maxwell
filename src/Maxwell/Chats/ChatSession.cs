using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;

namespace Maxwell;

public class ChatSession(ChatDefinition chatDefinition, LeaderAgent leader, AssistantsDelegate assistantsDelegate)
{
    private ChatDefinition Chat { get; } = chatDefinition;
    private AIAgent Leader { get; } = leader.Agent;
    public async Task<Assistants> GetAssistants(CancellationToken cancellationToken = default)
    => await assistantsDelegate(cancellationToken);


    public async IAsyncEnumerable<AgentResponseUpdate> RunStreamingAsync(
        string userQuery,
        AgentSession? session = default,
        AgentRunOptions? runOptions = default,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {                            
            await foreach (var update in Leader.RunStreamingAsync(userQuery, session: session, runOptions, cancellationToken))
            {
                yield return update;            
            }            
    }

    public async Task<AgentSession> CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        var session = await Leader.CreateSessionAsync(cancellationToken);
        return session;
    }

    public T? GetService<T>() where T : class
    {
        return Leader.GetService<T>();
    }

}
