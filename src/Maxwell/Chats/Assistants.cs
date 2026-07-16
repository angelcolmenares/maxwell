using System.Collections;
using Maxwell;
using Microsoft.Agents.AI;


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