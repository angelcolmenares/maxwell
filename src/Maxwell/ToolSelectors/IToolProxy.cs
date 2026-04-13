using Microsoft.Extensions.AI;

namespace Maxwell;

public interface IToolProxy
{
    Task<IList<AIFunction>> FindTools(string query, string agentName, CancellationToken cancellationToken = default);
    Task<object?> InvokeTool(string toolName, string agentName, Dictionary<string, object?>? arguments, CancellationToken cancellationToken = default);

    Workspace Workspace {get;}
}