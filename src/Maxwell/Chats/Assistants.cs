using System.Collections;
using Maxwell;
using Microsoft.Agents.AI;

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