using System.Collections;
using Microsoft.Agents.AI;


namespace Maxwell;
public delegate Task<Assistants> AssistantsDelegate(CancellationToken cancellationToken=default);
public record Assistants(AgentDefintionList Definitions, IReadOnlyList<AIAgent> Agents) : IEnumerable<AIAgent>
{
    public IEnumerator<AIAgent> GetEnumerator()
    {
        return Agents.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}